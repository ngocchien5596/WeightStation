using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Services;
using StationApp.UI.Helpers;
using System.Threading;
using System.Threading.Tasks;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed record EditWeighingSessionVehicleResult(
    Guid NewVehicleId,
    string NewVehiclePlate,
    string Reason
);

public sealed partial class EditWeighingSessionVehicleViewModel : ObservableObject
{
    private readonly IAutocompleteService _autocompleteService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IClock _clock;

    [ObservableProperty] private string _title = "Chỉnh sửa biển số xe lượt cân";
    [ObservableProperty] private string _sessionNo;
    [ObservableProperty] private string _weighingMode;
    [ObservableProperty] private decimal _weight1;
    [ObservableProperty] private decimal? _weight2;
    [ObservableProperty] private decimal? _netWeight;
    [ObservableProperty] private string _oldVehiclePlate;
    [ObservableProperty] private decimal? _oldStandardTare;

    [ObservableProperty] private AutocompleteInputViewModel _vehiclePlateInput;
    [ObservableProperty] private string? _selectedDriverName;
    [ObservableProperty] private decimal? _newStandardTareWeight;
    [ObservableProperty] private decimal? _newNetWeight;
    [ObservableProperty] private string _reason = string.Empty;

    partial void OnReasonChanged(string value)
    {
        SaveCommand?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedVehicleChanged(Vehicle? value)
    {
        SaveCommand?.NotifyCanExecuteChanged();
    }

    public Guid SessionId { get; }

    [ObservableProperty]
    private Vehicle? _selectedVehicle;

    public EditWeighingSessionVehicleResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public EditWeighingSessionVehicleViewModel(
        Guid sessionId,
        string sessionNo,
        string weighingMode,
        decimal weight1,
        decimal? weight2,
        decimal? netWeight,
        string oldVehiclePlate,
        decimal? oldStandardTare,
        IAutocompleteService autocompleteService,
        IVehicleRepository vehicleRepository,
        IClock clock)
    {
        SessionId = sessionId;
        SessionNo = sessionNo;
        WeighingMode = weighingMode;
        Weight1 = weight1;
        Weight2 = weight2;
        NetWeight = netWeight;
        OldVehiclePlate = oldVehiclePlate;
        OldStandardTare = oldStandardTare;
        _autocompleteService = autocompleteService;
        _vehicleRepository = vehicleRepository;
        _clock = clock;

        _vehiclePlateInput = new AutocompleteInputViewModel(
            SearchVehiclesAsync,
            OnVehicleSelected,
            minimumPrefixLength: 1);
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchVehiclesAsync(string keyword, CancellationToken ct)
    {
        var results = await _autocompleteService.SearchAsync(new AutocompleteQuery(AutocompleteFieldType.Vehicle, keyword), ct);
        var internalVehicles = new List<Vehicle>();

        foreach (var item in results)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                var vehicles = await _vehicleRepository.GetByPlateAsync(item.Value, ct);
                var internalVeh = vehicles.FirstOrDefault(v => v.IsInternalVehicle);
                if (internalVeh != null)
                {
                    internalVehicles.Add(internalVeh);
                }
            }
        }

        var todayLocal = _clock.TodayLocal;
        return internalVehicles
            .Select(v => new AutocompleteItem(
                v.VehiclePlate,
                $"{v.VehiclePlate}{(!string.IsNullOrWhiteSpace(v.DriverName) ? $" - {v.DriverName}" : "")}",
                StandardTarePolicy.GetEffectiveStandardTare(v, todayLocal)?.ToString("N0"),
                AutocompleteFieldType.Vehicle,
                new AutocompletePayload
                {
                    VehiclePlate = v.VehiclePlate,
                    DriverName = v.DriverName,
                    TtcpWeight = StandardTarePolicy.GetEffectiveStandardTare(v, todayLocal)
                }))
            .ToList();
    }

    private async void OnVehicleSelected(AutocompleteItem item)
    {
        if (item.Payload != null)
        {
            SelectedDriverName = item.Payload.DriverName;
            NewStandardTareWeight = item.Payload.TtcpWeight;

            // Load full vehicle entity
            try
            {
                var vehicles = await _vehicleRepository.GetByPlateAsync(item.Value, CancellationToken.None);
                var matched = vehicles.FirstOrDefault(v => v.IsInternalVehicle);
                if (matched != null)
                {
                    SelectedVehicle = matched;
                }
            }
            catch
            {
                // Ignore load error
            }

            RecalculateNetWeight();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private void RecalculateNetWeight()
    {
        if (string.Equals(WeighingMode, "SINGLE_WITH_STANDARD_TARE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(WeighingMode, "1", StringComparison.OrdinalIgnoreCase))
        {
            if (NewStandardTareWeight.HasValue)
            {
                var roundedWeight1 = decimal.Round(Weight1, 3, MidpointRounding.AwayFromZero);
                NewNetWeight = Math.Max(0, roundedWeight1 - NewStandardTareWeight.Value);
            }
            else
            {
                NewNetWeight = null;
            }
        }
        else
        {
            // TWO_WEIGH mode
            if (Weight2.HasValue)
            {
                NewNetWeight = Math.Abs(Weight2.Value - Weight1);
            }
            else
            {
                NewNetWeight = null;
            }
        }
    }

    [ObservableProperty] private string? _validationMessage;

    private bool CanSave() =>
        SelectedVehicle != null &&
        !string.IsNullOrWhiteSpace(Reason);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidationMessage = null;

        var inputPlate = VehiclePlateInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(inputPlate))
        {
            ValidationMessage = "Vui lòng nhập biển số xe mới.";
            return;
        }

        // Try loading the vehicle if not loaded or if plate changed
        if (SelectedVehicle == null || !string.Equals(SelectedVehicle.VehiclePlate, inputPlate, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var vehicles = await _vehicleRepository.GetByPlateAsync(inputPlate, CancellationToken.None);
                var matched = vehicles.FirstOrDefault(v => v.IsInternalVehicle);
                if (matched != null)
                {
                    SelectedVehicle = matched;
                    NewStandardTareWeight = StandardTarePolicy.GetEffectiveStandardTare(matched, _clock.TodayLocal);
                    SelectedDriverName = matched.DriverName;
                    RecalculateNetWeight();
                }
                else
                {
                    ValidationMessage = "Biển số xe mới không tồn tại hoặc không phải là xe nội bộ.";
                    return;
                }
            }
            catch (Exception)
            {
                ValidationMessage = "Không thể kiểm tra thông tin xe mới.";
                return;
            }
        }

        if (string.Equals(SelectedVehicle.VehiclePlate, OldVehiclePlate, StringComparison.OrdinalIgnoreCase))
        {
            ValidationMessage = "Biển số xe mới phải khác biển số xe cũ.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ValidationMessage = "Vui lòng nhập lý do sửa đổi.";
            return;
        }

        // Additional validation for single weigh mode
        if ((string.Equals(WeighingMode, "SINGLE_WITH_STANDARD_TARE", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(WeighingMode, "1", StringComparison.OrdinalIgnoreCase)) && !NewStandardTareWeight.HasValue)
        {
            ValidationMessage = "Xe mới chưa có cấu hình trọng lượng xe chuẩn.";
            return;
        }

        DialogResultValue = new EditWeighingSessionVehicleResult(
            SelectedVehicle.Id,
            SelectedVehicle.VehiclePlate,
            Reason.Trim()
        );

        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }
}
