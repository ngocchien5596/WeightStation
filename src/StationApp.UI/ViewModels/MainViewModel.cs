using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
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
    private string? _pendingEditHistoryVehiclePlate;
    private string? _pendingEditHistorySessionNo;
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
        : "ChÆ°a chá»n tráº¡m";
    public string AppVersionText => $"v{_appVersionProvider.GetVersion()}";
    public string InboundSummaryReportMenuText => string.Equals(_currentStationContext.StationCode, "QN01", StringComparison.OrdinalIgnoreCase)
        ? "B\u00e1o c\u00e1o nh\u1eadp h\u00e0ng"
        : "B\u00e1o c\u00e1o c\u00e2n h\u00e0ng";

    public bool CanViewDashboard => StationFeatures.ShowMenuDashboard;
    public bool CanViewIncomingVehicles => StationFeatures.ShowMenuIncomingVehicleList && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewWeighing => StationFeatures.ShowMenuWeighing && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewCrusherWeighing => StationFeatures.ShowMenuCrusherWeighing && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewClayWeighing => StationFeatures.ShowMenuClayWeighing && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewExportWeighing => StationFeatures.ShowMenuExportWeighing && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewOutgoingVehicles => StationFeatures.ShowMenuOutgoingVehicleList && StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);
    public bool CanViewReportsMenu => CanViewExportSummaryReport || CanViewExportScaleReport || CanViewShiftProductOutputReport || CanViewInboundSummaryReport || CanViewCrusherInboundReport || CanViewClayInboundReport || CanViewEditHistoryReport;
    public bool CanViewExportSummaryReport => StationFeatures.ShowMenuExportReport && StationAuthorization.CanViewReports(_currentUserContext.RoleCode);
    public bool CanViewExportScaleReport => CanViewExportSummaryReport;
    public bool CanViewShiftProductOutputReport => CanViewExportSummaryReport && !IsCrusherOrClayStation;
    public bool CanViewInboundSummaryReport => StationFeatures.ShowMenuInboundReport && StationAuthorization.CanViewReports(_currentUserContext.RoleCode);
    public bool CanViewCrusherInboundReport => StationFeatures.ShowMenuCrusherInboundReport && StationAuthorization.CanViewReports(_currentUserContext.RoleCode);
    public bool CanViewClayInboundReport => StationFeatures.ShowMenuClayInboundReport && StationAuthorization.CanViewReports(_currentUserContext.RoleCode);
    public bool CanViewEditHistoryReport => StationAuthorization.CanViewEditHistory(_currentUserContext.RoleCode);
    private bool IsCrusherOrClayStation => string.Equals(_currentStationContext.StationCode, "QN02", StringComparison.OrdinalIgnoreCase)
        || string.Equals(_currentStationContext.StationCode, "QN03", StringComparison.OrdinalIgnoreCase);

    public bool CanViewTicketList => false;
    public bool CanViewDiagnostics => false;
    public bool CanViewSettingsMenu =>
        StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode, _currentStationContext.StationCode)
        || StationAuthorization.CanViewSettingsAdministration(_currentUserContext.RoleCode)
        || StationAuthorization.CanUpdateApplication(_currentUserContext.RoleCode)
        || StationAuthorization.CanManagePrintLayout(_currentUserContext.RoleCode);
    public bool CanViewSettingsParams => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);
    public bool CanViewSettingsDevice => StationAuthorization.CanManageDeviceConfiguration(_currentUserContext.RoleCode);
    public bool CanViewSettingsPrint => StationAuthorization.CanManagePrintLayout(_currentUserContext.RoleCode);
    public bool CanViewSettingsVehicles => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode, _currentStationContext.StationCode);
    public bool CanViewSettingsCustomers => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode, _currentStationContext.StationCode);
    public bool CanViewSettingsProducts => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode, _currentStationContext.StationCode);
    public bool CanViewSettingsIncomingSeedVehicles => string.Equals(_currentStationContext.StationCode, "QN01", StringComparison.OrdinalIgnoreCase)
        && StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode, _currentStationContext.StationCode);
    public bool CanViewSettingsSync => StationAuthorization.CanViewSettingsAdministration(_currentUserContext.RoleCode);
    public bool CanViewSettingsExternalDatacan => StationAuthorization.IsAdmin(_currentUserContext.RoleCode);
    public bool CanViewSettingsStations => StationAuthorization.CanManageStations(_currentUserContext.RoleCode);
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
        _zoomLevel = LoadLocalZoomLevel();

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
            if (!string.IsNullOrEmpty(destination))
            {
                if (destination.StartsWith("Reports_"))
                {
                    IsReportsSubmenuVisible = true;
                }
                else if (destination.StartsWith("Settings_") || destination == "AppUpdate")
                {
                    IsSettingsSubmenuVisible = true;
                }
            }
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
                    crusherVm.NavigateToEditHistoryRequested += async (plate, sessionNo) =>
                    {
                        _pendingEditHistoryVehiclePlate = plate;
                        _pendingEditHistorySessionNo = sessionNo;
                        await NavigateAsync("Reports_EditHistory");
                    };
                    CurrentView = new CrusherWeighingView { DataContext = crusherVm };
                    _ = RunViewInitializationAsync(
                        () => crusherVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "ClayWeighing":
                    var clayVm = _serviceProvider.GetRequiredService<ClayWeighingViewModel>();
                    clayVm.NavigateToEditHistoryRequested += async (plate, sessionNo) =>
                    {
                        _pendingEditHistoryVehiclePlate = plate;
                        _pendingEditHistorySessionNo = sessionNo;
                        await NavigateAsync("Reports_EditHistory");
                    };
                    CurrentView = new ClayWeighingView { DataContext = clayVm };
                    _ = RunViewInitializationAsync(
                        () => clayVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "OutgoingVehicles":
                    var outgoingVm = _serviceProvider.GetRequiredService<OutgoingVehicleListViewModel>();
                    outgoingVm.NavigateToWeighingRequested += async sessionId =>
                    {
                        _pendingWeighingSessionId = sessionId;
                        await NavigateAsync("Weighing");
                    };
                    outgoingVm.NavigateToExportWeighingRequested += async cutOrderId =>
                    {
                        _pendingExportCutOrderId = cutOrderId;
                        await NavigateAsync("ExportWeighing");
                    };
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
                case "Reports_ExportScale":
                    var exportScaleVm = _serviceProvider.GetRequiredService<ExportScaleReportViewModel>();
                    CurrentView = new ExportScaleReportView { DataContext = exportScaleVm };
                    _ = RunViewInitializationAsync(
                        () => exportScaleVm.InitializeAsync(),
                        destination,
                        navigationVersion);
                    break;
                case "Reports_ShiftProductOutput":
                    var shiftProductOutputVm = _serviceProvider.GetRequiredService<ShiftProductOutputReportViewModel>();
                    CurrentView = new ShiftProductOutputReportView { DataContext = shiftProductOutputVm };
                    _ = RunViewInitializationAsync(
                        () => shiftProductOutputVm.InitializeAsync(),
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
                case "Reports_EditHistory":
                    var editHistoryVm = _serviceProvider.GetRequiredService<WeighingSessionEditHistoryViewModel>();
                    if (!string.IsNullOrWhiteSpace(_pendingEditHistoryVehiclePlate) || !string.IsNullOrWhiteSpace(_pendingEditHistorySessionNo))
                    {
                        editHistoryVm.SetFilter(_pendingEditHistoryVehiclePlate, _pendingEditHistorySessionNo);
                        _pendingEditHistoryVehiclePlate = null;
                        _pendingEditHistorySessionNo = null;
                    }
                    CurrentView = new WeighingSessionEditHistoryView { DataContext = editHistoryVm };
                    _ = RunViewInitializationAsync(
                        () => editHistoryVm.InitializeAsync(),
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
                case "Settings_IncomingSeedVehicles":
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
                            "Settings_IncomingSeedVehicles" => 6,
                            "Settings_Sync" => 7,
                            "Settings_ExternalDatacan" => 8,
                            "Settings_Stations" => 9,
                            "Settings_Accounts" => 10,
                            "Settings_Camera" => 11,
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
            "Reports_ExportScale" => CanViewExportScaleReport,
            "Reports_ShiftProductOutput" => CanViewShiftProductOutputReport,
            "Reports_InboundSummary" => CanViewInboundSummaryReport,
            "Reports_CrusherInbound" => CanViewCrusherInboundReport,
            "Reports_ClayInbound" => CanViewClayInboundReport,
            "Reports_EditHistory" => CanViewEditHistoryReport,
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
            "Settings_IncomingSeedVehicles" => CanViewSettingsIncomingSeedVehicles,
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
        _currentUserContext.UpdateStationCode(station.StationCode);
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
        OnPropertyChanged(nameof(InboundSummaryReportMenuText));
        OnPropertyChanged(nameof(CanViewDashboard));
        OnPropertyChanged(nameof(CanViewIncomingVehicles));
        OnPropertyChanged(nameof(CanViewWeighing));
        OnPropertyChanged(nameof(CanViewCrusherWeighing));
        OnPropertyChanged(nameof(CanViewClayWeighing));
        OnPropertyChanged(nameof(CanViewExportWeighing));
        OnPropertyChanged(nameof(CanViewOutgoingVehicles));
        OnPropertyChanged(nameof(CanViewReportsMenu));
        OnPropertyChanged(nameof(CanViewExportSummaryReport));
        OnPropertyChanged(nameof(CanViewExportScaleReport));
        OnPropertyChanged(nameof(CanViewShiftProductOutputReport));
        OnPropertyChanged(nameof(CanViewInboundSummaryReport));
        OnPropertyChanged(nameof(CanViewCrusherInboundReport));
        OnPropertyChanged(nameof(CanViewClayInboundReport));
        OnPropertyChanged(nameof(CanViewEditHistoryReport));
        OnPropertyChanged(nameof(CanViewSettingsMenu));
        OnPropertyChanged(nameof(CanViewSettingsParams));
        OnPropertyChanged(nameof(CanViewSettingsDevice));
        OnPropertyChanged(nameof(CanViewSettingsPrint));
        OnPropertyChanged(nameof(CanViewSettingsVehicles));
        OnPropertyChanged(nameof(CanViewSettingsCustomers));
        OnPropertyChanged(nameof(CanViewSettingsProducts));
        OnPropertyChanged(nameof(CanViewSettingsIncomingSeedVehicles));
        OnPropertyChanged(nameof(CanViewSettingsSync));
        OnPropertyChanged(nameof(CanViewSettingsExternalDatacan));
        OnPropertyChanged(nameof(CanViewSettingsStations));
        OnPropertyChanged(nameof(CanViewSettingsAccounts));
        OnPropertyChanged(nameof(CanViewAppUpdate));
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

    [ObservableProperty] private double _zoomLevel = 1.0;

    public string ZoomPercentageText => $"{Math.Round(ZoomLevel * 100)}%";

    partial void OnZoomLevelChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomPercentageText));
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(1.5, Math.Round(ZoomLevel + 0.05, 2));
        SaveLocalZoomLevel(ZoomLevel);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(0.8, Math.Round(ZoomLevel - 0.05, 2));
        SaveLocalZoomLevel(ZoomLevel);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        SaveLocalZoomLevel(ZoomLevel);
    }

    private const string AppSettingsFileName = "appsettings.json";
    private static readonly string BackupConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StationApp");
    private static readonly string BackupConfigPath = Path.Combine(BackupConfigDir, "zoomsettings.json");

    private double LoadLocalZoomLevel()
    {
        try
        {
            var mainPath = Path.Combine(AppContext.BaseDirectory, AppSettingsFileName);
            if (File.Exists(mainPath))
            {
                var content = File.ReadAllText(mainPath);
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("ZoomLevel", out var element) && element.TryGetDouble(out var mainZoom))
                {
                    return Math.Clamp(mainZoom, 0.8, 1.5);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read from main config: {ex.Message}");
        }

        try
        {
            if (File.Exists(BackupConfigPath))
            {
                var content = File.ReadAllText(BackupConfigPath);
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("ZoomLevel", out var element) && element.TryGetDouble(out var backupZoom))
                {
                    return Math.Clamp(backupZoom, 0.8, 1.5);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read from backup config: {ex.Message}");
        }

        return 1.0;
    }

    private void SaveLocalZoomLevel(double level)
    {
        var mainPath = Path.Combine(AppContext.BaseDirectory, AppSettingsFileName);
        try
        {
            if (File.Exists(mainPath))
            {
                var content = File.ReadAllText(mainPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                if (dict != null)
                {
                    dict["ZoomLevel"] = level;
                    var newContent = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(mainPath, newContent);
                    return;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine("Permission denied to write to main config. Using backup config.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write to main config: {ex.Message}");
        }

        try
        {
            if (!Directory.Exists(BackupConfigDir))
            {
                Directory.CreateDirectory(BackupConfigDir);
            }

            string content = "{}";
            if (File.Exists(BackupConfigPath))
            {
                content = File.ReadAllText(BackupConfigPath);
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(content) ?? new Dictionary<string, object>();
            dict["ZoomLevel"] = level;
            var newContent = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BackupConfigPath, newContent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write to backup config: {ex.Message}");
        }
    }
}




