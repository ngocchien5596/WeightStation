using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Device.Abstractions;
using StationApp.Device.Implementations;
using StationApp.Domain.Entities;

namespace StationApp.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserContext _currentUserContext;

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

    public async Task LoadAsync()
    {
        SelectedTabIndex = GetDefaultAccessibleTabIndex();
        await LoadMasterDataAsync();
        await HandleTabSelectionAsync(SelectedTabIndex);
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

    [ObservableProperty] private string? _stationCode;
    [ObservableProperty] private string? _ticketPrefix;
    [ObservableProperty] private string? _toleranceKg;
    [ObservableProperty] private string? _syncInterval;
    [ObservableProperty] private string? _centralApiUrl;
    [ObservableProperty] private string? _centralApiKey;
    [ObservableProperty] private string? _comPort;
    [ObservableProperty] private string? _baudrate;
    [ObservableProperty] private string? _parserType;
    [ObservableProperty] private string? _frameEndChar;
    [ObservableProperty] private bool _useSimulator;
    [ObservableProperty] private string? _weightSubstringStart;
    [ObservableProperty] private string? _weightSubstringLength;
    [ObservableProperty] private ObservableCollection<Vehicle> _vehicles = new();
    [ObservableProperty] private ObservableCollection<Customer> _customers = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new();

    private async Task LoadMasterDataAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        var vList = await vehicleRepo.SearchAsync(null, CancellationToken.None);
        Vehicles = new ObservableCollection<Vehicle>(vList);

        var cList = await customerRepo.SearchAsync(null, CancellationToken.None);
        Customers = new ObservableCollection<Customer>(cList);

        var pList = await productRepo.SearchAsync(null, CancellationToken.None);
        Products = new ObservableCollection<Product>(pList);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        if (StationCode != null) await repo.SetValueAsync("station_code", StationCode, CancellationToken.None);
        if (TicketPrefix != null) await repo.SetValueAsync("ticket_prefix", TicketPrefix, CancellationToken.None);
        if (ToleranceKg != null) await repo.SetValueAsync("tolerance_kg", ToleranceKg, CancellationToken.None);
        if (SyncInterval != null) await repo.SetValueAsync("sync_interval", SyncInterval, CancellationToken.None);
        if (CentralApiUrl != null) await repo.SetValueAsync("central_api_url", CentralApiUrl, CancellationToken.None);
        if (CentralApiKey != null) await repo.SetValueAsync("central_api_key", CentralApiKey, CancellationToken.None);
        if (ComPort != null) await repo.SetValueAsync("device_com_port", ComPort, CancellationToken.None);
        if (Baudrate != null) await repo.SetValueAsync("device_baudrate", Baudrate, CancellationToken.None);
        if (ParserType != null) await repo.SetValueAsync("device_parser_type", ParserType, CancellationToken.None);
        if (FrameEndChar != null) await repo.SetValueAsync("device_frame_end_char", FrameEndChar, CancellationToken.None);
        if (WeightSubstringStart != null) await repo.SetValueAsync("weight_substring_start", WeightSubstringStart, CancellationToken.None);
        if (WeightSubstringLength != null) await repo.SetValueAsync("weight_substring_length", WeightSubstringLength, CancellationToken.None);

        await repo.SetValueAsync("device_use_simulator", "false", CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        try
        {
            var parser = scope.ServiceProvider.GetService<IWeightFrameParser>() as YaohuaWeightFrameParser;
            if (parser != null)
            {
                if (int.TryParse(WeightSubstringStart, out var startVal))
                {
                    parser.WeightSubstringStart = startVal;
                }
                else
                {
                    parser.WeightSubstringStart = null;
                }

                if (int.TryParse(WeightSubstringLength, out var lenVal))
                {
                    parser.WeightSubstringLength = lenVal;
                }
                else
                {
                    parser.WeightSubstringLength = null;
                }
            }
        }
        catch
        {
        }
    }
}
