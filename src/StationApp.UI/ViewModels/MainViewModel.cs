using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.UI.Views;

namespace StationApp.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
    private Guid? _pendingWeighingSessionId;
    private bool _isInitialized;
    private int _navigationVersion;

    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private string? _currentDestination;
    [ObservableProperty] private bool _isSettingsSubmenuVisible;
    [ObservableProperty] private string _currentTimeDisplay = DateTime.Now.ToString("HH:mm:ss");

    public string CurrentUserDisplayName =>
        string.IsNullOrWhiteSpace(_currentUserContext.DisplayName) ? "Chưa đăng nhập" : _currentUserContext.DisplayName;

    public string CurrentUserRoleCode => _currentUserContext.RoleCode;

    public bool CanViewDashboard => true;
    public bool CanViewIncomingVehicles => StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewWeighing => StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewOutgoingVehicles => StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewTicketList => StationAuthorization.CanViewTicketLookup(_currentUserContext.RoleCode);
    public bool CanViewDiagnostics => StationAuthorization.CanViewDiagnostics(_currentUserContext.RoleCode);
    public bool CanViewSettingsMenu => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode) || StationAuthorization.CanViewSettingsAdministration(_currentUserContext.RoleCode);
    public bool CanViewSettingsParams => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);
    public bool CanViewSettingsDevice => StationAuthorization.CanManageDeviceConfiguration(_currentUserContext.RoleCode);
    public bool CanViewSettingsVehicles => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanViewSettingsCustomers => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanViewSettingsProducts => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanViewSettingsSync => StationAuthorization.CanViewSettingsAdministration(_currentUserContext.RoleCode);
    public bool CanViewSettingsAccounts => StationAuthorization.CanManageAccounts(_currentUserContext.RoleCode);

    public MainViewModel(IServiceProvider serviceProvider, ICurrentUserContext currentUserContext)
    {
        _serviceProvider = serviceProvider;
        _currentUserContext = currentUserContext;

        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => CurrentTimeDisplay = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await Task.Yield();
        await NavigateAsync("IncomingVehicles");
    }

    [RelayCommand]
    private void ToggleSettingsSubmenu()
    {
        if (!CanViewSettingsMenu)
        {
            return;
        }

        IsSettingsSubmenuVisible = !IsSettingsSubmenuVisible;
    }

    [RelayCommand]
    private async Task NavigateAsync(string destination)
    {
        if (!CanNavigateTo(destination))
        {
            var dialogService = _serviceProvider.GetRequiredService<Services.IDialogService>();
            await dialogService.ShowWarningAsync("Không đủ quyền", $"Bạn không có quyền truy cập {destination}.");
            return;
        }

        try
        {
            var navigationVersion = ++_navigationVersion;
            CurrentDestination = destination;
            DisposeCurrentViewModel();

            switch (destination)
            {
                case "Weighing":
                    var weighingVm = _serviceProvider.GetRequiredService<WeighingViewModel>();
                    weighingVm.NavigateToOutgoingRequested += async () => await NavigateAsync("OutgoingVehicles");
                    CurrentView = new WeighingView { DataContext = weighingVm };
                    _ = RunViewInitializationAsync(async () =>
                    {
                        await weighingVm.InitializeAsync();
                        if (_pendingWeighingSessionId.HasValue)
                        {
                            await weighingVm.FocusSessionAsync(_pendingWeighingSessionId.Value);
                            _pendingWeighingSessionId = null;
                        }
                    }, destination, navigationVersion);
                    break;
                case "IncomingVehicles":
                    var incomingVm = _serviceProvider.GetRequiredService<IncomingVehicleListViewModel>();
                    incomingVm.NavigateToWeighingRequested += async sessionId =>
                    {
                        _pendingWeighingSessionId = sessionId;
                        await NavigateAsync("Weighing");
                    };
                    incomingVm.NavigateToOutgoingRequested += async () => await NavigateAsync("OutgoingVehicles");
                    CurrentView = new IncomingVehicleListView { DataContext = incomingVm };
                    _ = RunViewInitializationAsync(
                        () => incomingVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "OutgoingVehicles":
                    var outgoingVm = _serviceProvider.GetRequiredService<OutgoingVehicleListViewModel>();
                    CurrentView = new OutgoingVehicleListView { DataContext = outgoingVm };
                    _ = RunViewInitializationAsync(
                        () => outgoingVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "Dashboard":
                    var dashboardVm = _serviceProvider.GetRequiredService<DashboardViewModel>();
                    CurrentView = new DashboardView { DataContext = dashboardVm };
                    _ = RunViewInitializationAsync(
                        () => dashboardVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "TicketList":
                    var ticketVm = _serviceProvider.GetRequiredService<TicketListViewModel>();
                    CurrentView = new TicketListView { DataContext = ticketVm };
                    _ = RunViewInitializationAsync(
                        () => ticketVm.LoadTicketsAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "Diagnostics":
                    var diagnosticsVm = _serviceProvider.GetRequiredService<DiagnosticsViewModel>();
                    CurrentView = new DiagnosticsView { DataContext = diagnosticsVm };
                    _ = RunViewInitializationAsync(
                        () => diagnosticsVm.InitializeAsync(),
                        destination,
                        navigationVersion);
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
                    var initialSettingsTab = destination switch
                    {
                        "Settings_Params" => 0,
                        "Settings_Device" => 1,
                        "Settings_Vehicles" => 2,
                        "Settings_Customers" => 3,
                        "Settings_Products" => 4,
                        "Settings_Sync" => 5,
                        "Settings_Accounts" => 6,
                        _ => (int?)null
                    };
                    _ = RunViewInitializationAsync(
                        () => settingsVm.LoadAsync(initialSettingsTab),
                        destination,
                        navigationVersion);
                    break;
                default:
                    CurrentView = null;
                    break;
            }
        }
        catch (Exception ex)
        {
            var dialogService = _serviceProvider.GetRequiredService<Services.IDialogService>();
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi chuyển hướng đến {destination}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var dialogService = _serviceProvider.GetRequiredService<Services.IDialogService>();
        var confirmed = await dialogService.ShowConfirmAsync(
            "Xac nhan dang xuat",
            "Ban co chac muon dang xuat khong?",
            "Dang xuat",
            "Khong");

        if (!confirmed)
        {
            return;
        }

        await ((App)System.Windows.Application.Current).LogoutAsync();
    }

    private bool CanNavigateTo(string destination)
    {
        return destination switch
        {
            "Dashboard" => CanViewDashboard,
            "IncomingVehicles" => CanViewIncomingVehicles,
            "Weighing" => CanViewWeighing,
            "OutgoingVehicles" => CanViewOutgoingVehicles,
            "TicketList" => CanViewTicketList,
            "Diagnostics" => CanViewDiagnostics,
            "Settings" => CanViewSettingsMenu,
            "Settings_Params" => CanViewSettingsParams,
            "Settings_Device" => CanViewSettingsDevice,
            "Settings_Vehicles" => CanViewSettingsVehicles,
            "Settings_Customers" => CanViewSettingsCustomers,
            "Settings_Products" => CanViewSettingsProducts,
            "Settings_Sync" => CanViewSettingsSync,
            "Settings_Accounts" => CanViewSettingsAccounts,
            _ => false
        };
    }

    private void DisposeCurrentViewModel()
    {
        if (CurrentView is FrameworkElement { DataContext: IDisposable disposable })
        {
            disposable.Dispose();
        }
    }

    private async Task RunViewInitializationAsync(Func<Task> initializeAsync, string destination, int navigationVersion)
    {
        try
        {
            using var perfScope = Helpers.PerformanceLogger.Track($"Main.NavigateInit.{destination}");
            await Task.Yield();

            if (navigationVersion != _navigationVersion)
            {
                return;
            }

            await initializeAsync();
        }
        catch (Exception ex)
        {
            if (navigationVersion != _navigationVersion)
            {
                return;
            }

            var dialogService = _serviceProvider.GetRequiredService<Services.IDialogService>();
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi tải dữ liệu màn {destination}: {ex.Message}");
        }
    }
}
