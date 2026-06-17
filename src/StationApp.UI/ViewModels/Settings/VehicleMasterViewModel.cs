using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings;

public partial class VehicleMasterViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentStationContext _currentStationContext;

    public VehicleMasterViewModel(IServiceScopeFactory scopeFactory, ICurrentStationContext currentStationContext)
    {
        _scopeFactory = scopeFactory;
        _currentStationContext = currentStationContext;
    }

    [ObservableProperty] private string _searchVehiclePlate = string.Empty;
    [ObservableProperty] private string _searchMoocNumber = string.Empty;
    [ObservableProperty] private string _searchDriverName = string.Empty;

    [ObservableProperty] private string _editVehiclePlate = string.Empty;
    [ObservableProperty] private string _editMoocNumber = string.Empty;
    [ObservableProperty] private string _editDriverName = string.Empty;
    [ObservableProperty] private TransportMethod? _editTransportMethod = TransportMethod.ROAD;
    [ObservableProperty] private decimal? _editTtcpWeight;
    [ObservableProperty] private bool _editIsInternalVehicle;
    [ObservableProperty] private string _editVehicleRegistrationNo = string.Empty;
    [ObservableProperty] private DateTime? _editVehicleRegistrationExpiryDate;
    [ObservableProperty] private string _editMoocRegistrationNo = string.Empty;
    [ObservableProperty] private DateTime? _editMoocRegistrationExpiryDate;
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private bool _isInternalVehicleStation;

    [ObservableProperty] private ObservableCollection<Vehicle> _vehicles = new();
    [ObservableProperty] private Vehicle? _selectedVehicle;

    public string TtcpWeightLabel => IsInternalVehicleStation ? "TL xe chuẩn (kg)" : "TTCP (kg)";
    public bool ShowTtcp10Weight => !IsInternalVehicleStation;

    partial void OnSelectedVehicleChanged(Vehicle? value)
    {
        if (value == null)
        {
            return;
        }

        EditVehiclePlate = value.VehiclePlate;
        EditMoocNumber = value.MoocNumber;
        EditDriverName = value.DriverName ?? string.Empty;
        EditTransportMethod = Enum.TryParse<TransportMethod>(value.TransportMethod, out var transportMethod)
            ? transportMethod
            : null;
        EditTtcpWeight = value.TtcpWeight;
        EditIsInternalVehicle = value.IsInternalVehicle;
        EditVehicleRegistrationNo = value.VehicleRegistrationNo ?? string.Empty;
        EditVehicleRegistrationExpiryDate = value.VehicleRegistrationExpiryDate;
        EditMoocRegistrationNo = value.MoocRegistrationNo ?? string.Empty;
        EditMoocRegistrationExpiryDate = value.MoocRegistrationExpiryDate;
        EditIsActive = value.IsActive;
    }

    public async Task LoadAsync()
    {
        await RefreshStationDisplayModeAsync();
        await SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await RefreshStationDisplayModeAsync();

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        var list = await repo.SearchAsync(null, CancellationToken.None);
        var filtered = list.Where(x =>
            MatchesSearch(x.VehiclePlate, SearchVehiclePlate)
            && MatchesSearch(x.MoocNumber, SearchMoocNumber)
            && MatchesSearch(x.DriverName, SearchDriverName));

        Vehicles.Clear();
        foreach (var item in filtered)
        {
            Vehicles.Add(item);
        }
    }

    private static bool MatchesSearch(string? source, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(source)
            && source.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void ResetForm()
    {
        EditVehiclePlate = string.Empty;
        EditMoocNumber = string.Empty;
        EditDriverName = string.Empty;
        EditTransportMethod = TransportMethod.ROAD;
        EditTtcpWeight = null;
        EditIsInternalVehicle = IsInternalVehicleStation;
        EditVehicleRegistrationNo = string.Empty;
        EditVehicleRegistrationExpiryDate = null;
        EditMoocRegistrationNo = string.Empty;
        EditMoocRegistrationExpiryDate = null;
        EditIsActive = true;
        SelectedVehicle = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RefreshStationDisplayModeAsync();

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
        var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

        if (string.IsNullOrWhiteSpace(EditVehiclePlate))
        {
            await dialogService.ShowErrorAsync("Lỗi", "Số PTVC không được rỗng!");
            return;
        }

        if (EditIsInternalVehicle && (!EditTtcpWeight.HasValue || EditTtcpWeight.Value <= 0))
        {
            await dialogService.ShowWarningAsync("Lỗi", $"Xe nội bộ bắt buộc nhập {TtcpWeightLabel.ToLowerInvariant()}.");
            return;
        }

        try
        {
            var existing = await repo.GetByPlateAndMoocAsync(EditVehiclePlate, EditMoocNumber, CancellationToken.None);
            if (existing != null && (SelectedVehicle == null || existing.Id != SelectedVehicle.Id))
            {
                await dialogService.ShowWarningAsync("Lỗi", "Cặp (Biển số, Số Mooc) đã tồn tại trên hệ thống!");
                return;
            }

            if (SelectedVehicle == null)
            {
                var newVehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    VehiclePlate = EditVehiclePlate.Trim(),
                    MoocNumber = EditMoocNumber.Trim(),
                    DriverName = EditDriverName.Trim(),
                    TransportMethod = EditTransportMethod?.ToString(),
                    TtcpWeight = EditTtcpWeight,
                    IsInternalVehicle = EditIsInternalVehicle,
                    StandardTareSource = null,
                    StandardTareUpdatedAt = EditIsInternalVehicle ? clock.NowLocal : null,
                    StandardTareUpdatedBy = EditIsInternalVehicle ? "Operator" : null,
                    VehicleRegistrationNo = EditVehicleRegistrationNo.Trim(),
                    VehicleRegistrationExpiryDate = EditVehicleRegistrationExpiryDate,
                    MoocRegistrationNo = EditMoocRegistrationNo.Trim(),
                    MoocRegistrationExpiryDate = EditMoocRegistrationExpiryDate,
                    IsActive = EditIsActive,
                    CreatedAt = clock.NowLocal,
                    CreatedBy = "Operator"
                };

                await repo.AddAsync(newVehicle, CancellationToken.None);
                await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, newVehicle, clock.NowLocal);
            }
            else
            {
                ApplyVehicleEdits(SelectedVehicle, clock.NowLocal);

                if (existing != null)
                {
                    ApplyVehicleEdits(existing, clock.NowLocal);
                    await repo.UpdateAsync(existing, CancellationToken.None);
                    await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, existing, clock.NowLocal);
                }
                else
                {
                    await repo.UpdateAsync(SelectedVehicle, CancellationToken.None);
                    await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, SelectedVehicle, clock.NowLocal);
                }
            }

            await uow.SaveChangesAsync(CancellationToken.None);
            await dialogService.ShowInfoAsync("Thông báo", "Lưu dữ liệu thành công!");

            ResetForm();
            await SearchAsync();
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi lưu dữ liệu: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        if (SelectedVehicle == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

        var result = await dialogService.ShowConfirmAsync(
            "Xác nhận",
            $"Bạn có chắc muốn ngừng sử dụng phương tiện {SelectedVehicle.VehiclePlate}?",
            "Đồng ý",
            "Bỏ qua");

        if (!result)
        {
            return;
        }

        var repo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
        var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

        SelectedVehicle.IsActive = false;
        await repo.UpdateAsync(SelectedVehicle, CancellationToken.None);
        await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, SelectedVehicle, clock.NowLocal);
        await uow.SaveChangesAsync(CancellationToken.None);

        await dialogService.ShowInfoAsync("Thông báo", "Đã chuyển đổi trạng thái ngừng sử dụng.");
        ResetForm();
        await SearchAsync();
    }

    private void ApplyVehicleEdits(Vehicle vehicle, DateTime now)
    {
        vehicle.VehiclePlate = EditVehiclePlate.Trim();
        vehicle.MoocNumber = EditMoocNumber.Trim();
        vehicle.DriverName = EditDriverName.Trim();
        vehicle.TransportMethod = EditTransportMethod?.ToString();
        vehicle.TtcpWeight = EditTtcpWeight;
        vehicle.IsInternalVehicle = EditIsInternalVehicle;
        vehicle.StandardTareSource = null;
        vehicle.StandardTareUpdatedAt = EditIsInternalVehicle ? now : null;
        vehicle.StandardTareUpdatedBy = EditIsInternalVehicle ? "Operator" : null;
        vehicle.VehicleRegistrationNo = EditVehicleRegistrationNo.Trim();
        vehicle.VehicleRegistrationExpiryDate = EditVehicleRegistrationExpiryDate;
        vehicle.MoocRegistrationNo = EditMoocRegistrationNo.Trim();
        vehicle.MoocRegistrationExpiryDate = EditMoocRegistrationExpiryDate;
        vehicle.IsActive = EditIsActive;
        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = "Operator";
    }

    private async Task RefreshStationDisplayModeAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentStationContext.StationCode))
        {
            SetInternalVehicleStationMode(false);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var stationFeatureService = scope.ServiceProvider.GetRequiredService<IStationFeatureService>();
        var features = await stationFeatureService.GetFeaturesAsync(_currentStationContext.StationCode, CancellationToken.None);
        SetInternalVehicleStationMode(IsCrusherOrClayStation(features));
    }

    private void SetInternalVehicleStationMode(bool value)
    {
        var modeChanged = IsInternalVehicleStation != value;
        IsInternalVehicleStation = value;
        OnPropertyChanged(nameof(TtcpWeightLabel));
        OnPropertyChanged(nameof(ShowTtcp10Weight));

        if (!modeChanged || SelectedVehicle != null)
        {
            return;
        }

        EditIsInternalVehicle = value;
    }

    private static bool IsCrusherOrClayStation(StationFeatureSetDto features)
        => features.ShowMenuCrusherWeighing || features.ShowMenuClayWeighing;

    private static async Task EnqueueMasterSyncAsync(
        ISyncOutboxRepository outboxRepo,
        ISyncPayloadFactory payloadFactory,
        Vehicle vehicle,
        DateTime now)
    {
        await outboxRepo.EnqueueAsync(new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateId = vehicle.Id,
            AggregateType = SyncAggregateTypes.Vehicle,
            PayloadJson = payloadFactory.CreatePayload(vehicle),
            IdempotencyKey = vehicle.Id,
            Status = OutboxStatus.PENDING,
            RetryCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        }, CancellationToken.None);
    }
}
