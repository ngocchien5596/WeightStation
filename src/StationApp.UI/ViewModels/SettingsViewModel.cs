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
    public ViewModels.Settings.ScaleDeviceConfigViewModel ScaleDeviceConfigVM { get; }
    public ViewModels.Settings.VehicleMasterViewModel VehicleMasterVM { get; }
    public ViewModels.Settings.CustomerMasterViewModel CustomerMasterVM { get; }
    public ViewModels.Settings.ProductMasterViewModel ProductMasterVM { get; }
    public ViewModels.Settings.SyncInfoViewModel SyncInfoVM { get; }
    public ViewModels.Settings.AccountManagementViewModel AccountManagementVM { get; }

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _viewTitle = "THAM SO HE THONG";

    public bool CanAccessSystemSettings => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);
    public bool CanAccessScaleDeviceConfig => StationAuthorization.CanManageDeviceConfiguration(_currentUserContext.RoleCode);
    public bool CanAccessVehicleMaster => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanAccessCustomerMaster => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanAccessProductMaster => StationAuthorization.CanViewMasterData(_currentUserContext.RoleCode);
    public bool CanAccessSyncInfo => StationAuthorization.CanViewSettingsAdministration(_currentUserContext.RoleCode);
    public bool CanAccessAccountManagement => StationAuthorization.CanManageAccounts(_currentUserContext.RoleCode);

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (_suppressTabChangedHandler)
        {
            return;
        }

        _ = HandleTabSelectionAsync(value);
    }

    public SettingsViewModel(IServiceScopeFactory scopeFactory, ICurrentUserContext currentUserContext)
    {
        _scopeFactory = scopeFactory;
        _currentUserContext = currentUserContext;
        SystemSettingsVM = new Settings.SystemSettingsViewModel(_scopeFactory, _currentUserContext);
        ScaleDeviceConfigVM = new Settings.ScaleDeviceConfigViewModel(_scopeFactory, _currentUserContext);
        VehicleMasterVM = new Settings.VehicleMasterViewModel(_scopeFactory);
        CustomerMasterVM = new Settings.CustomerMasterViewModel(_scopeFactory);
        ProductMasterVM = new Settings.ProductMasterViewModel(_scopeFactory);
        SyncInfoVM = new Settings.SyncInfoViewModel(_scopeFactory);
        AccountManagementVM = new Settings.AccountManagementViewModel(_scopeFactory);
    }

    public async Task LoadAsync(int? preferredTabIndex = null)
    {
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
            0 => "THAM SO HE THONG",
            1 => "THIET BI CAN",
            2 => "MASTER XE",
            3 => "KHACH HANG",
            4 => "SAN PHAM",
            5 => "DONG BO",
            6 => "QUAN LY TAI KHOAN",
            _ => "CAU HINH HE THONG"
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
                    await VehicleMasterVM.LoadAsync();
                    break;
                case 3:
                    await CustomerMasterVM.LoadAsync();
                    break;
                case 4:
                    await ProductMasterVM.LoadAsync();
                    break;
                case 5:
                    await SyncInfoVM.LoadAsync();
                    break;
                case 6:
                    await AccountManagementVM.LoadAsync();
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
            2 => CanAccessVehicleMaster,
            3 => CanAccessCustomerMaster,
            4 => CanAccessProductMaster,
            5 => CanAccessSyncInfo,
            6 => CanAccessAccountManagement,
            _ => false
        };
    }

    private int GetDefaultAccessibleTabIndex()
    {
        if (CanAccessSystemSettings)
        {
            return 0;
        }

        if (CanAccessVehicleMaster)
        {
            return 2;
        }

        if (CanAccessCustomerMaster)
        {
            return 3;
        }

        if (CanAccessProductMaster)
        {
            return 4;
        }

        return 0;
    }

}
