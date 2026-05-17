using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Device.Abstractions;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;

    [ObservableProperty] private int _pendingTicketsCount;
    [ObservableProperty] private int _inProgressTicketsCount;
    [ObservableProperty] private int _overweightTicketsCount;
    [ObservableProperty] private int _unsyncedTicketsCount;
    [ObservableProperty] private int _completedTodayCount;
    [ObservableProperty] private int _totalVehiclesCount;
    [ObservableProperty] private string _networkStatus = "Đang kiểm tra...";
    [ObservableProperty] private SolidColorBrush _networkStatusBrush = new(Colors.Gray);
    [ObservableProperty] private string _deviceStatus = "Đang kiểm tra...";
    [ObservableProperty] private SolidColorBrush _deviceStatusBrush = new(Colors.Gray);
    [ObservableProperty] private string? _lastSyncTime;
    [ObservableProperty] private string? _stationCode;

    public DashboardViewModel(IServiceScopeFactory scopeFactory, IScaleDevice scaleDevice)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
    }

    public async Task InitializeAsync()
    {
        await LoadCountersAsync();
        CheckDeviceStatus();
        await CheckNetworkStatusAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        PendingTicketsCount = 0;
        InProgressTicketsCount = 0;
        OverweightTicketsCount = 0;
        UnsyncedTicketsCount = 0;
        CompletedTodayCount = 0;
        TotalVehiclesCount = 0;
        NetworkStatus = "Đang kiểm tra...";
        NetworkStatusBrush = new SolidColorBrush(Colors.Gray);
        DeviceStatus = "Đang kiểm tra...";
        DeviceStatusBrush = new SolidColorBrush(Colors.Gray);
        LastSyncTime = null;
        StationCode = null;
        await InitializeAsync();
    }

    private async Task LoadCountersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
        var ticketRepo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var dbContext = scope.ServiceProvider.GetRequiredService<StationDbContext>();

        var activeSessions = await sessionRepo.SearchActiveSessionsAsync(null, CancellationToken.None);
        PendingTicketsCount = activeSessions.Count(x => x.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1);
        InProgressTicketsCount = activeSessions.Count(x =>
            x.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT2
                or WeighingSessionStatus.ALLOCATION_PENDING
                or WeighingSessionStatus.READY_TO_COMPLETE);
        OverweightTicketsCount = activeSessions.Count(x => x.IsOverweight);

        var primaryTickets = await ticketRepo.GetPrimaryDisplayTicketsAsync(null, CancellationToken.None);
        UnsyncedTicketsCount = primaryTickets.Count(t => t.SyncStatus != SyncStatus.SYNC_SUCCESS);

        var completedToday = await sessionRepo.SearchCompletedSessionsAsync(null, clock.TodayLocal, CancellationToken.None);
        CompletedTodayCount = completedToday.Count;

        var vehicles = await vehicleRepo.SearchAsync(null, CancellationToken.None);
        TotalVehiclesCount = vehicles.Count;

        var lastMasterSuccess = await dbContext.SyncOutbox.AsNoTracking()
            .Where(x =>
                (x.AggregateType == SyncAggregateTypes.Vehicle
                || x.AggregateType == SyncAggregateTypes.Customer
                || x.AggregateType == SyncAggregateTypes.Product)
                && x.Status == OutboxStatus.SUCCESS)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);

        StationCode = await appConfig.GetValueAsync("station_code", CancellationToken.None) ?? "N/A";
        LastSyncTime = lastMasterSuccess == null
            ? "Chưa đồng bộ"
            : (lastMasterSuccess.UpdatedAt ?? lastMasterSuccess.CreatedAt).ToString("dd/MM/yyyy HH:mm:ss");
    }

    private void CheckDeviceStatus()
    {
        if (_scaleDevice.IsConnected)
        {
            DeviceStatus = "Đang hoạt động";
            DeviceStatusBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
        }
        else
        {
            DeviceStatus = "Mất kết nối";
            DeviceStatusBrush = new SolidColorBrush(Colors.Red);
        }
    }

    private async Task CheckNetworkStatusAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var apiUrl = await appConfig.GetValueAsync("central_api_url", CancellationToken.None);

            if (string.IsNullOrEmpty(apiUrl))
            {
                NetworkStatus = "Chưa cấu hình";
                NetworkStatusBrush = new SolidColorBrush(Colors.Orange);
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync(apiUrl.TrimEnd('/') + "/health");
            if (response.IsSuccessStatusCode)
            {
                NetworkStatus = "Kết nối OK";
                NetworkStatusBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
            }
            else
            {
                NetworkStatus = $"Lỗi ({response.StatusCode})";
                NetworkStatusBrush = new SolidColorBrush(Colors.Red);
            }
        }
        catch
        {
            NetworkStatus = "Không kết nối được";
            NetworkStatusBrush = new SolidColorBrush(Colors.Red);
        }
    }
}
