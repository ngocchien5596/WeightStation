using System.Globalization;
using System.Threading;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.UseCases;
using StationApp.Application.UseCases.MasterData;
using StationApp.Device.Abstractions;
using StationApp.Device.Models;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.UI.Helpers;
using StationApp.UI.Resources;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public partial class CrusherWeighingViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly IToastService _toastService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<CrusherWeighingViewModel>? _logger;
    private readonly DispatcherTimer _scaleUiTimer;
    private readonly object _scaleReadingLock = new();
    private LatestScaleReadingSnapshot? _pendingScaleReading;
    private bool _pendingScaleDeviceConnected;
    private bool _hasStartedDeviceAttach;

    public ObservableCollection<CrusherWeighingModeOption> WeighingModeOptions { get; } = new()
    {
        new(CrusherWeighingModes.TwoWeigh, "Cân 2 lần"),
        new(CrusherWeighingModes.SingleWithStandardTare, "Cân 1 lần")
    };

    public AutocompleteInputViewModel InternalVehiclePlateInput { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    private Vehicle? _selectedVehicle;
    [ObservableProperty] private ObservableCollection<CrusherWeighingSessionListItem> _sessions = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    private CrusherWeighingSessionListItem? _selectedSession;
    [ObservableProperty] private string? _searchVehicle;
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    private string _selectedWeighingMode = CrusherWeighingModes.TwoWeigh;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight2Command))]
    private decimal _currentWeight;
    [ObservableProperty] private bool _isStable;
    [ObservableProperty] private string _currentCaptureMode = AutoModeText;
    [ObservableProperty] private bool _isDeviceConnected;
    [ObservableProperty] private string _stabilityText = "CHƯA ỔN ĐỊNH";
    [ObservableProperty] private string _deviceStatusText = "Chưa kết nối đầu cân";
    [ObservableProperty] private Brush _stabilityBrush = new SolidColorBrush(Colors.Orange);
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private string? _selectedDriverName;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    private string? _standardTareText;
    [ObservableProperty] private string _vehicleSelectionStatusText = "Nhập số xe nội bộ rồi bấm Chọn/Tạo xe.";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    private bool _showUpdateButton;
    [ObservableProperty] private bool _isVehicleFormReadOnly = true;

    private string? _originalDriverName;
    private decimal? _originalStandardTare;
    private decimal? _pendingWeight1;
    private decimal? _pendingWeight2;
    private Guid? _activeCrusherSessionId;
    private bool _pendingWeight1IsStable;
    private bool _pendingWeight2IsStable;
    private WeightMode _pendingWeight1Mode = WeightMode.AUTO;
    private WeightMode _pendingWeight2Mode = WeightMode.AUTO;
    private int _vehicleMasterLookupVersion;
    private const string AutoModeText = "TỰ ĐỘNG";
    private const string ManualModeText = "CÂN TAY";

    public bool IsSingleWeighMode => SelectedWeighingMode == CrusherWeighingModes.SingleWithStandardTare;
    public bool IsTwoWeighMode => SelectedWeighingMode == CrusherWeighingModes.TwoWeigh;
    public bool ShowCaptureWeight2Button => IsTwoWeighMode;
    public string CaptureWeight1ButtonText => IsSingleWeighMode ? "CÂN" : "CÂN LẦN 1";
    public bool IsAutoMode => CurrentCaptureMode == AutoModeText;
    public bool IsManualMode => CurrentCaptureMode == ManualModeText;
    public bool CanUseManualMode => StationAuthorization.CanUseManualWeighing(_currentUserContext.RoleCode);
    public decimal? DisplayWeight1 => _pendingWeight1 ?? SelectedSession?.Weight1;
    public decimal? DisplayWeight2 => IsSingleWeighMode
        ? ParseStandardTare(StandardTareText)
        : _pendingWeight2 ?? SelectedSession?.Weight2;
    public decimal? DisplayNetWeight => CalculateDisplayNetWeight() ?? SelectedSession?.NetWeight;

    public CrusherWeighingViewModel(
        IServiceScopeFactory scopeFactory,
        IScaleDevice scaleDevice,
        IToastService toastService,
        ICurrentUserContext currentUserContext,
        ILogger<CrusherWeighingViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
        _toastService = toastService;
        _currentUserContext = currentUserContext;
        _logger = logger;
        _scaleDevice.WeightReceived += OnWeightReceived;
        _scaleUiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _scaleUiTimer.Tick += OnScaleUiTimerTick;

        InternalVehiclePlateInput = CreateAutocompleteField(AutocompleteFieldType.Vehicle, 1, ApplyVehicleSelection);
        WireTextState(InternalVehiclePlateInput, text =>
        {
            var trimmedText = text?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedText))
            {
                SelectedVehicle = null;
            }
            else if (!string.Equals(SelectedVehicle?.VehiclePlate, trimmedText, StringComparison.OrdinalIgnoreCase))
            {
                SelectedVehicle = null;
                IsVehicleFormReadOnly = false;
                VehicleSelectionStatusText = $"Xe {trimmedText} chưa có trong danh mục. Nhập trọng lượng xe chuẩn rồi bấm Chọn/Tạo xe.";
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                _ = RefreshVehicleMasterInfoAsync();
            }

            TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        });
    }

    public async Task InitializeAsync()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var useCases = scope.ServiceProvider.GetRequiredService<CrusherWeighingUseCases>();
            try
            {
                SelectedWeighingMode = await useCases.GetDefaultWeighingModeAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get default weighing mode, using default TWO_WEIGH");
                SelectedWeighingMode = CrusherWeighingModes.TwoWeigh;
            }
        }

        await LoadSessionsAsync();
        StartDeviceAttachIfNeeded();
    }

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {
        try
        {
            IsLoading = true;
            using var scope = _scopeFactory.CreateScope();
            var useCases = scope.ServiceProvider.GetRequiredService<CrusherWeighingUseCases>();
            var keyword = !string.IsNullOrWhiteSpace(SearchSessionNo)
                ? SearchSessionNo
                : SearchVehicle;
            Sessions = new ObservableCollection<CrusherWeighingSessionListItem>(
                await useCases.SearchSessionsAsync(keyword, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load crusher weighing sessions.");
            _toastService.ShowError("Không thể tải danh sách lượt cân trạm đập.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // Clear search fields
        SearchSessionNo = null;
        SearchVehicle = null;

        // Clear selected session (triggers OnSelectedSessionChanged which clears weighing state)
        SelectedSession = null;

        // Clear selected vehicle (triggers OnSelectedVehicleChanged which calls ApplyVehicleInfo)
        SelectedVehicle = null;

        // Clear autocomplete input text
        InternalVehiclePlateInput.Clear();

        // Ensure ShowUpdateButton is cleared
        ShowUpdateButton = false;

        // Reset weighing mode to default
        using (var scope = _scopeFactory.CreateScope())
        {
            var useCases = scope.ServiceProvider.GetRequiredService<CrusherWeighingUseCases>();
            try
            {
                SelectedWeighingMode = await useCases.GetDefaultWeighingModeAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get default weighing mode during refresh, using default TWO_WEIGH");
                SelectedWeighingMode = CrusherWeighingModes.TwoWeigh;
            }
        }

        // Reload sessions list
        await LoadSessionsAsync();
    }

    partial void OnSelectedWeighingModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSingleWeighMode));
        OnPropertyChanged(nameof(IsTwoWeighMode));
        OnPropertyChanged(nameof(ShowCaptureWeight2Button));
        OnPropertyChanged(nameof(CaptureWeight1ButtonText));
        ClearAllWeighingState();
        TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        TakeCrusherWeight2Command.NotifyCanExecuteChanged();
        SaveCrusherWeighingCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentCaptureModeChanged(string value)
    {
        if (value == ManualModeText && !CanUseManualMode)
        {
            CurrentCaptureMode = AutoModeText;
            _toastService.ShowWarning(UiText.Weighing.ManualModeForbidden);
            return;
        }

        OnPropertyChanged(nameof(IsAutoMode));
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(CanUseManualMode));

        if (IsManualMode)
        {
            IsStable = true;
            StabilityText = ManualModeText;
            StabilityBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
        }
    }

    private bool CanTakeCrusherWeight1()
    {
        if (IsLoading || string.IsNullOrWhiteSpace(InternalVehiclePlateInput.Text))
            return false;

        // Không cho phép bắt đầu cân lần 1 nếu:
        // - Đang có active session (đã cân lần 1 nhưng chưa hoàn thành session đang cân)
        // - Session đã hoàn thành hoặc đã hủy (người dùng cần deselect để cân xe khác)
        var sessionStatus = SelectedSession?.SessionStatus;
        if (_activeCrusherSessionId.HasValue
            || sessionStatus == WeighingSessionStatus.COMPLETED
            || sessionStatus == WeighingSessionStatus.CANCELLED
            || sessionStatus == WeighingSessionStatus.PENDING_WEIGHT2)
        {
            return false;
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanTakeCrusherWeight1))]
    private async Task TakeCrusherWeight1Async()
    {
        var vehicle = await EnsureInternalVehicleForWeighingAsync();
        if (vehicle == null)
            return;

        if (SelectedVehicle == null)
        {
            _toastService.ShowWarning("Vui lòng chọn xe nội bộ trước khi cân.");
            return;
        }

        if (CurrentWeight <= 0)
        {
            _toastService.ShowWarning("Số cân phải lớn hơn 0.");
            return;
        }

        // Clear all state when starting a new weighing
        ClearAllWeighingState();
        SelectedSession = null;

        _pendingWeight1 = CurrentWeight;
        _pendingWeight1IsStable = IsStable;
        _pendingWeight1Mode = IsManualMode ? WeightMode.MANUAL : WeightMode.AUTO;

        RefreshCapturedWeightState();
        _toastService.ShowSuccess(IsSingleWeighMode ? "Đã lấy số cân." : "Đã lấy số cân lần 1.");
    }

    private bool CanTakeCrusherWeight2()
{
    if (IsLoading || !IsTwoWeighMode)
        return false;

    // Option 1: Đang cân mới (có pending weight 1, chưa có active session)
    if (_pendingWeight1.HasValue && !_activeCrusherSessionId.HasValue)
        return true;

    // Option 2: Đã có session đang chờ cân lần 2 (active session và session status là PENDING_WEIGHT2)
    if (_activeCrusherSessionId.HasValue
        && SelectedSession?.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2)
        return true;

    return false;
}

    [RelayCommand(CanExecute = nameof(CanTakeCrusherWeight2))]
    private void TakeCrusherWeight2()
    {
        if (_pendingWeight1 is null && !_activeCrusherSessionId.HasValue)
        {
            _toastService.ShowWarning("Vui lòng cân lần 1 trước khi cân lần 2.");
            return;
        }

        if (CurrentWeight <= 0)
        {
            _toastService.ShowWarning("Số cân lần 2 phải lớn hơn 0.");
            return;
        }

        _pendingWeight2 = CurrentWeight;
        _pendingWeight2IsStable = IsStable;
        _pendingWeight2Mode = IsManualMode ? WeightMode.MANUAL : WeightMode.AUTO;

        RefreshCapturedWeightState();
        _toastService.ShowSuccess("Đã lấy số cân lần 2.");
    }

    private bool CanSaveCrusherWeighing()
    {
        if (IsLoading)
            return false;

        // Không cho phép lưu nếu session đã hoàn thành hoặc đã hủy
        var sessionStatus = SelectedSession?.SessionStatus;
        if (sessionStatus == WeighingSessionStatus.COMPLETED
            || sessionStatus == WeighingSessionStatus.CANCELLED)
        {
            return false;
        }

        // Cân 1 lần: cần có pending weight 1 và selected vehicle
        if (IsSingleWeighMode && _pendingWeight1.HasValue && SelectedVehicle != null)
            return true;

        // Cân 2 lần - trường hợp mới (chưa có active session):
        //   - Cần có pending weight 1 và selected vehicle (để tạo session mới với weight1)
        if (IsTwoWeighMode && _pendingWeight1.HasValue && SelectedVehicle != null && !_activeCrusherSessionId.HasValue)
            return true;

        // Cân 2 lần - trường hợp tiếp tục (có active session):
        //   - Cần có pending weight 2 và session đang ở trạng thái PENDING_WEIGHT2
        if (IsTwoWeighMode && _activeCrusherSessionId.HasValue && _pendingWeight2.HasValue
            && SelectedSession?.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2)
            return true;

        return false;
    }

    [RelayCommand(CanExecute = nameof(CanSaveCrusherWeighing))]
    private async Task SaveCrusherWeighingAsync()
    {
        if ((SelectedVehicle == null && !_activeCrusherSessionId.HasValue) || (_pendingWeight1 is null && !_activeCrusherSessionId.HasValue))
        {
            _toastService.ShowWarning("Vui lòng lấy số cân trước khi lưu.");
            return;
        }

        if (IsTwoWeighMode && _activeCrusherSessionId.HasValue && _pendingWeight2 is null)
        {
            _toastService.ShowWarning("Vui lòng lấy đủ cân lần 1 và cân lần 2 trước khi lưu.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCases = scope.ServiceProvider.GetRequiredService<CrusherWeighingUseCases>();
            var sessionId = _activeCrusherSessionId;
            if (sessionId is null)
            {
                sessionId = await useCases.CreateSessionAsync(
                    new CreateCrusherSessionRequest(
                        SelectedVehicle!.Id,
                        SelectedWeighingMode,
                        _pendingWeight1!.Value,
                        _pendingWeight1IsStable,
                        _pendingWeight1Mode),
                    CancellationToken.None);
            }

            if (IsTwoWeighMode && _pendingWeight2.HasValue)
            {
                await useCases.CaptureWeight2Async(
                    new CaptureCrusherWeight2Request(
                        sessionId.Value,
                        _pendingWeight2.Value,
                        _pendingWeight2IsStable,
                        _pendingWeight2Mode),
                    CancellationToken.None);
            }

            _toastService.ShowSuccess("Đã lưu lượt cân trạm đập.");

            // Reload sessions and auto-select the saved session
            await LoadSessionsAsync();
            var savedSession = Sessions.FirstOrDefault(x => x.SessionId == sessionId.Value);
            if (savedSession != null)
            {
                SelectedSession = savedSession;

                // Keep active session if it's still pending weight 2
                if (savedSession.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2)
                {
                    // Clear only pending weights, keep active session
                    _pendingWeight1 = null;
                    _pendingWeight2 = null;
                    _pendingWeight1IsStable = false;
                    _pendingWeight2IsStable = false;
                    _pendingWeight1Mode = WeightMode.AUTO;
                    _pendingWeight2Mode = WeightMode.AUTO;
                    RefreshCapturedWeightState();
                }
                else
                {
                    // Session completed - clear all weighing state
                    ClearAllWeighingState();
                }
            }
            else
            {
                // Should not happen, but clear state if session not found
                ClearAllWeighingState();
            }
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save crusher weighing session.");
            _toastService.ShowError("Không thể lưu lượt cân trạm đập.");
        }
    }

    partial void OnSelectedVehicleChanged(Vehicle? value)
    {
        ApplyVehicleInfo(value);
    }

    partial void OnSelectedSessionChanged(CrusherWeighingSessionListItem? value)
    {
        if (value != null)
        {
            // Clear pending weights but NOT active session yet (will set it based on session status)
            _pendingWeight1 = null;
            _pendingWeight2 = null;
            _pendingWeight1IsStable = false;
            _pendingWeight2IsStable = false;
            _pendingWeight1Mode = WeightMode.AUTO;
            _pendingWeight2Mode = WeightMode.AUTO;

            var isTwoWeighPending = value.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
                && string.Equals(value.WeighingMode, CrusherWeighingModes.TwoWeigh, StringComparison.OrdinalIgnoreCase);

            if (isTwoWeighPending)
            {
                _activeCrusherSessionId = value.SessionId;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
            }
            else if (value.SessionStatus == WeighingSessionStatus.COMPLETED
                || value.SessionStatus == WeighingSessionStatus.CANCELLED)
            {
                // session hoàn tất hoặc đã hủy - chỉ hiển thị thông tin, không cho sửa
                _activeCrusherSessionId = null;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
            }
            else if (value.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1)
            {
                // Session đang chờ cân lần 1 - không set active session (cần cân lại)
                _activeCrusherSessionId = null;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
            }
            else
            {
                // Other statuses (ALLOCATION_PENDING, READY_TO_COMPLETE)
                _activeCrusherSessionId = null;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
            }
        }
        else
        {
            // Deselect session - clear all weighing state
            ClearAllWeighingState();
            OnPropertyChanged(nameof(DisplayWeight1));
            OnPropertyChanged(nameof(DisplayWeight2));
            OnPropertyChanged(nameof(DisplayNetWeight));
            TakeCrusherWeight1Command.NotifyCanExecuteChanged();
            TakeCrusherWeight2Command.NotifyCanExecuteChanged();
            SaveCrusherWeighingCommand.NotifyCanExecuteChanged();
            return;
        }

        RefreshCapturedWeightState();
        OnPropertyChanged(nameof(DisplayWeight1));
        OnPropertyChanged(nameof(DisplayWeight2));
        OnPropertyChanged(nameof(DisplayNetWeight));
        TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        TakeCrusherWeight2Command.NotifyCanExecuteChanged();
        SaveCrusherWeighingCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDriverNameChanged(string? value)
    {
        CheckForChanges();
    }

    partial void OnStandardTareTextChanged(string? value)
    {
        CheckForChanges();
    }

    private decimal? CalculateDisplayNetWeight()
    {
        var weight1 = _pendingWeight1 ?? SelectedSession?.Weight1;
        if (weight1 is null)
            return null;

        if (IsSingleWeighMode)
        {
            var standardTare = ParseStandardTare(StandardTareText);
            return standardTare is > 0 ? Math.Max(0, weight1.Value - standardTare.Value) : null;
        }

        return _pendingWeight2.HasValue ? Math.Abs(_pendingWeight2.Value - weight1.Value) : null;
    }

    private async Task<Vehicle?> EnsureInternalVehicleForWeighingAsync()
    {
        var vehiclePlate = InternalVehiclePlateInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            _toastService.ShowWarning("Vui lòng nhập số xe nội bộ.");
            return null;
        }

        var standardTare = ParseStandardTare(StandardTareText);
        if (standardTare == null && !string.IsNullOrWhiteSpace(StandardTareText))
        {
            _toastService.ShowWarning("Trọng lượng xe chuẩn không đúng định dạng.");
            return null;
        }

        try
        {
            IsLoading = true;
            using var scope = _scopeFactory.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var now = DateTime.Now;
            var vehicle = (await vehicleRepo.GetByPlateAsync(vehiclePlate, CancellationToken.None))
                .FirstOrDefault(v => v.IsInternalVehicle);

            if (vehicle == null)
            {
                if (standardTare is null or <= 0)
                {
                    _toastService.ShowWarning("Xe nội bộ mới bắt buộc nhập TL xe chuẩn lớn hơn 0.");
                    return null;
                }

                vehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    VehiclePlate = vehiclePlate,
                    DriverName = string.IsNullOrWhiteSpace(SelectedDriverName) ? null : SelectedDriverName.Trim(),
                    TtcpWeight = standardTare,
                    IsInternalVehicle = true,
                    StandardTareSource = null,
                    StandardTareUpdatedAt = now,
                    StandardTareUpdatedBy = "Operator",
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "Operator"
                };

                await vehicleRepo.AddAsync(vehicle, CancellationToken.None);
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
                await EnqueueVehicleSyncAsync(scope.ServiceProvider, vehicle, now);
            }
            else
            {
                if (!vehicle.IsActive)
                {
                    _toastService.ShowWarning("Xe nội bộ này đang ngừng sử dụng, không thể cân.");
                    return null;
                }

                var changed = false;
                if (!string.IsNullOrWhiteSpace(SelectedDriverName)
                    && !string.Equals(vehicle.DriverName, SelectedDriverName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    vehicle.DriverName = SelectedDriverName.Trim();
                    changed = true;
                }

                if (standardTare is > 0 && vehicle.TtcpWeight != standardTare)
                {
                    vehicle.TtcpWeight = standardTare;
                    vehicle.StandardTareUpdatedAt = now;
                    vehicle.StandardTareUpdatedBy = "Operator";
                    changed = true;
                }

                if (changed)
                {
                    await vehicleRepo.UpdateAsync(vehicle, CancellationToken.None);
                    await unitOfWork.SaveChangesAsync(CancellationToken.None);
                    await EnqueueVehicleSyncAsync(scope.ServiceProvider, vehicle, now);
                }
            }

            if (IsSingleWeighMode && (!vehicle.TtcpWeight.HasValue || vehicle.TtcpWeight.Value <= 0))
            {
                _toastService.ShowWarning("Xe nội bộ chưa có TL xe chuẩn, không thể cân 1 lần.");
                return null;
            }

            InternalVehiclePlateInput.SetText(vehicle.VehiclePlate);
            SelectedVehicle = vehicle;
            return vehicle;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to ensure crusher internal vehicle {VehiclePlate}.", vehiclePlate);
            _toastService.ShowError("Không thể tạo/cập nhật xe nội bộ.");
            return null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshCapturedWeightState()
    {
        OnPropertyChanged(nameof(DisplayWeight1));
        OnPropertyChanged(nameof(DisplayWeight2));
        OnPropertyChanged(nameof(DisplayNetWeight));
        TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        TakeCrusherWeight2Command.NotifyCanExecuteChanged();
        SaveCrusherWeighingCommand.NotifyCanExecuteChanged();
    }

    private void ClearPendingWeights()
    {
        _pendingWeight1 = null;
        _pendingWeight2 = null;
        _pendingWeight1IsStable = false;
        _pendingWeight2IsStable = false;
        _pendingWeight1Mode = WeightMode.AUTO;
        _pendingWeight2Mode = WeightMode.AUTO;
        // Don't clear _activeCrusherSessionId here - it should be managed separately
        RefreshCapturedWeightState();
    }

    private void ClearAllWeighingState()
    {
        _pendingWeight1 = null;
        _pendingWeight2 = null;
        _pendingWeight1IsStable = false;
        _pendingWeight2IsStable = false;
        _pendingWeight1Mode = WeightMode.AUTO;
        _pendingWeight2Mode = WeightMode.AUTO;
        _activeCrusherSessionId = null;
        RefreshCapturedWeightState();
    }

    private void ApplyVehicleInfo(Vehicle? vehicle)
    {
        if (vehicle != null)
        {
            SelectedDriverName = vehicle.DriverName;
            StandardTareText = vehicle.TtcpWeight?.ToString("N0", CultureInfo.InvariantCulture);
            _originalDriverName = vehicle.DriverName;
            _originalStandardTare = vehicle.TtcpWeight;
            IsVehicleFormReadOnly = false;
            VehicleSelectionStatusText = $"Đã chọn xe nội bộ: {vehicle.VehiclePlate}";
        }
        else
        {
            SelectedDriverName = null;
            StandardTareText = null;
            _originalDriverName = null;
            _originalStandardTare = null;
            IsVehicleFormReadOnly = true;
            VehicleSelectionStatusText = "Chưa chọn xe nội bộ.";
        }

        ShowUpdateButton = false;
    }

    private void CheckForChanges()
    {
        var hasDriverChanged = !string.Equals(SelectedDriverName, _originalDriverName, StringComparison.OrdinalIgnoreCase);
        var hasTareChanged = ParseStandardTare(StandardTareText) != _originalStandardTare;
        ShowUpdateButton = hasDriverChanged || hasTareChanged;
    }

    private decimal? ParseStandardTare(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleanText = text.Replace(",", "").Replace(".", "").Trim();
        if (decimal.TryParse(cleanText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private bool CanConfirmInternalVehicle()
    {
        return !IsLoading && !string.IsNullOrWhiteSpace(InternalVehiclePlateInput.Text);
    }

    [RelayCommand(CanExecute = nameof(CanConfirmInternalVehicle))]
    private async Task ConfirmInternalVehicleAsync()
    {
        var vehiclePlate = InternalVehiclePlateInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            _toastService.ShowWarning("Vui lòng nhập số xe nội bộ.");
            return;
        }

        var standardTare = ParseStandardTare(StandardTareText);
        try
        {
            IsLoading = true;
            using var scope = _scopeFactory.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var now = DateTime.Now;

            var vehicles = await vehicleRepo.GetByPlateAsync(vehiclePlate, CancellationToken.None);
            var vehicle = vehicles.FirstOrDefault(v => v.IsInternalVehicle);
            var created = false;

            if (vehicle == null)
            {
                if (standardTare is null or <= 0)
                {
                    _toastService.ShowWarning("Xe nội bộ mới bắt buộc nhập trọng lượng xe chuẩn lớn hơn 0.");
                    return;
                }

                vehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    VehiclePlate = vehiclePlate,
                    DriverName = SelectedDriverName?.Trim(),
                    TtcpWeight = standardTare,
                    IsInternalVehicle = true,
                    StandardTareSource = null,
                    StandardTareUpdatedAt = now,
                    StandardTareUpdatedBy = "Operator",
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = "Operator"
                };

                await vehicleRepo.AddAsync(vehicle, CancellationToken.None);
                await EnqueueVehicleSyncAsync(scope.ServiceProvider, vehicle, now);
                created = true;
            }

            if (!vehicle.IsActive)
            {
                _toastService.ShowWarning("Xe nội bộ này đang ngừng sử dụng, không thể chọn để cân.");
                SelectedVehicle = null;
                return;
            }

            InternalVehiclePlateInput.SetText(vehicle.VehiclePlate);
            SelectedVehicle = vehicle;
            _toastService.ShowSuccess(created
                ? $"Đã tạo và chọn xe nội bộ {vehicle.VehiclePlate}."
                : $"Đã chọn xe nội bộ {vehicle.VehiclePlate}.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to confirm crusher internal vehicle {VehiclePlate}.", vehiclePlate);
            _toastService.ShowError("Không thể chọn/tạo xe nội bộ.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UpdateVehicleMasterDataAsync()
    {
        if (SelectedVehicle == null)
        {
            _toastService.ShowWarning("Vui lòng chọn xe nội bộ trước khi cập nhật.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            var vehicle = await vehicleRepo.GetByIdAsync(SelectedVehicle.Id, CancellationToken.None);
            if (vehicle == null)
            {
                _toastService.ShowError("Không tìm thấy xe nội bộ trong database.");
                return;
            }

            var newTareWeight = ParseStandardTare(StandardTareText);
            if (newTareWeight == null && !string.IsNullOrWhiteSpace(StandardTareText))
            {
                _toastService.ShowWarning("Trọng lượng xe chuẩn không đúng định dạng.");
                return;
            }

            vehicle.DriverName = SelectedDriverName;
            vehicle.TtcpWeight = newTareWeight;
            vehicle.StandardTareUpdatedAt = DateTime.Now;
            vehicle.StandardTareUpdatedBy = "UI_USER";

            await vehicleRepo.UpdateAsync(vehicle, CancellationToken.None);

            _toastService.ShowSuccess("Đã cập nhật master data xe nội bộ thành công.");

            _originalDriverName = vehicle.DriverName;
            _originalStandardTare = vehicle.TtcpWeight;
            ShowUpdateButton = false;
            VehicleSelectionStatusText = $"Đã chọn xe nội bộ: {vehicle.VehiclePlate}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update vehicle master data.");
            _toastService.ShowError("Không thể cập nhật master data xe nội bộ.");
        }
    }

    private async Task EnqueueVehicleSyncAsync(IServiceProvider serviceProvider, Vehicle vehicle, DateTime now)
    {
        try
        {
            var outboxRepo = serviceProvider.GetRequiredService<ISyncOutboxRepository>();
            var payloadFactory = serviceProvider.GetRequiredService<ISyncPayloadFactory>();
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
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enqueue crusher internal vehicle sync for {VehiclePlate}.", vehicle.VehiclePlate);
        }
    }

    private async Task RefreshVehicleMasterInfoAsync()
    {
        var lookupVersion = Interlocked.Increment(ref _vehicleMasterLookupVersion);
        var vehiclePlate = InternalVehiclePlateInput.Text?.Trim();

        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            if (lookupVersion == Volatile.Read(ref _vehicleMasterLookupVersion))
            {
                SelectedVehicle = null;
            }

            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            var vehicle = (await vehicleRepo.GetByPlateAsync(vehiclePlate, CancellationToken.None))
                .FirstOrDefault(v => v.IsInternalVehicle);

            if (lookupVersion == Volatile.Read(ref _vehicleMasterLookupVersion))
            {
                SelectedVehicle = vehicle;
                if (vehicle == null)
                {
                    IsVehicleFormReadOnly = false;
                    VehicleSelectionStatusText = $"Xe {vehiclePlate} chưa có trong danh mục. Nhập trọng lượng xe chuẩn rồi bấm Chọn/Tạo xe.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Load crusher vehicle master info failed for plate {VehiclePlate}", vehiclePlate);
            if (lookupVersion == Volatile.Read(ref _vehicleMasterLookupVersion))
            {
                SelectedVehicle = null;
            }
        }
    }

    private void ApplyVehicleSelection(AutocompleteItem item)
    {
        InternalVehiclePlateInput.SetText(item.Value);
        _ = RefreshVehicleMasterInfoAsync();
    }

    private AutocompleteInputViewModel CreateAutocompleteField(
        AutocompleteFieldType fieldType,
        int minimumPrefixLength,
        Action<AutocompleteItem> onSelected)
    {
        return new AutocompleteInputViewModel(
            (keyword, ct) => SearchAutocompleteAsync(fieldType, keyword, ct),
            onSelected,
            minimumPrefixLength);
    }

    private async Task<IReadOnlyList<AutocompleteItem>> SearchAutocompleteAsync(
        AutocompleteFieldType fieldType,
        string keyword,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutocompleteService>();
        var results = await service.SearchAsync(new AutocompleteQuery(fieldType, keyword), ct);

        if (fieldType == AutocompleteFieldType.Vehicle)
        {
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var internalVehicles = new List<Vehicle>();

            foreach (var item in results)
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    var vehicles = await vehicleRepo.GetByPlateAsync(item.Value, ct);
                    var internalVeh = vehicles.FirstOrDefault(v => v.IsInternalVehicle);
                    if (internalVeh != null)
                    {
                        internalVehicles.Add(internalVeh);
                    }
                }
            }

            return internalVehicles
                .Select(v => new AutocompleteItem(
                    v.VehiclePlate,
                    $"{v.VehiclePlate}{(!string.IsNullOrWhiteSpace(v.DriverName) ? $" - {v.DriverName}" : "")}",
                    v.TtcpWeight?.ToString("N0"),
                    AutocompleteFieldType.Vehicle,
                    new AutocompletePayload
                    {
                        VehiclePlate = v.VehiclePlate,
                        DriverName = v.DriverName,
                        TtcpWeight = v.TtcpWeight
                    }))
                .ToList();
        }

        return results;
    }

    private static void WireTextState(AutocompleteInputViewModel state, Action<string?> onChanged)
    {
        state.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AutocompleteInputViewModel.Text))
            {
                onChanged(state.Text);
            }
        };
    }

    private void StartDeviceAttachIfNeeded()
    {
        if (_hasStartedDeviceAttach)
        {
            return;
        }

        _hasStartedDeviceAttach = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await _scaleDevice.ConnectAsync(CancellationToken.None);
                await _scaleDevice.StartAsync(CancellationToken.None);
                IsDeviceConnected = _scaleDevice.IsConnected;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Crusher weighing scale attach failed.");
                DeviceStatusText = "Mất kết nối đầu cân";
            }
        });
    }

    private void OnWeightReceived(object? sender, ScaleReading reading)
    {
        lock (_scaleReadingLock)
        {
            _pendingScaleReading = new LatestScaleReadingSnapshot
            {
                Weight = reading.Weight,
                IsStable = reading.IsStable,
                ReceivedAt = reading.CapturedAt
            };
            _pendingScaleDeviceConnected = _scaleDevice.IsConnected;
        }

        if (!_scaleUiTimer.IsEnabled)
        {
            _scaleUiTimer.Start();
        }
    }

    private void OnScaleUiTimerTick(object? sender, EventArgs e)
    {
        LatestScaleReadingSnapshot? latest;
        bool connected;
        lock (_scaleReadingLock)
        {
            latest = _pendingScaleReading;
            connected = _pendingScaleDeviceConnected;
            _pendingScaleReading = null;
        }

        if (latest is null)
        {
            return;
        }

        if (IsManualMode)
        {
            IsStable = true;
            IsDeviceConnected = connected;
            StabilityText = ManualModeText;
            StabilityBrush = new SolidColorBrush(Color.FromRgb(46, 213, 115));
            DeviceStatusText = connected ? "Đang kết nối đầu cân" : "Mất kết nối đầu cân";
            return;
        }

        CurrentWeight = latest.Weight;
        IsStable = latest.IsStable;
        IsDeviceConnected = connected;
        StabilityText = latest.IsStable ? "ỔN ĐỊNH" : "CHƯA ỔN ĐỊNH";
        StabilityBrush = latest.IsStable
            ? new SolidColorBrush(Color.FromRgb(46, 213, 115))
            : new SolidColorBrush(Colors.Orange);
        DeviceStatusText = connected ? "Đang kết nối đầu cân" : "Mất kết nối đầu cân";
    }

    public void Dispose()
    {
        _scaleUiTimer.Stop();
        _scaleUiTimer.Tick -= OnScaleUiTimerTick;
        _scaleDevice.WeightReceived -= OnWeightReceived;
    }
}

public sealed record CrusherWeighingModeOption(string Value, string DisplayName)
{
    public override string ToString()
        => Value switch
        {
            CrusherWeighingModes.SingleWithStandardTare => "Cân 1 lần",
            CrusherWeighingModes.TwoWeigh => "Cân 2 lần",
            _ => DisplayName
        };
}
