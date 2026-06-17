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
    [ObservableProperty] private bool _showMenuExportWeighing = true;
    [ObservableProperty] private bool _showMenuOutgoingVehicleList = true;
    [ObservableProperty] private bool _showMenuExportReport = true;
    [ObservableProperty] private bool _showMenuInboundReport = true;
    [ObservableProperty] private DateTime? _createdAt;
    [ObservableProperty] private string? _createdBy;
    [ObservableProperty] private DateTime? _updatedAt;
    [ObservableProperty] private string? _updatedBy;

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
        ShowMenuExportWeighing = value.Features.ShowMenuExportWeighing;
        ShowMenuOutgoingVehicleList = value.Features.ShowMenuOutgoingVehicleList;
        ShowMenuExportReport = value.Features.ShowMenuExportReport;
        ShowMenuInboundReport = value.Features.ShowMenuInboundReport;
        CreatedAt = value.CreatedAt;
        CreatedBy = value.CreatedBy;
        UpdatedAt = value.UpdatedAt;
        UpdatedBy = value.UpdatedBy;
        RaiseModeStateChanged();
    }

    partial void OnEditStationIdChanged(Guid? value)
    {
        RaiseModeStateChanged();
    }

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
        ShowMenuExportWeighing = true;
        ShowMenuOutgoingVehicleList = true;
        ShowMenuExportReport = true;
        ShowMenuInboundReport = true;
        CreatedAt = null;
        CreatedBy = null;
        UpdatedAt = null;
        UpdatedBy = null;
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
                BuildFeatureSet()), CancellationToken.None);

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
            ShowMenuExportWeighing,
            ShowMenuOutgoingVehicleList,
            ShowMenuExportReport,
            ShowMenuInboundReport,
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
        if (ShowMenuExportWeighing) return "ExportWeighing";
        if (ShowMenuOutgoingVehicleList) return "OutgoingVehicles";
        if (ShowMenuInboundReport) return "Reports_InboundSummary";
        if (ShowMenuExportReport) return "Reports_ExportSummary";
        return "Settings";
    }

    private void RaiseModeStateChanged()
    {
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsStationCodeReadOnly));
    }
}
