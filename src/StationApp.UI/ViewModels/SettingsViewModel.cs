using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Device.Abstractions;
using StationApp.Device.Implementations;

namespace StationApp.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public ViewModels.Settings.SystemSettingsViewModel SystemSettingsVM { get; }
    public ViewModels.Settings.ScaleDeviceConfigViewModel ScaleDeviceConfigVM { get; }
    public ViewModels.Settings.VehicleMasterViewModel VehicleMasterVM { get; }
    public ViewModels.Settings.CustomerMasterViewModel CustomerMasterVM { get; }
    public ViewModels.Settings.ProductMasterViewModel ProductMasterVM { get; }
    public ViewModels.Settings.SyncInfoViewModel SyncInfoVM { get; }
    public ViewModels.Settings.AccountManagementViewModel AccountManagementVM { get; }

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _viewTitle = "THAM SỐ HỆ THỐNG";

    partial void OnSelectedTabIndexChanged(int value)
    {
        // Trigger automated sub-view loads safely
        _ = HandleTabSelectionAsync(value);
    }

    private async Task HandleTabSelectionAsync(int tabIndex)
    {
        ViewTitle = tabIndex switch
        {
            0 => "THAM SỐ HỆ THỐNG",
            1 => "THIẾT BỊ CÂN",
            2 => "MASTER XE",
            3 => "KHÁCH HÀNG",
            4 => "SẢN PHẨM",
            5 => "ĐỒNG BỘ",
            6 => "QUẢN LÝ TÀI KHOẢN",
            _ => "CẤU HÌNH HỆ THỐNG"
        };

        try
        {
            switch (tabIndex)
            {
                case 0: await SystemSettingsVM.LoadAsync(); break;
                case 1: await ScaleDeviceConfigVM.LoadAsync(); break;
                case 2: await VehicleMasterVM.LoadAsync(); break;
                case 3: await CustomerMasterVM.LoadAsync(); break;
                case 4: await ProductMasterVM.LoadAsync(); break;
                case 5: await SyncInfoVM.LoadAsync(); break;
                case 6: await AccountManagementVM.LoadAsync(); break;
            }
        }
        catch { /* Robust error containment */ }
    }
    // Tham số hệ thống
    [ObservableProperty] private string? _stationCode;
    [ObservableProperty] private string? _ticketPrefix;
    [ObservableProperty] private string? _toleranceKg;
    [ObservableProperty] private string? _syncInterval;
    [ObservableProperty] private string? _centralApiUrl;
    [ObservableProperty] private string? _centralApiKey;

    // Thiết bị cân
    [ObservableProperty] private string? _comPort;
    [ObservableProperty] private string? _baudrate;
    [ObservableProperty] private string? _parserType;
    [ObservableProperty] private string? _frameEndChar;
    [ObservableProperty] private bool _useSimulator;
    [ObservableProperty] private string? _weightSubstringStart;
    [ObservableProperty] private string? _weightSubstringLength;

    // Master Data
    [ObservableProperty] private ObservableCollection<Vehicle> _vehicles = new();
    [ObservableProperty] private ObservableCollection<Customer> _customers = new();
    [ObservableProperty] private ObservableCollection<Product> _products = new();

    public SettingsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        SystemSettingsVM = new Settings.SystemSettingsViewModel(_scopeFactory);
        ScaleDeviceConfigVM = new Settings.ScaleDeviceConfigViewModel(_scopeFactory);
        VehicleMasterVM = new Settings.VehicleMasterViewModel(_scopeFactory);
        CustomerMasterVM = new Settings.CustomerMasterViewModel(_scopeFactory);
        ProductMasterVM = new Settings.ProductMasterViewModel(_scopeFactory);
        SyncInfoVM = new Settings.SyncInfoViewModel(_scopeFactory);
        AccountManagementVM = new Settings.AccountManagementViewModel(_scopeFactory);
    }

    public async Task LoadAsync()
    {
        await SystemSettingsVM.LoadAsync();
        using var scope = _scopeFactory.CreateScope();
        var appRepo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        
        StationCode = await appRepo.GetValueAsync("station_code", CancellationToken.None);
        TicketPrefix = await appRepo.GetValueAsync("ticket_prefix", CancellationToken.None);
        ToleranceKg = await appRepo.GetValueAsync("tolerance_kg", CancellationToken.None);
        SyncInterval = await appRepo.GetValueAsync("sync_interval", CancellationToken.None);
        CentralApiUrl = await appRepo.GetValueAsync("central_api_url", CancellationToken.None);
        CentralApiKey = await appRepo.GetValueAsync("central_api_key", CancellationToken.None);

        ComPort = await appRepo.GetValueAsync("device_com_port", CancellationToken.None) ?? "COM6";
        Baudrate = await appRepo.GetValueAsync("device_baudrate", CancellationToken.None) ?? "9600";
        ParserType = await appRepo.GetValueAsync("device_parser_type", CancellationToken.None) ?? "DEFAULT";
        FrameEndChar = await appRepo.GetValueAsync("device_frame_end_char", CancellationToken.None) ?? "3";
        WeightSubstringStart = await appRepo.GetValueAsync("weight_substring_start", CancellationToken.None) ?? "0";
        WeightSubstringLength = await appRepo.GetValueAsync("weight_substring_length", CancellationToken.None) ?? "7";

        UseSimulator = false; // Plan A: Hardcoded to false

        await LoadMasterDataAsync();
    }

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

        // Update live parser instance parameters
        try
        {
            var parser = scope.ServiceProvider.GetService<IWeightFrameParser>() as YaohuaWeightFrameParser;
            if (parser != null)
            {
                if (int.TryParse(WeightSubstringStart, out var startVal)) parser.WeightSubstringStart = startVal;
                else parser.WeightSubstringStart = null;

                if (int.TryParse(WeightSubstringLength, out var lenVal)) parser.WeightSubstringLength = lenVal;
                else parser.WeightSubstringLength = null;
            }
        }
        catch { /* Swallow */ }
    }
}
