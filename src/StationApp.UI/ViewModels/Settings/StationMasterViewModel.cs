using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.UI.Services;
using StationApp.UI.ViewModels.Messages;

namespace StationApp.UI.ViewModels.Settings;

public partial class StationMasterViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<string> StatusFilterOptions { get; } = new(["Tất cả", "Đang hoạt động", "Ngừng hoạt động"]);

    [ObservableProperty] private string _searchStationCode = string.Empty;
    [ObservableProperty] private string _searchStationName = string.Empty;
    [ObservableProperty] private string _selectedStatusFilter = "Tất cả";

    [ObservableProperty] private ObservableCollection<StationManagementDto> _stations = new();
    [ObservableProperty] private StationManagementDto? _selectedStation;

    [ObservableProperty] private Guid? _editStationId;
    [ObservableProperty] private string _editStationCode = string.Empty;
    [ObservableProperty] private string _editStationName = string.Empty;
    [ObservableProperty] private int _editSortOrder;
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private bool _showMenuDashboard = true;
    [ObservableProperty] private bool _showMenuIncomingVehicleList = true;
    [ObservableProperty] private bool _showMenuWeighing = true;
    [ObservableProperty] private bool _showMenuCrusherWeighing;
    [ObservableProperty] private bool _showMenuClayWeighing;
    [ObservableProperty] private bool _showMenuExportWeighing = true;
    [ObservableProperty] private bool _showMenuOutgoingVehicleList = true;
    [ObservableProperty] private bool _showMenuExportReport = true;
    [ObservableProperty] private bool _showMenuInboundReport = true;
    [ObservableProperty] private bool _showMenuCrusherInboundReport;
    [ObservableProperty] private bool _showMenuClayInboundReport;
    [ObservableProperty] private DateTime? _createdAt;
    [ObservableProperty] private string? _createdBy;
    [ObservableProperty] private DateTime? _updatedAt;
    [ObservableProperty] private string? _updatedBy;

    // Station Operation Settings: Crusher
    [ObservableProperty] private bool _crusherSingleWeighEnabled;
    [ObservableProperty] private string _crusherDefaultWeighMode = "TWO_WEIGH";
    [ObservableProperty] private string _crusherDefaultProductCode = string.Empty;
    [ObservableProperty] private string _crusherDefaultCustomerCode = string.Empty;

    // Station Operation Settings: Clay
    [ObservableProperty] private bool _claySingleWeighEnabled;
    [ObservableProperty] private string _clayDefaultWeighMode = "TWO_WEIGH";
    [ObservableProperty] private string _clayDefaultProductCode = string.Empty;
    [ObservableProperty] private string _clayDefaultCustomerCode = string.Empty;

    // Dynamic Shared Settings
    [ObservableProperty] private string _activeConfigMode = "NONE";
    [ObservableProperty] private string _operationSettingsHeader = string.Empty;
    [ObservableProperty] private bool _isOperationSettingsVisible;

    [ObservableProperty] private bool _sharedSingleWeighEnabled;
    [ObservableProperty] private string _sharedDefaultWeighMode = "TWO_WEIGH";
    [ObservableProperty] private string _sharedDefaultProductCode = string.Empty;
    [ObservableProperty] private string _sharedDefaultCustomerCode = string.Empty;
    [ObservableProperty] private bool _incomingRequireTtcpForBaggedOutbound;
    [ObservableProperty] private bool _incomingRequireRegistrationForBaggedOutbound;
    [ObservableProperty] private bool _incomingRequireTtcpForBulkOutbound;
    [ObservableProperty] private bool _incomingRequireRegistrationForBulkOutbound;

    private bool _isSyncingSharedProperties;

    public ObservableCollection<WeighModeOption> WeighModeOptions { get; } = new()
    {
        new("TWO_WEIGH", "Cân 2 lần"),
        new("SINGLE_WITH_STANDARD_TARE", "Cân 1 lần")
    };

    [ObservableProperty] private ObservableCollection<ProductAutocompleteSource> _stationProducts = new();
    [ObservableProperty] private ObservableCollection<CustomerAutocompleteSource> _stationCustomers = new();

    public bool IsEditMode => EditStationId.HasValue;
    public bool IsStationCodeReadOnly => IsEditMode;

    public StationMasterViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    partial void OnSelectedStationChanged(StationManagementDto? value)
    {
        if (value == null)
        {
            return;
        }

        EditStationId = value.Id;
        EditStationCode = value.StationCode;
        EditStationName = value.StationName;
        EditSortOrder = value.SortOrder;
        EditIsActive = value.IsActive;
        ShowMenuDashboard = value.Features.ShowMenuDashboard;
        ShowMenuIncomingVehicleList = value.Features.ShowMenuIncomingVehicleList;
        ShowMenuWeighing = value.Features.ShowMenuWeighing;
        ShowMenuCrusherWeighing = value.Features.ShowMenuCrusherWeighing;
        ShowMenuClayWeighing = value.Features.ShowMenuClayWeighing;
        ShowMenuExportWeighing = value.Features.ShowMenuExportWeighing;
        ShowMenuOutgoingVehicleList = value.Features.ShowMenuOutgoingVehicleList;
        ShowMenuExportReport = value.Features.ShowMenuExportReport;
        ShowMenuInboundReport = value.Features.ShowMenuInboundReport;
        ShowMenuCrusherInboundReport = value.Features.ShowMenuCrusherInboundReport;
        ShowMenuClayInboundReport = value.Features.ShowMenuClayInboundReport;
        CreatedAt = value.CreatedAt;
        CreatedBy = value.CreatedBy;
        UpdatedAt = value.UpdatedAt;
        UpdatedBy = value.UpdatedBy;

        _ = LoadStationProductsAndCustomersAndApplySettingsAsync(value);

        RaiseModeStateChanged();
    }

    partial void OnCrusherSingleWeighEnabledChanged(bool value)
    {
        if (!value)
        {
            CrusherDefaultWeighMode = "TWO_WEIGH";
        }
        if (ActiveConfigMode == "CRUSHER" && !_isSyncingSharedProperties)
        {
            SharedSingleWeighEnabled = value;
            if (!value)
            {
                SharedDefaultWeighMode = "TWO_WEIGH";
            }
        }
    }

    partial void OnClaySingleWeighEnabledChanged(bool value)
    {
        if (!value)
        {
            ClayDefaultWeighMode = "TWO_WEIGH";
        }
        if (ActiveConfigMode == "CLAY" && !_isSyncingSharedProperties)
        {
            SharedSingleWeighEnabled = value;
            if (!value)
            {
                SharedDefaultWeighMode = "TWO_WEIGH";
            }
        }
    }

    partial void OnShowMenuCrusherWeighingChanged(bool value)
    {
        UpdateActiveConfigMode();
    }

    partial void OnShowMenuClayWeighingChanged(bool value)
    {
        UpdateActiveConfigMode();
    }

    partial void OnSharedSingleWeighEnabledChanged(bool value)
    {
        if (!value)
        {
            SharedDefaultWeighMode = "TWO_WEIGH";
        }
        if (_isSyncingSharedProperties) return;
        if (ActiveConfigMode == "CRUSHER")
        {
            CrusherSingleWeighEnabled = value;
        }
        else if (ActiveConfigMode == "CLAY")
        {
            ClaySingleWeighEnabled = value;
        }
    }

    partial void OnSharedDefaultWeighModeChanged(string value)
    {
        if (_isSyncingSharedProperties) return;
        if (ActiveConfigMode == "CRUSHER")
        {
            CrusherDefaultWeighMode = value;
        }
        else if (ActiveConfigMode == "CLAY")
        {
            ClayDefaultWeighMode = value;
        }
    }

    partial void OnSharedDefaultProductCodeChanged(string value)
    {
        if (_isSyncingSharedProperties) return;
        if (ActiveConfigMode == "CRUSHER")
        {
            CrusherDefaultProductCode = value;
        }
        else if (ActiveConfigMode == "CLAY")
        {
            ClayDefaultProductCode = value;
        }
    }

    partial void OnSharedDefaultCustomerCodeChanged(string value)
    {
        if (_isSyncingSharedProperties) return;
        if (ActiveConfigMode == "CRUSHER")
        {
            CrusherDefaultCustomerCode = value;
        }
        else if (ActiveConfigMode == "CLAY")
        {
            ClayDefaultCustomerCode = value;
        }
    }

    private void UpdateActiveConfigMode()
    {
        if (ShowMenuCrusherWeighing)
        {
            ActiveConfigMode = "CRUSHER";
        }
        else if (ShowMenuClayWeighing)
        {
            ActiveConfigMode = "CLAY";
        }
        else
        {
            ActiveConfigMode = "NONE";
        }

        IsOperationSettingsVisible = ActiveConfigMode != "NONE";
        OperationSettingsHeader = ActiveConfigMode switch
        {
            "CRUSHER" => "Cấu hình vận hành (Cân trạm đập)",
            "CLAY" => "Cấu hình vận hành (Cân mỏ sét)",
            _ => string.Empty
        };

        _isSyncingSharedProperties = true;
        try
        {
            if (ActiveConfigMode == "CRUSHER")
            {
                SharedSingleWeighEnabled = CrusherSingleWeighEnabled;
                SharedDefaultWeighMode = CrusherDefaultWeighMode;
                SharedDefaultProductCode = CrusherDefaultProductCode;
                SharedDefaultCustomerCode = CrusherDefaultCustomerCode;
            }
            else if (ActiveConfigMode == "CLAY")
            {
                SharedSingleWeighEnabled = ClaySingleWeighEnabled;
                SharedDefaultWeighMode = ClayDefaultWeighMode;
                SharedDefaultProductCode = ClayDefaultProductCode;
                SharedDefaultCustomerCode = ClayDefaultCustomerCode;
            }
        }
        finally
        {
            _isSyncingSharedProperties = false;
        }
    }

    partial void OnEditStationIdChanged(Guid? value)
    {
        RaiseModeStateChanged();
    }

    partial void OnEditStationCodeChanged(string value)
    {
        OnPropertyChanged(nameof(IsIncomingComplianceSettingsVisible));
    }

    public bool IsIncomingComplianceSettingsVisible
        => string.Equals(EditStationCode?.Trim(), "QN01", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync()
    {
        await SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStationAdministrationService>();
        var records = await service.SearchStationsAsync(
            SearchStationCode,
            SearchStationName,
            ResolveActiveFilter(),
            CancellationToken.None);

        Stations = new ObservableCollection<StationManagementDto>(records);
        if (SelectedStation != null)
        {
            SelectedStation = Stations.FirstOrDefault(x => x.Id == SelectedStation.Id);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        SearchStationCode = string.Empty;
        SearchStationName = string.Empty;
        SelectedStatusFilter = StatusFilterOptions[0];
        ResetForm();
        await SearchAsync();
    }

    [RelayCommand]
    private void ResetForm()
    {
        SelectedStation = null;
        EditStationId = null;
        EditStationCode = string.Empty;
        EditStationName = string.Empty;
        EditSortOrder = 0;
        EditIsActive = true;
        ShowMenuDashboard = true;
        ShowMenuIncomingVehicleList = true;
        ShowMenuWeighing = true;
        ShowMenuCrusherWeighing = false;
        ShowMenuClayWeighing = false;
        ShowMenuExportWeighing = true;
        ShowMenuOutgoingVehicleList = true;
        ShowMenuExportReport = true;
        ShowMenuInboundReport = true;
        ShowMenuCrusherInboundReport = false;
        ShowMenuClayInboundReport = false;
        CreatedAt = null;
        CreatedBy = null;
        UpdatedAt = null;
        UpdatedBy = null;

        CrusherSingleWeighEnabled = false;
        CrusherDefaultWeighMode = "TWO_WEIGH";
        CrusherDefaultProductCode = string.Empty;
        CrusherDefaultCustomerCode = string.Empty;

        ClaySingleWeighEnabled = false;
        ClayDefaultWeighMode = "TWO_WEIGH";
        ClayDefaultProductCode = string.Empty;
        ClayDefaultCustomerCode = string.Empty;
        IncomingRequireTtcpForBaggedOutbound = false;
        IncomingRequireRegistrationForBaggedOutbound = false;
        IncomingRequireTtcpForBulkOutbound = false;
        IncomingRequireRegistrationForBulkOutbound = false;

        StationProducts.Clear();
        StationCustomers.Clear();

        UpdateActiveConfigMode();
        RaiseModeStateChanged();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var toast = scope.ServiceProvider.GetRequiredService<IToastService>();
        var service = scope.ServiceProvider.GetRequiredService<IStationAdministrationService>();

        try
        {
            var saved = await service.SaveStationAsync(new SaveStationRequest(
                EditStationId,
                EditStationCode,
                EditStationName,
                EditIsActive,
                EditSortOrder,
                BuildFeatureSet(),
                BuildOperationSettings()), CancellationToken.None);

            toast.ShowSuccess("Đã lưu danh mục trạm.");
            WeakReferenceMessenger.Default.Send(new StationFeaturesChangedMessage(saved.StationCode));
            await SearchAsync();
            SelectedStation = Stations.FirstOrDefault(x => x.Id == saved.Id);
        }
        catch (InvalidOperationException ex)
        {
            toast.ShowWarning(ex.Message);
        }
        catch
        {
            toast.ShowError("Không thể lưu danh mục trạm. Vui lòng thử lại.");
        }
    }

    private bool? ResolveActiveFilter()
    {
        return SelectedStatusFilter switch
        {
            "Đang hoạt động" => true,
            "Ngừng hoạt động" => false,
            _ => null
        };
    }

    private StationFeatureSetDto BuildFeatureSet()
    {
        var defaultTarget = ResolveDefaultNavigationTarget();
        return new StationFeatureSetDto(
            ShowMenuDashboard,
            ShowMenuIncomingVehicleList,
            ShowMenuWeighing,
            ShowMenuCrusherWeighing,
            ShowMenuClayWeighing,
            ShowMenuExportWeighing,
            ShowMenuOutgoingVehicleList,
            ShowMenuExportReport,
            ShowMenuInboundReport,
            ShowMenuCrusherInboundReport,
            ShowMenuClayInboundReport,
            ShowDashboardInboundKpi: ShowMenuDashboard,
            ShowDashboardOutboundKpi: ShowMenuDashboard,
            defaultTarget);
    }

    private string ResolveDefaultNavigationTarget()
    {
        if (ShowMenuDashboard) return "Dashboard";
        if (ShowMenuIncomingVehicleList) return "IncomingVehicles";
        if (ShowMenuWeighing) return "Weighing";
        if (ShowMenuCrusherWeighing) return "CrusherWeighing";
        if (ShowMenuClayWeighing) return "ClayWeighing";
        if (ShowMenuExportWeighing) return "ExportWeighing";
        if (ShowMenuOutgoingVehicleList) return "OutgoingVehicles";
        if (ShowMenuCrusherInboundReport) return "Reports_CrusherInbound";
        if (ShowMenuClayInboundReport) return "Reports_ClayInbound";
        if (ShowMenuInboundReport) return "Reports_InboundSummary";
        if (ShowMenuExportReport) return "Reports_ExportSummary";
        return "Settings";
    }

    private void RaiseModeStateChanged()
    {
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsStationCodeReadOnly));
    }

    private StationOperationSettingsDto BuildOperationSettings()
    {
        return new StationOperationSettingsDto(
            CrusherSingleWeighEnabled,
            CrusherDefaultWeighMode ?? "TWO_WEIGH",
            CrusherDefaultProductCode ?? "",
            CrusherDefaultCustomerCode ?? "",
            ClaySingleWeighEnabled,
            ClayDefaultWeighMode ?? "TWO_WEIGH",
            ClayDefaultProductCode ?? "",
            ClayDefaultCustomerCode ?? "",
            IncomingRequireTtcpForBaggedOutbound,
            IncomingRequireRegistrationForBaggedOutbound,
            IncomingRequireTtcpForBulkOutbound,
            IncomingRequireRegistrationForBulkOutbound);
    }

    private async Task LoadStationProductsAndCustomersAndApplySettingsAsync(StationManagementDto value)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IStationAdministrationService>();
            
            var products = await service.GetProductsByStationAsync(value.StationCode, CancellationToken.None);
            var customers = await service.GetCustomersByStationAsync(value.StationCode, CancellationToken.None);

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                // 1. Populate Lists
                StationProducts.Clear();
                StationProducts.Add(new ProductAutocompleteSource(string.Empty, "--- Không chọn ---", null, "MASTER"));
                foreach (var p in products)
                {
                    StationProducts.Add(p);
                }

                StationCustomers.Clear();
                StationCustomers.Add(new CustomerAutocompleteSource(string.Empty, "--- Không chọn ---", "MASTER"));
                foreach (var c in customers)
                {
                    StationCustomers.Add(c);
                }

                // 2. Apply Operation Settings (now that items lists are loaded)
                CrusherSingleWeighEnabled = value.Settings.CrusherSingleWeighEnabled;
                CrusherDefaultWeighMode = value.Settings.CrusherDefaultWeighMode ?? "TWO_WEIGH";
                CrusherDefaultProductCode = value.Settings.CrusherDefaultProductCode ?? "";
                CrusherDefaultCustomerCode = value.Settings.CrusherDefaultCustomerCode ?? "";

                ClaySingleWeighEnabled = value.Settings.ClaySingleWeighEnabled;
                ClayDefaultWeighMode = value.Settings.ClayDefaultWeighMode ?? "TWO_WEIGH";
                ClayDefaultProductCode = value.Settings.ClayDefaultProductCode ?? "";
                ClayDefaultCustomerCode = value.Settings.ClayDefaultCustomerCode ?? "";
                IncomingRequireTtcpForBaggedOutbound = value.Settings.IncomingRequireTtcpForBaggedOutbound;
                IncomingRequireRegistrationForBaggedOutbound = value.Settings.IncomingRequireRegistrationForBaggedOutbound;
                IncomingRequireTtcpForBulkOutbound = value.Settings.IncomingRequireTtcpForBulkOutbound;
                IncomingRequireRegistrationForBulkOutbound = value.Settings.IncomingRequireRegistrationForBulkOutbound;

                UpdateActiveConfigMode();
                OnPropertyChanged(nameof(IsIncomingComplianceSettingsVisible));
            });
        }
        catch (Exception)
        {
            // Fail silently
        }
    }
}

public record WeighModeOption(string Code, string DisplayName);
