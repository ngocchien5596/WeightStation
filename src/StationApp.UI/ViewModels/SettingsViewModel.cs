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
    public ViewModels.Settings.PrintConfigViewModel PrintConfigVM { get; }
    public ViewModels.Settings.VehicleMasterViewModel VehicleMasterVM { get; }
    public ViewModels.Settings.CustomerMasterViewModel CustomerMasterVM { get; }
    public ViewModels.Settings.ProductMasterViewModel ProductMasterVM { get; }
    public ViewModels.Settings.SyncInfoViewModel SyncInfoVM { get; }
    public ViewModels.Settings.AccountManagementViewModel AccountManagementVM { get; }

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _viewTitle = "THAM SỐ HỆ THỐNG";

    public bool CanAccessSystemSettings => StationAuthorization.CanManageSystemSettings(_currentUserContext.RoleCode);
    public bool CanAccessScaleDeviceConfig => StationAuthorization.CanManageDeviceConfiguration(_currentUserContext.RoleCode);
    public bool CanAccessPrintConfig => StationAuthorization.CanManagePrintLayout(_currentUserContext.RoleCode);
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

    public SettingsViewModel(IServiceScopeFactory scopeFactory, ICurrentUserContext currentUserContext, Device.Abstractions.IScaleDevice scaleDevice)
    {
        _scopeFactory = scopeFactory;
        _currentUserContext = currentUserContext;

        SystemSettingsVM = new Settings.SystemSettingsViewModel(_scopeFactory, _currentUserContext);
        ScaleDeviceConfigVM = new Settings.ScaleDeviceConfigViewModel(_scopeFactory, _currentUserContext, scaleDevice);
        PrintConfigVM = new Settings.PrintConfigViewModel(_scopeFactory, _currentUserContext);
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
            0 => "THAM SỐ HỆ THỐNG",
            1 => "THIẾT BỊ CÂN",
            2 => "CẤU HÌNH IN",
            3 => "DANH MỤC XE",
            4 => "KHÁCH HÀNG",
            5 => "SẢN PHẨM",
            6 => "ĐỒNG BỘ",
            7 => "QUẢN LÝ TÀI KHOẢN",
            _ => "CẤU HÌNH HỆ THỐNG"
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
            2 => CanAccessPrintConfig,
            3 => CanAccessVehicleMaster,
            4 => CanAccessCustomerMaster,
            5 => CanAccessProductMaster,
            6 => CanAccessSyncInfo,
            7 => CanAccessAccountManagement,
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

        if (CanAccessAccountManagement)
        {
            return 7;
        }

        return 0;
    }
}
