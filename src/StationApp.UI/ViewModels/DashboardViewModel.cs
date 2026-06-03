using System.Linq;
using System.Threading;
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
    [ObservableProperty] private string _networkStatus = "Dang kiem tra...";
    [ObservableProperty] private SolidColorBrush _networkStatusBrush = new(Colors.Gray);
    [ObservableProperty] private string _deviceStatus = "Dang kiem tra...";
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
        NetworkStatus = "Dang kiem tra...";
        NetworkStatusBrush = new SolidColorBrush(Colors.Gray);
        DeviceStatus = "Dang kiem tra...";
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
                || x.AggregateType == SyncAggregateTypes.Product
                || x.AggregateType == SyncAggregateTypes.WeighingSession
                || x.AggregateType == SyncAggregateTypes.WeighingSessionLine)
                && x.Status == OutboxStatus.SUCCESS)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(CancellationToken.None);

        StationCode = await appConfig.GetValueAsync("station_code", CancellationToken.None) ?? "N/A";
        LastSyncTime = lastMasterSuccess == null
            ? "Chua dong bo"
            : (lastMasterSuccess.UpdatedAt ?? lastMasterSuccess.CreatedAt).ToString("dd/MM/yyyy HH:mm:ss");
    }

    private void CheckDeviceStatus()
    {
        if (_scaleDevice.IsConnected)
        {
            DeviceStatus = "Dang hoat dong";
            DeviceStatusBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
        }
        else
        {
            DeviceStatus = "Mat ket noi";
            DeviceStatusBrush = new SolidColorBrush(Colors.Red);
        }
    }

    private async Task CheckNetworkStatusAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var checker = scope.ServiceProvider.GetRequiredService<ICentralApiHealthChecker>();
        var result = await checker.CheckAsync(CancellationToken.None);
        if (result.Success)
        {
            NetworkStatus = "Ket noi OK";
            NetworkStatusBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
            return;
        }

        NetworkStatus = result.StatusCode == "CONFIG_INVALID"
            ? "Chua cau hinh"
            : result.Message;
        NetworkStatusBrush = result.StatusCode == "CONFIG_INVALID"
            ? new SolidColorBrush(Colors.Orange)
            : new SolidColorBrush(Colors.Red);
    }
}
