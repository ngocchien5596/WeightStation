using System.Windows;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.UI.ViewModels.Messages;
using StationApp.UI.Views;

namespace StationApp.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICurrentStationContext _currentStationContext;
    private readonly IStationAuthorizationService _stationAuthorizationService;
    private readonly IStationFeatureService _stationFeatureService;
    private readonly IAppVersionProvider _appVersionProvider;
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
    private Guid? _pendingWeighingSessionId;
    private Guid? _pendingExportCutOrderId;
    private bool _isInitialized;
    private bool _suppressStationChanged;
    private int _navigationVersion;

    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private string? _currentDestination;
    [ObservableProperty] private bool _isSettingsSubmenuVisible;
    [ObservableProperty] private bool _isReportsSubmenuVisible;
    [ObservableProperty] private bool _isSidebarCollapsed;
    [ObservableProperty] private string _currentTimeDisplay = DateTime.Now.ToString("HH:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
    [ObservableProperty] private StationOptionDto? _selectedStation;
    [ObservableProperty] private StationFeatureSetDto _stationFeatures = StationFeatureSetDto.Defaults;

    public ObservableCollection<StationOptionDto> AllowedStations { get; } = new();

    public GridLength SidebarWidth => IsSidebarCollapsed ? new GridLength(56) : new GridLength(176);

    public string CurrentUserDisplayName =>
        string.IsNullOrWhiteSpace(_currentUserContext.DisplayName) ? "\u0043\u0068\u01B0\u0061\u0020\u0111\u0103\u006E\u0067\u0020\u006E\u0068\u1EAD\u0070" : _currentUserContext.DisplayName;

    public string CurrentUserRoleCode => _currentUserContext.RoleCode;
    public string CurrentStationDisplay => _currentStationContext.HasStation
        ? $"{_currentStationContext.StationCode} - {_currentStationContext.StationName}"
        : "Chưa chọn trạm";
    public string AppVersionText => $"v{_appVersionProvider.GetVersion()}";

    public bool CanViewDashboard => StationFeatures.ShowMenuDashboard;
    public bool CanViewIncomingVehicles => StationFeatures.ShowMenuIncomingVehicleList && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewWeighing => StationFeatures.ShowMenuWeighing && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewCrusherWeighing => StationFeatures.ShowMenuCrusherWeighing && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewClayWeighing => StationFeatures.ShowMenuClayWeighing && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewExportWeighing => StationFeatures.ShowMenuExportWeighing && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewOutgoingVehicles => StationFeatures.ShowMenuOutgoingVehicleList && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewReportsMenu => CanViewExportSummaryReport || CanViewInboundSummaryReport || CanViewCrusherInboundReport || CanViewClayInboundReport;
    public bool CanViewExportSummaryReport => StationFeatures.ShowMenuExportReport && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewInboundSummaryReport => StationFeatures.ShowMenuInboundReport && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewCrusherInboundReport => StationFeatures.ShowMenuCrusherInboundReport && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewClayInboundReport => StationFeatures.ShowMenuClayInboundReport && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewTicketList => false;
    public bool CanViewDiagnostics => false;
    public bool CanViewSettingsMenu =>
        StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode)
        || StationAuthorization.CanViewSettingsAdministration(_currentUserContext.RoleCode)
        || StationAuthorization.CanUpdateApplication(_currentUserContext.RoleCode);
    public bool CanViewSettingsParams => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);
    public bool CanViewSettingsDevice => StationAuthorization.CanManageDeviceConfiguration(_currentUserContext.RoleCode);
    public bool CanViewSettingsPrint => StationAuthorization.CanManagePrintLayout(_currentUserContext.RoleCode);
    public bool CanViewSettingsVehicles => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanViewSettingsCustomers => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanViewSettingsProducts => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanViewSettingsSync => StationAuthorization.CanViewSettingsAdministration(_currentUserContext.RoleCode);
    public bool CanViewSettingsExternalDatacan => StationAuthorization.IsAdmin(_currentUserContext.RoleCode);
    public bool CanViewSettingsStations => StationAuthorization.IsAdmin(_currentUserContext.RoleCode);
    public bool CanViewSettingsAccounts => StationAuthorization.CanManageAccounts(_currentUserContext.RoleCode);
    public bool CanViewAppUpdate => StationAuthorization.CanUpdateApplication(_currentUserContext.RoleCode);

    public MainViewModel(
        IServiceProvider serviceProvider,
        ICurrentUserContext currentUserContext,
        ICurrentStationContext currentStationContext,
        IStationAuthorizationService stationAuthorizationService,
        IStationFeatureService stationFeatureService,
        IAppVersionProvider appVersionProvider)
    {
        _serviceProvider = serviceProvider;
        _currentUserContext = currentUserContext;
        _currentStationContext = currentStationContext;
        _stationAuthorizationService = stationAuthorizationService;
        _stationFeatureService = stationFeatureService;
        _appVersionProvider = appVersionProvider;

        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => CurrentTimeDisplay = DateTime.Now.ToString("HH:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
        _clockTimer.Start();

        WeakReferenceMessenger.Default.Register<StationFeaturesChangedMessage>(
            this,
            (_, message) => _ = ReloadStationFeaturesIfCurrentAsync(message.StationCode));
        WeakReferenceMessenger.Default.Register<UserStationAssignmentsChangedMessage>(
            this,
            (_, message) => _ = ReloadAllowedStationsIfCurrentUserAsync(message.UserId));
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await LoadStationContextAsync();
        await Task.Yield();
        await NavigateAsync(ResolveDefaultNavigationTarget());
    }

    private async Task LoadStationContextAsync()
    {
        AllowedStations.Clear();

        if (!_currentUserContext.UserId.HasValue)
        {
            return;
        }

        var stations = await _stationAuthorizationService.GetAllowedStationsAsync(_currentUserContext.UserId.Value, CancellationToken.None);
        foreach (var station in stations)
        {
            AllowedStations.Add(station);
        }

        var currentStation = stations.FirstOrDefault(x => string.Equals(x.StationCode, _currentStationContext.StationCode, StringComparison.OrdinalIgnoreCase))
            ?? stations.FirstOrDefault(x => x.IsDefault)
            ?? stations.FirstOrDefault();

        if (currentStation is not null)
        {
            _suppressStationChanged = true;
            try
            {
                SelectedStation = currentStation;
                _currentStationContext.SetStation(currentStation.StationCode, currentStation.StationName);
                StationFeatures = await _stationFeatureService.GetFeaturesAsync(currentStation.StationCode, CancellationToken.None);
                NotifyAuthorizationPropertiesChanged();
            }
            finally
            {
                _suppressStationChanged = false;
            }
        }

        OnPropertyChanged(nameof(CurrentStationDisplay));
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
    private void ToggleReportsSubmenu()
    {
        if (!CanViewReportsMenu || IsSidebarCollapsed)
        {
            return;
        }

        IsReportsSubmenuVisible = !IsReportsSubmenuVisible;
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
                    weighingVm.NavigateToExportWeighingRequested += async cutOrderId =>
                    {
                        _pendingExportCutOrderId = cutOrderId;
                        await NavigateAsync("ExportWeighing");
                    };
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
                    incomingVm.NavigateToExportWeighingRequested += async cutOrderId =>
                    {
                        _pendingExportCutOrderId = cutOrderId;
                        await NavigateAsync("ExportWeighing");
                    };
                    incomingVm.NavigateToOutgoingRequested += async () => await NavigateAsync("OutgoingVehicles");
                    CurrentView = new IncomingVehicleListView { DataContext = incomingVm };
                    _ = RunViewInitializationAsync(
                        () => incomingVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "ExportWeighing":
                    var exportVm = _serviceProvider.GetRequiredService<ExportWeighingViewModel>();
                    CurrentView = new ExportWeighingView { DataContext = exportVm };
                    _ = RunViewInitializationAsync(async () =>
                    {
                        if (_pendingExportCutOrderId.HasValue)
                        {
                            await exportVm.FocusCutOrderAsync(_pendingExportCutOrderId.Value);
                            _pendingExportCutOrderId = null;
                        }
                        else
                        {
                            await exportVm.InitializeAsync();
                        }
                    }, destination, navigationVersion);
                    break;
                case "CrusherWeighing":
                    var crusherVm = _serviceProvider.GetRequiredService<CrusherWeighingViewModel>();
                    CurrentView = new CrusherWeighingView { DataContext = crusherVm };
                    _ = RunViewInitializationAsync(
                        () => crusherVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "ClayWeighing":
                    var clayVm = _serviceProvider.GetRequiredService<ClayWeighingViewModel>();
                    CurrentView = new ClayWeighingView { DataContext = clayVm };
                    _ = RunViewInitializationAsync(
                        () => clayVm.InitializeAsync(),
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
                case "Reports_ExportSummary":
                    var exportSummaryVm = _serviceProvider.GetRequiredService<ExportSummaryReportViewModel>();
                    CurrentView = new ExportSummaryReportView { DataContext = exportSummaryVm };
                    _ = RunViewInitializationAsync(
                        () => exportSummaryVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "Reports_InboundSummary":
                    var inboundSummaryVm = _serviceProvider.GetRequiredService<InboundSummaryReportViewModel>();
                    CurrentView = new InboundSummaryReportView { DataContext = inboundSummaryVm };
                    _ = RunViewInitializationAsync(
                        () => inboundSummaryVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "Reports_CrusherInbound":
                    var crusherInboundVm = _serviceProvider.GetRequiredService<CrusherInboundReportViewModel>();
                    CurrentView = new CrusherInboundReportView { DataContext = crusherInboundVm };
                    _ = RunViewInitializationAsync(
                        () => crusherInboundVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "Reports_ClayInbound":
                    var clayInboundVm = _serviceProvider.GetRequiredService<ClayInboundReportViewModel>();
                    CurrentView = new ClayInboundReportView { DataContext = clayInboundVm };
                    _ = RunViewInitializationAsync(
                        () => clayInboundVm.InitializeAsync(),
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
                case "Settings_Camera":
                case "Settings_Device":
                case "Settings_Print":
                case "Settings_Vehicles":
                case "Settings_Customers":
                case "Settings_Products":
                case "Settings_Sync":
                case "Settings_ExternalDatacan":
                case "Settings_Stations":
                case "Settings_Accounts":
                case "AppUpdate":
                    var settingsVm = _serviceProvider.GetRequiredService<SettingsViewModel>();
                    if (destination == "AppUpdate")
                    {
                        CurrentView = new AppUpdateView { DataContext = settingsVm.AppUpdateVM };
                        _ = RunViewInitializationAsync(
                            () => settingsVm.AppUpdateVM.LoadAsync(),
                            destination,
                            navigationVersion);
                    }
                    else
                    {
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
                            "Settings_ExternalDatacan" => 7,
                            "Settings_Stations" => 8,
                            "Settings_Accounts" => 9,
                            "Settings_Camera" => 10,
                            _ => (int?)null
                        };
                        _ = RunViewInitializationAsync(
                            () => settingsVm.LoadAsync(initialSettingsTab),
                            destination,
                            navigationVersion);
                    }
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
            "CrusherWeighing" => CanViewCrusherWeighing,
            "ClayWeighing" => CanViewClayWeighing,
            "ExportWeighing" => CanViewExportWeighing,
            "OutgoingVehicles" => CanViewOutgoingVehicles,
            "Reports_ExportSummary" => CanViewExportSummaryReport,
            "Reports_InboundSummary" => CanViewInboundSummaryReport,
            "Reports_CrusherInbound" => CanViewCrusherInboundReport,
            "Reports_ClayInbound" => CanViewClayInboundReport,
            "TicketList" => CanViewTicketList,
            "Diagnostics" => CanViewDiagnostics,
            "Settings" => CanViewSettingsMenu,
            "Settings_Params" => CanViewSettingsParams,
            "Settings_Camera" => CanViewSettingsParams,
            "Settings_Device" => CanViewSettingsDevice,
            "Settings_Print" => CanViewSettingsPrint,
            "Settings_Vehicles" => CanViewSettingsVehicles,
            "Settings_Customers" => CanViewSettingsCustomers,
            "Settings_Products" => CanViewSettingsProducts,
            "Settings_Sync" => CanViewSettingsSync,
            "Settings_ExternalDatacan" => CanViewSettingsExternalDatacan,
            "Settings_Stations" => CanViewSettingsStations,
            "Settings_Accounts" => CanViewSettingsAccounts,
            "AppUpdate" => CanViewAppUpdate,
            _ => false
        };
    }

    partial void OnSelectedStationChanged(StationOptionDto? value)
    {
        if (!_isInitialized || _suppressStationChanged || value is null)
        {
            return;
        }

        _ = SwitchStationAsync(value);
    }

    private async Task SwitchStationAsync(StationOptionDto station)
    {
        if (_currentUserContext.UserId.HasValue)
        {
            await _stationAuthorizationService.EnsureCanAccessStationAsync(_currentUserContext.UserId.Value, station.StationCode, CancellationToken.None);
        }

        _currentStationContext.SetStation(station.StationCode, station.StationName);
        StationFeatures = await _stationFeatureService.GetFeaturesAsync(station.StationCode, CancellationToken.None);
        OnPropertyChanged(nameof(CurrentStationDisplay));
        NotifyAuthorizationPropertiesChanged();

        _pendingWeighingSessionId = null;
        _pendingExportCutOrderId = null;
        IsReportsSubmenuVisible = false;
        IsSettingsSubmenuVisible = false;
        await NavigateAsync(ResolveDefaultNavigationTarget());
    }

    private async Task ReloadStationFeaturesIfCurrentAsync(string stationCode)
    {
        if (string.IsNullOrWhiteSpace(_currentStationContext.StationCode)
            || !string.Equals(_currentStationContext.StationCode, stationCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StationFeatures = await _stationFeatureService.GetFeaturesAsync(stationCode, CancellationToken.None);
        NotifyAuthorizationPropertiesChanged();

        if (CurrentDestination != null && !CanNavigateTo(CurrentDestination))
        {
            await NavigateAsync(ResolveDefaultNavigationTarget());
        }
    }

    private async Task ReloadAllowedStationsIfCurrentUserAsync(Guid userId)
    {
        if (!_currentUserContext.UserId.HasValue || _currentUserContext.UserId.Value != userId)
        {
            return;
        }

        var previousStationCode = _currentStationContext.StationCode;
        var stations = await _stationAuthorizationService.GetAllowedStationsAsync(userId, CancellationToken.None);

        AllowedStations.Clear();
        foreach (var station in stations)
        {
            AllowedStations.Add(station);
        }

        var nextStation = stations.FirstOrDefault(x => string.Equals(x.StationCode, previousStationCode, StringComparison.OrdinalIgnoreCase))
            ?? stations.FirstOrDefault(x => x.IsDefault)
            ?? stations.FirstOrDefault();

        _suppressStationChanged = true;
        try
        {
            SelectedStation = nextStation;
            if (nextStation is null)
            {
                _currentStationContext.Clear();
                StationFeatures = StationFeatureSetDto.Defaults;
                CurrentView = null;
                CurrentDestination = null;
                return;
            }

            _currentStationContext.SetStation(nextStation.StationCode, nextStation.StationName);
            StationFeatures = await _stationFeatureService.GetFeaturesAsync(nextStation.StationCode, CancellationToken.None);
            NotifyAuthorizationPropertiesChanged();
        }
        finally
        {
            _suppressStationChanged = false;
        }

        OnPropertyChanged(nameof(CurrentStationDisplay));
        if (CurrentDestination != null && !CanNavigateTo(CurrentDestination))
        {
            await NavigateAsync(ResolveDefaultNavigationTarget());
        }
    }

    private string ResolveDefaultNavigationTarget()
    {
        var target = StationFeatures.DefaultNavigationTarget;
        if (!string.IsNullOrWhiteSpace(target) && CanNavigateTo(target))
        {
            return target;
        }

        if (CanViewIncomingVehicles) return "IncomingVehicles";
        if (CanViewWeighing) return "Weighing";
        if (CanViewCrusherWeighing) return "CrusherWeighing";
        if (CanViewClayWeighing) return "ClayWeighing";
        if (CanViewDashboard) return "Dashboard";
        if (CanViewOutgoingVehicles) return "OutgoingVehicles";
        if (CanViewClayInboundReport) return "Reports_ClayInbound";
        return "Dashboard";
    }

    partial void OnStationFeaturesChanged(StationFeatureSetDto value)
    {
        NotifyAuthorizationPropertiesChanged();
    }

    private void NotifyAuthorizationPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentStationDisplay));
        OnPropertyChanged(nameof(CanViewDashboard));
        OnPropertyChanged(nameof(CanViewIncomingVehicles));
        OnPropertyChanged(nameof(CanViewWeighing));
        OnPropertyChanged(nameof(CanViewCrusherWeighing));
        OnPropertyChanged(nameof(CanViewClayWeighing));
        OnPropertyChanged(nameof(CanViewExportWeighing));
        OnPropertyChanged(nameof(CanViewOutgoingVehicles));
        OnPropertyChanged(nameof(CanViewReportsMenu));
        OnPropertyChanged(nameof(CanViewExportSummaryReport));
        OnPropertyChanged(nameof(CanViewInboundSummaryReport));
        OnPropertyChanged(nameof(CanViewCrusherInboundReport));
        OnPropertyChanged(nameof(CanViewClayInboundReport));
        OnPropertyChanged(nameof(CanViewSettingsMenu));
        OnPropertyChanged(nameof(CanViewSettingsParams));
        OnPropertyChanged(nameof(CanViewSettingsDevice));
        OnPropertyChanged(nameof(CanViewSettingsPrint));
        OnPropertyChanged(nameof(CanViewSettingsVehicles));
        OnPropertyChanged(nameof(CanViewSettingsCustomers));
        OnPropertyChanged(nameof(CanViewSettingsProducts));
        OnPropertyChanged(nameof(CanViewSettingsSync));
        OnPropertyChanged(nameof(CanViewSettingsExternalDatacan));
        OnPropertyChanged(nameof(CanViewSettingsStations));
        OnPropertyChanged(nameof(CanViewSettingsAccounts));
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
            IsReportsSubmenuVisible = false;
        }
    }
}
