using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.UI.Views;

namespace StationApp.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
    private Guid? _pendingWeighingSessionId;

    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private string? _currentDestination;
    [ObservableProperty] private bool _isSettingsSubmenuVisible;

    public string CurrentUserDisplayName =>
        string.IsNullOrWhiteSpace(_currentUserContext.DisplayName) ? "Chưa đăng nhập" : _currentUserContext.DisplayName;

    public string CurrentUserRoleCode => _currentUserContext.RoleCode;

    [ObservableProperty] private string _currentTimeDisplay = DateTime.Now.ToString("HH:mm:ss");

    public MainViewModel(IServiceProvider serviceProvider, ICurrentUserContext currentUserContext)
    {
        _serviceProvider = serviceProvider;
        _currentUserContext = currentUserContext;
        _ = NavigateAsync("IncomingVehicles");

        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => CurrentTimeDisplay = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
    }

    [RelayCommand]
    private void ToggleSettingsSubmenu()
    {
        IsSettingsSubmenuVisible = !IsSettingsSubmenuVisible;
    }

    [RelayCommand]
    private async Task NavigateAsync(string destination)
    {
        try
        {
            CurrentDestination = destination;
            DisposeCurrentViewModel();

            switch (destination)
            {
                case "Weighing":
                    var weighingVm = _serviceProvider.GetRequiredService<WeighingViewModel>();
                    weighingVm.NavigateToOutgoingRequested += async () => await NavigateAsync("OutgoingVehicles");
                    CurrentView = new WeighingView { DataContext = weighingVm };
                    await weighingVm.InitializeAsync();
                    if (_pendingWeighingSessionId.HasValue)
                    {
                        await weighingVm.FocusSessionAsync(_pendingWeighingSessionId.Value);
                        _pendingWeighingSessionId = null;
                    }
                    break;
                case "IncomingVehicles":
                    var incomingVm = _serviceProvider.GetRequiredService<IncomingVehicleListViewModel>();
                    incomingVm.NavigateToWeighingRequested += async (sessionId) =>
                    {
                        _pendingWeighingSessionId = sessionId;
                        await NavigateAsync("Weighing");
                    };
                    CurrentView = new IncomingVehicleListView { DataContext = incomingVm };
                    await incomingVm.InitializeAsync();
                    break;
                case "OutgoingVehicles":
                    var outgoingVm = _serviceProvider.GetRequiredService<OutgoingVehicleListViewModel>();
                    CurrentView = new OutgoingVehicleListView { DataContext = outgoingVm };
                    await outgoingVm.InitializeAsync();
                    break;
                case "Dashboard":
                    var dashboardVm = _serviceProvider.GetRequiredService<DashboardViewModel>();
                    CurrentView = new DashboardView { DataContext = dashboardVm };
                    await dashboardVm.InitializeAsync();
                    break;
                case "TicketList":
                    var ticketVm = _serviceProvider.GetRequiredService<TicketListViewModel>();
                    CurrentView = new TicketListView { DataContext = ticketVm };
                    await ticketVm.LoadTicketsAsync();
                    break;
                case "Diagnostics":
                    var diagnosticsVm = _serviceProvider.GetRequiredService<DiagnosticsViewModel>();
                    CurrentView = new DiagnosticsView { DataContext = diagnosticsVm };
                    await diagnosticsVm.InitializeAsync();
                    break;
                case "Settings":
                case "Settings_Params":
                case "Settings_Device":
                case "Settings_Vehicles":
                case "Settings_Customers":
                case "Settings_Products":
                case "Settings_Sync":
                case "Settings_Accounts":
                    var settingsVm = _serviceProvider.GetRequiredService<SettingsViewModel>();
                    CurrentView = new SettingsView { DataContext = settingsVm };
                    await settingsVm.LoadAsync();

                    settingsVm.SelectedTabIndex = destination switch
                    {
                        "Settings_Device" => 1,
                        "Settings_Vehicles" => 2,
                        "Settings_Customers" => 3,
                        "Settings_Products" => 4,
                        "Settings_Sync" => 5,
                        "Settings_Accounts" => 6,
                        _ => 0
                    };
                    break;
                default:
                    CurrentView = null;
                    break;
            }
        }
        catch (Exception ex)
        {
            var dialogService = _serviceProvider.GetRequiredService<Services.IDialogService>();
            await dialogService.ShowErrorAsync("Lỗi Hệ Thống", $"Lỗi khi chuyển hướng đến {destination}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var dialogService = _serviceProvider.GetRequiredService<Services.IDialogService>();
        var confirmed = await dialogService.ShowConfirmAsync(
            "Xác nhận đăng xuất",
            "Bạn có chắc muốn đăng xuất không?",
            "Đăng xuất",
            "Không");

        if (!confirmed)
        {
            return;
        }

        await ((App)System.Windows.Application.Current).LogoutAsync();
    }

    private void DisposeCurrentViewModel()
    {
        if (CurrentView is FrameworkElement { DataContext: IDisposable disposable })
        {
            disposable.Dispose();
        }
    }
}
