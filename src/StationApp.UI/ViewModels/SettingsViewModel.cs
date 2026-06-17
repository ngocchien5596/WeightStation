using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;

namespace StationApp.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserContext _currentUserContext;
    private bool _suppressTabChangedHandler;

    public ViewModels.Settings.SystemSettingsViewModel SystemSettingsVM { get; }
    public ViewModels.Settings.CameraConfigViewModel CameraConfigVM { get; }
    public ViewModels.Settings.ScaleDeviceConfigViewModel ScaleDeviceConfigVM { get; }
    public ViewModels.Settings.PrintConfigViewModel PrintConfigVM { get; }
    public ViewModels.Settings.VehicleMasterViewModel VehicleMasterVM { get; }
    public ViewModels.Settings.CustomerMasterViewModel CustomerMasterVM { get; }
    public ViewModels.Settings.ProductMasterViewModel ProductMasterVM { get; }
    public ViewModels.Settings.SyncInfoViewModel SyncInfoVM { get; }
    public ViewModels.Settings.ExternalDatacanViewModel ExternalDatacanVM { get; }
    public ViewModels.Settings.StationMasterViewModel StationMasterVM { get; }
    public ViewModels.Settings.AccountManagementViewModel AccountManagementVM { get; }
    public AppUpdateViewModel AppUpdateVM { get; }

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _viewTitle = "\u0054\u0048\u0041\u004D\u0020\u0053\u1ED0\u0020\u0048\u1EC6\u0020\u0054\u0048\u1ED0\u004E\u0047";

    public bool CanAccessSystemSettings => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);
    public bool CanAccessScaleDeviceConfig => StationAuthorization.CanManageDeviceConfiguration(_currentUserContext.RoleCode);
    public bool CanAccessPrintConfig => StationAuthorization.CanManagePrintLayout(_currentUserContext.RoleCode);
    public bool CanAccessVehicleMaster => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanAccessCustomerMaster => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanAccessProductMaster => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanAccessSyncInfo => StationAuthorization.CanViewSettingsAdministration(_currentUserContext.RoleCode);
    public bool CanAccessExternalDatacan => StationAuthorization.IsAdmin(_currentUserContext.RoleCode);
    public bool CanAccessStationMaster => StationAuthorization.IsAdmin(_currentUserContext.RoleCode);
    public bool CanAccessAccountManagement => StationAuthorization.CanManageAccounts(_currentUserContext.RoleCode);
    public bool CanAccessAppUpdate => StationAuthorization.CanUpdateApplication(_currentUserContext.RoleCode);

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (_suppressTabChangedHandler)
        {
            return;
        }

        _ = HandleTabSelectionAsync(value);
    }

    public SettingsViewModel(
        IServiceScopeFactory scopeFactory,
        ICurrentUserContext currentUserContext,
        Device.Abstractions.IScaleDevice scaleDevice,
        AppUpdateViewModel appUpdateViewModel)
    {
        _scopeFactory = scopeFactory;
        _currentUserContext = currentUserContext;
        using var scope = _scopeFactory.CreateScope();
        var currentStationContext = scope.ServiceProvider.GetRequiredService<ICurrentStationContext>();

        SystemSettingsVM = new Settings.SystemSettingsViewModel(_scopeFactory, _currentUserContext);
        CameraConfigVM = new Settings.CameraConfigViewModel(_scopeFactory, _currentUserContext);
        ScaleDeviceConfigVM = new Settings.ScaleDeviceConfigViewModel(_scopeFactory, _currentUserContext, scaleDevice);
        PrintConfigVM = new Settings.PrintConfigViewModel(_scopeFactory, _currentUserContext);
        VehicleMasterVM = new Settings.VehicleMasterViewModel(_scopeFactory, currentStationContext);
        CustomerMasterVM = new Settings.CustomerMasterViewModel(_scopeFactory);
        ProductMasterVM = new Settings.ProductMasterViewModel(_scopeFactory);
        SyncInfoVM = new Settings.SyncInfoViewModel(_scopeFactory);
        ExternalDatacanVM = new Settings.ExternalDatacanViewModel(_scopeFactory);
        StationMasterVM = new Settings.StationMasterViewModel(_scopeFactory);
        AccountManagementVM = new Settings.AccountManagementViewModel(_scopeFactory);
        AppUpdateVM = appUpdateViewModel;
    }

    public async Task LoadAsync(int? preferredTabIndex = null)
    {
        await AppUpdateVM.LoadAsync();

        var targetTabIndex = preferredTabIndex.HasValue && CanAccessTab(preferredTabIndex.Value)
            ? preferredTabIndex.Value
            : GetDefaultAccessibleTabIndex();

        _suppressTabChangedHandler = true;
        try
        {
            SelectedTabIndex = targetTabIndex;
        }
        finally
        {
            _suppressTabChangedHandler = false;
        }

        await HandleTabSelectionAsync(targetTabIndex);
    }

    private async Task HandleTabSelectionAsync(int tabIndex)
    {
        if (!CanAccessTab(tabIndex))
        {
            var fallbackTab = GetDefaultAccessibleTabIndex();
            if (SelectedTabIndex != fallbackTab)
            {
                SelectedTabIndex = fallbackTab;
            }

            return;
        }

        ViewTitle = tabIndex switch
        {
            0 => "\u0054\u0048\u0041\u004D\u0020\u0053\u1ED0\u0020\u0048\u1EC6\u0020\u0054\u0048\u1ED0\u004E\u0047",
            1 => "\u0054\u0048\u0049\u1EBE\u0054\u0020\u0042\u1ECA\u0020\u0043\u00C2\u004E",
            2 => "\u0043\u1EA4\u0055\u0020\u0048\u00CC\u004E\u0048\u0020\u0049\u004E",
            3 => "\u0044\u0041\u004E\u0048\u0020\u004D\u1EE4\u0043\u0020\u0058\u0045",
            4 => "\u004B\u0048\u00C1\u0043\u0048\u0020\u0048\u00C0\u004E\u0047",
            5 => "\u0053\u1EA2\u004E\u0020\u0050\u0048\u1EA8\u004D",
            6 => "\u0110\u1ED2\u004E\u0047\u0020\u0042\u1ED8",
            7 => "\u004C\u1ECA\u0043\u0048\u0020\u0053\u1EEC\u0020\u0043\u00C2\u004E\u0020\u0028\u0050\u004D\u0020\u0043\u0168\u0029",
            8 => "\u0044\u0041\u004E\u0048\u0020\u004D\u1EE4\u0043\u0020\u0054\u0052\u1EA0\u004D",
            9 => "\u0051\u0055\u1EA2\u004E\u0020\u004C\u00DD\u0020\u0054\u00C0\u0049\u0020\u004B\u0048\u004F\u1EA2\u004E",
            10 => "\u0043\u1EA4\u0055\u0020\u0048\u00CC\u004E\u0048\u0020\u0043\u0041\u004D\u0045\u0052\u0041",
            _ => "\u0043\u1EA4\u0055\u0020\u0048\u00CC\u004E\u0048\u0020\u0048\u1EC6\u0020\u0054\u0048\u1ED0\u004E\u0047"
        };

        try
        {
            switch (tabIndex)
            {
                case 0:
                    await SystemSettingsVM.LoadAsync();
                    break;
                case 1:
                    await ScaleDeviceConfigVM.LoadAsync();
                    break;
                case 2:
                    await PrintConfigVM.LoadAsync();
                    break;
                case 3:
                    await VehicleMasterVM.LoadAsync();
                    break;
                case 4:
                    await CustomerMasterVM.LoadAsync();
                    break;
                case 5:
                    await ProductMasterVM.LoadAsync();
                    break;
                case 6:
                    await SyncInfoVM.LoadAsync();
                    break;
                case 7:
                    await ExternalDatacanVM.LoadAsync();
                    break;
                case 8:
                    await StationMasterVM.LoadAsync();
                    break;
                case 9:
                    await AccountManagementVM.LoadAsync();
                    break;
                case 10:
                    await CameraConfigVM.LoadAsync();
                    break;
            }
        }
        catch
        {
        }
    }

    private bool CanAccessTab(int tabIndex)
    {
        return tabIndex switch
        {
            0 => CanAccessSystemSettings,
            1 => CanAccessScaleDeviceConfig,
            2 => CanAccessPrintConfig,
            3 => CanAccessVehicleMaster,
            4 => CanAccessCustomerMaster,
            5 => CanAccessProductMaster,
            6 => CanAccessSyncInfo,
            7 => CanAccessExternalDatacan,
            8 => CanAccessStationMaster,
            9 => CanAccessAccountManagement,
            10 => CanAccessSystemSettings,
            _ => false
        };
    }

    private int GetDefaultAccessibleTabIndex()
    {
        if (CanAccessSystemSettings)
        {
            return 0;
        }

        if (CanAccessScaleDeviceConfig)
        {
            return 1;
        }

        if (CanAccessPrintConfig)
        {
            return 2;
        }

        if (CanAccessVehicleMaster)
        {
            return 3;
        }

        if (CanAccessCustomerMaster)
        {
            return 4;
        }

        if (CanAccessProductMaster)
        {
            return 5;
        }

        if (CanAccessSyncInfo)
        {
            return 6;
        }

        if (CanAccessExternalDatacan)
        {
            return 7;
        }

        if (CanAccessStationMaster)
        {
            return 8;
        }

        if (CanAccessAccountManagement)
        {
            return 9;
        }

        return 0;
    }
}
