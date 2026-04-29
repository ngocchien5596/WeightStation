using System.Net.Http;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Device.Abstractions;
using StationApp.Domain.Enums;

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
        await InitializeAsync();
    }

    private async Task LoadCountersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var ticketRepo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var created = await ticketRepo.GetByStatusAsync(TicketStatus.TICKET_CREATED, CancellationToken.None);
        PendingTicketsCount = created.Count;

        var loading = await ticketRepo.GetByStatusAsync(TicketStatus.LOADING_STARTED, CancellationToken.None);
        InProgressTicketsCount = loading.Count;

        var all = await ticketRepo.SearchAsync(null, null, CancellationToken.None);
        OverweightTicketsCount = all.Count(t => t.IsOverWeight);
        UnsyncedTicketsCount = all.Count(t => t.SyncStatus != SyncStatus.SYNC_SUCCESS);
        CompletedTodayCount = all.Count(t => t.Status == TicketStatus.TICKET_COMPLETED
            && t.CreatedAt.Date == clock.TodayLocal);

        var vehicles = await vehicleRepo.SearchAsync(null, CancellationToken.None);
        TotalVehiclesCount = vehicles.Count;

        StationCode = await appConfig.GetValueAsync("station_code", CancellationToken.None) ?? "N/A";
        LastSyncTime = await appConfig.GetValueAsync("master_data_last_sync", CancellationToken.None) ?? "Chưa đồng bộ";
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
