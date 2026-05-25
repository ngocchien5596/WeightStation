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
    [ObservableProperty] private bool _isSidebarCollapsed;
    [ObservableProperty] private string _currentTimeDisplay = DateTime.Now.ToString("HH:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);

    public GridLength SidebarWidth => IsSidebarCollapsed ? new GridLength(56) : new GridLength(176);

    public string CurrentUserDisplayName =>
        string.IsNullOrWhiteSpace(_currentUserContext.DisplayName) ? "\u0043\u0068\u01B0\u0061\u0020\u0111\u0103\u006E\u0067\u0020\u006E\u0068\u1EAD\u0070" : _currentUserContext.DisplayName;

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
    public bool CanViewSettingsPrint => StationAuthorization.CanManagePrintLayout(_currentUserContext.RoleCode);
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
        _clockTimer.Tick += (_, _) => CurrentTimeDisplay = DateTime.Now.ToString("HH:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
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
        if (!CanViewSettingsMenu || IsSidebarCollapsed)
        {
            return;
        }

        IsSettingsSubmenuVisible = !IsSettingsSubmenuVisible;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    [RelayCommand]
    private async Task NavigateAsync(string destination)
    {
        if (!CanNavigateTo(destination))
        {
            var dialogService = _serviceProvider.GetRequiredService<Services.IDialogService>();
            await dialogService.ShowWarningAsync(
                "\u004B\u0068\u00F4\u006E\u0067\u0020\u0111\u1EE7\u0020\u0071\u0075\u0079\u1EC1\u006E",
                $"\u0042\u1EA1\u006E\u0020\u006B\u0068\u00F4\u006E\u0067\u0020\u0063\u00F3\u0020\u0071\u0075\u0079\u1EC1\u006E\u0020\u0074\u0072\u0075\u0079\u0020\u0063\u1EAD\u0070\u0020{destination}.");
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
                case "Settings_Print":
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
                        "Settings_Print" => 2,
                        "Settings_Vehicles" => 3,
                        "Settings_Customers" => 4,
                        "Settings_Products" => 5,
                        "Settings_Sync" => 6,
                        "Settings_Accounts" => 7,
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
            await dialogService.ShowErrorAsync(
                "\u004C\u1ED7\u0069\u0020\u0068\u1EC7\u0020\u0074\u0068\u1ED1\u006E\u0067",
                $"\u004C\u1ED7\u0069\u0020\u006B\u0068\u0069\u0020\u0063\u0068\u0075\u0079\u1EC3\u006E\u0020\u0068\u01B0\u1EDB\u006E\u0067\u0020\u0111\u1EBF\u006E\u0020{destination}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var dialogService = _serviceProvider.GetRequiredService<Services.IDialogService>();
        var confirmed = await dialogService.ShowConfirmAsync(
            "\u0058\u00E1\u0063\u0020\u006E\u0068\u1EAD\u006E\u0020\u0111\u0103\u006E\u0067\u0020\u0078\u0075\u1EA5\u0074",
            "\u0042\u1EA1\u006E\u0020\u0063\u00F3\u0020\u0063\u0068\u1EAF\u0063\u0020\u006D\u0075\u1ED1\u006E\u0020\u0111\u0103\u006E\u0067\u0020\u0078\u0075\u1EA5\u0074\u0020\u006B\u0068\u00F4\u006E\u0067\u003F",
            "\u0110\u0103\u006E\u0067\u0020\u0078\u0075\u1EA5\u0074",
            "\u004B\u0068\u00F4\u006E\u0067");

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
            "Settings_Print" => CanViewSettingsPrint,
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
            await dialogService.ShowErrorAsync(
                "\u004C\u1ED7\u0069\u0020\u0068\u1EC7\u0020\u0074\u0068\u1ED1\u006E\u0067",
                $"\u004C\u1ED7\u0069\u0020\u006B\u0068\u0069\u0020\u0074\u1EA3\u0069\u0020\u0064\u1EEF\u0020\u006C\u0069\u1EC7\u0075\u0020\u006D\u00E0\u006E\u0020{destination}: {ex.Message}");
        }
    }

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        if (value)
        {
            IsSettingsSubmenuVisible = false;
        }
    }
}
