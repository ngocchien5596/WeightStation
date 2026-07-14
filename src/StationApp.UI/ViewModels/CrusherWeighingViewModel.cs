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
using StationApp.Application.Services;
using StationApp.Application.UseCases;
using StationApp.Application.UseCases.MasterData;
using StationApp.Device.Abstractions;
using StationApp.Device.Models;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Services;
using StationApp.UI.Helpers;
using StationApp.UI.Resources;
using StationApp.UI.Services;
using StationApp.UI.ViewModels.Dialogs;
using StationApp.Application.Printing;
using StationApp.UI.Printing;

namespace StationApp.UI.ViewModels;

public partial class CrusherWeighingViewModel : ObservableObject, IDisposable, IWeighingDeviceHost
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly ICameraPreviewService _cameraPreviewService;
    private readonly IToastService _toastService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICurrentStationContext _currentStationContext;
    private readonly IDialogService _dialogService;
    private readonly ILogger<CrusherWeighingViewModel>? _logger;
    private readonly Dispatcher _uiDispatcher;
    private readonly WeighingDeviceConnector _deviceConnector;

    // Crusher Weighing: Default Product and Customer
    public event Action<string?, string?>? NavigateToEditHistoryRequested;

    private string _defaultProductCode = DefaultProductCode;
    private string _defaultProductName = DefaultProductName;
    private string _defaultCustomerCode = DefaultCustomerCode;
    private string _defaultCustomerName = DefaultCustomerName;

    public ObservableCollection<CrusherWeighingModeOption> WeighingModeOptions { get; } = new()
    {
        new(CrusherWeighingModes.TwoWeigh, "Cân 2 lần"),
        new(CrusherWeighingModes.SingleWithStandardTare, "Cân 1 lần")
    };

    public AutocompleteInputViewModel InternalVehiclePlateInput { get; }

    // Crusher Weighing: Product and Customer Inputs
    public AutocompleteInputViewModel ProductCodeInput { get; }
    public AutocompleteInputViewModel ProductNameInput { get; }
    public AutocompleteInputViewModel CustomerCodeInput { get; }
    public AutocompleteInputViewModel CustomerNameInput { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    private Vehicle? _selectedVehicle;
    [ObservableProperty] private ObservableCollection<CrusherWeighingSessionListItem> _sessions = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintWeighTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditSessionVehicleCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewSessionHistoryCommand))]
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
    [ObservableProperty] private DateTime? _selectedDate = DateTime.Today;

    [ObservableProperty] private string? _selectedDriverName;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    private string? _standardTareText;
    [ObservableProperty] private string _vehicleSelectionStatusText = "Chọn xe nội bộ có trong danh mục xe.";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    private bool _showUpdateButton;
    [ObservableProperty] private bool _isVehicleFormReadOnly = true;
    [ObservableProperty] private string _cameraPreviewStatusText = "Chưa cấu hình camera";
    [ObservableProperty] private ImageSource? _cameraPreviewSource;
    [ObservableProperty] private string _selectedPreviewCameraCode = "CAM1";
    [ObservableProperty] private bool _isCameraPreviewAvailable;
    [ObservableProperty] private bool _isCamera1PreviewAvailable;
    [ObservableProperty] private bool _isCamera2PreviewAvailable;
    [ObservableProperty] private string _camera1PreviewName = "Camera 1";
    [ObservableProperty] private string _camera2PreviewName = "Camera 2";

    private string? _originalDriverName;
    private decimal? _originalStandardTare;
    private decimal? _pendingWeight1;
    private decimal? _pendingWeight2;
    private decimal? _effectiveStandardTareForPendingWeight2;
    private Guid? _activeCrusherSessionId;
    private bool _pendingWeight1IsStable;
    private bool _pendingWeight2IsStable;
    private WeightMode _pendingWeight1Mode = WeightMode.AUTO;
    private WeightMode _pendingWeight2Mode = WeightMode.AUTO;
    private int _vehicleMasterLookupVersion;
    private const string AutoModeText = "TỰ ĐỘNG";
    private const string ManualModeText = "CÂN TAY";

    // Crusher Weighing: Default Product and Customer
    private const string DefaultProductCode = "ĐV";
    private const string DefaultProductName = "Đá vôi";
    private const string DefaultCustomerCode = "NCC1";
    private const string DefaultCustomerName = "Công ty CPXD và SXVLXD";

    public bool IsWeighingReadOnly
    {
        get
        {
            var status = SelectedSession?.SessionStatus;
            return status == WeighingSessionStatus.COMPLETED || status == WeighingSessionStatus.CANCELLED;
        }
    }

    public bool IsCrusherInfoFormReadOnly => HasCapturedWeight1OrLater();
    public bool CanEditCrusherInfoForm => !IsCrusherInfoFormReadOnly;
    public bool IsVehicleDetailsReadOnly => IsVehicleFormReadOnly || IsCrusherInfoFormReadOnly;
    public bool IsSingleWeighMode => SelectedWeighingMode == CrusherWeighingModes.SingleWithStandardTare;
    public bool IsTwoWeighMode => SelectedWeighingMode == CrusherWeighingModes.TwoWeigh;
    public bool ShowCaptureWeight2Button => IsTwoWeighMode;
    public string CaptureWeight1ButtonText => IsSingleWeighMode ? "CÂN" : "CÂN LẦN 1";
    public bool IsAutoMode => CurrentCaptureMode == AutoModeText;
    public bool IsManualMode => CurrentCaptureMode == ManualModeText;
    public bool CanUseManualMode => StationAuthorization.CanUseManualWeighing(_currentUserContext.RoleCode);
    public bool ShowCamera1Selector => IsCameraPreviewAvailable && IsCamera1PreviewAvailable;
    public bool ShowCamera2Selector => IsCameraPreviewAvailable && IsCamera2PreviewAvailable;
    public bool ShowCameraPreviewPlaceholder =>
        !IsCameraPreviewAvailable
        || !_cameraPreviewService.IsPreviewRunning;
    public decimal? DisplayWeight1 => _pendingWeight1 ?? SelectedSession?.Weight1;
    public decimal? DisplayWeight2 => IsSingleWeighMode
        ? ParseStandardTare(StandardTareText)
        : _pendingWeight2 ?? SelectedSession?.Weight2;
    public decimal? DisplayNetWeight => CalculateDisplayNetWeight() ?? SelectedSession?.NetWeight;
    public int ReturnedBrokenTripCount => Sessions.Count(x => x.IsReturnedBrokenTrip);

    public CrusherWeighingViewModel(
        IServiceScopeFactory scopeFactory,
        IScaleDevice scaleDevice,
        ICameraPreviewService cameraPreviewService,
        IToastService toastService,
        ICurrentUserContext currentUserContext,
        ICurrentStationContext currentStationContext,
        IDialogService dialogService,
        ILogger<CrusherWeighingViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
        _cameraPreviewService = cameraPreviewService;
        _toastService = toastService;
        _currentUserContext = currentUserContext;
        _currentStationContext = currentStationContext;
        _dialogService = dialogService;
        _logger = logger;
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        _deviceConnector = new WeighingDeviceConnector(this, scaleDevice, cameraPreviewService, logger);

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
                IsVehicleFormReadOnly = true;
                VehicleSelectionStatusText = $"Xe {trimmedText} chưa có trong danh mục xe nội bộ. Vui lòng tạo xe tại màn Danh mục xe trước khi cân.";
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                _ = RefreshVehicleMasterInfoAsync();
            }

            TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        });

        // Crusher Weighing: Product and Customer input fields
        ProductCodeInput = CreateAutocompleteField(AutocompleteFieldType.ProductCode, 1, OnProductCodeSelected);
        ProductNameInput = CreateAutocompleteField(AutocompleteFieldType.ProductName, 1, OnProductNameSelected);
        CustomerCodeInput = CreateAutocompleteField(AutocompleteFieldType.CustomerCode, 1, OnCustomerCodeSelected);
        CustomerNameInput = CreateAutocompleteField(AutocompleteFieldType.Customer, 1, OnCustomerNameSelected);

        // Set default values for Product and Customer
        SetDefaultProductAndCustomer();
    }

    partial void OnSessionsChanged(ObservableCollection<CrusherWeighingSessionListItem> value)
    {
        OnPropertyChanged(nameof(ReturnedBrokenTripCount));
    }

    public async Task InitializeAsync()
    {
        await LoadDefaultSettingsAsync();

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
        _deviceConnector.StartDeviceAttachIfNeeded();
        await LoadCameraPreviewAsync();
    }

    private async Task LoadDefaultSettingsAsync()
    {
        var stationCode = _currentStationContext.StationCode;
        if (string.IsNullOrWhiteSpace(stationCode))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<IStationOperationSettingsRepository>();
            var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

            var dbProductCode = await settingsRepo.GetValueAsync(stationCode, StationOperationSettingKeys.CrusherDefaultProductCode, CancellationToken.None);
            var dbCustomerCode = await settingsRepo.GetValueAsync(stationCode, StationOperationSettingKeys.CrusherDefaultCustomerCode, CancellationToken.None);

            if (!string.IsNullOrWhiteSpace(dbProductCode))
            {
                var p = await productRepo.GetByCodeAsync(dbProductCode, CancellationToken.None);
                if (p != null)
                {
                    _defaultProductCode = p.ProductCode;
                    _defaultProductName = p.ProductName;
                }
                else
                {
                    _defaultProductCode = dbProductCode;
                    _defaultProductName = string.Empty;
                }
            }

            if (!string.IsNullOrWhiteSpace(dbCustomerCode))
            {
                var c = await customerRepo.GetByCodeAsync(dbCustomerCode, CancellationToken.None);
                if (c != null)
                {
                    _defaultCustomerCode = c.CustomerCode;
                    _defaultCustomerName = c.CustomerName;
                }
                else
                {
                    _defaultCustomerCode = dbCustomerCode;
                    _defaultCustomerName = string.Empty;
                }
            }

            SetDefaultProductAndCustomer();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load default settings for station {StationCode}", stationCode);
        }
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
                await useCases.SearchSessionsAsync(keyword, SelectedDate, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load crusher weighing sessions.");
            _toastService.ShowError("Không thể tải danh sách lượt cân mỏ đá.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleReturnedBrokenTripAsync(CrusherWeighingSessionListItem? session)
    {
        if (session == null || IsLoading)
        {
            return;
        }

        var newState = !session.IsReturnedBrokenTrip;
        var confirmMessage = $"Bỏ đánh dấu hàng hoàn cho lượt cân {session.SessionNo}?\n\nLượt này sẽ được tính lại như lượt nhập bình thường.";

        if (newState)
        {
            using var lookupScope = _scopeFactory.CreateScope();
            var sessionRepo = lookupScope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
            var previousTrip = await sessionRepo.GetPreviousCrusherTripForReturnedAsync(session.SessionId, CancellationToken.None);
            if (previousTrip == null)
            {
                await _dialogService.ShowWarningAsync(
                    "Không đủ dữ liệu đối chiếu",
                    "Không có dữ liệu chuyến xe gần nhất trước đó của xe này. Vui lòng kiểm tra lại.");
                return;
            }

            var actualWeight = ResolveActualNetWeightKg(session);
            var resolution = ReturnedBrokenTripWeightLimiter.Resolve(actualWeight, previousTrip.NetWeightKg);
            confirmMessage = resolution.IsCapped
                ? BuildReturnedBrokenTripCappedConfirmMessage(session.SessionNo, previousTrip, resolution)
                : $"Đánh dấu lượt cân {session.SessionNo} là hàng hoàn?\n\nLượt này sẽ được tính vào KPI Hoàn và trừ khỏi Thực nhập.";
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            newState ? "Xác nhận hàng hoàn" : "Bỏ đánh dấu hàng hoàn",
            confirmMessage,
            "Đồng ý",
            "Hủy");

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsLoading = true;
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<ToggleCrusherReturnedBrokenTripUseCase>();
            await useCase.ExecuteAsync(session.SessionId, newState, CancellationToken.None);

            IsLoading = false;
            await LoadSessionsAsync();
            SelectedSession = Sessions.FirstOrDefault(x => x.SessionId == session.SessionId);
            _toastService.ShowSuccess("Đã cập nhật trạng thái hàng hoàn.");
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Toggle crusher returned broken trip rejected. SessionId={SessionId}", session.SessionId);
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Toggle crusher returned broken trip failed. SessionId={SessionId}", session.SessionId);
            _toastService.ShowError("Không thể cập nhật trạng thái hàng hoàn.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string BuildReturnedBrokenTripCappedConfirmMessage(
        string sessionNo,
        ReturnedBrokenTripPreviousTripInfo previousTrip,
        ReturnedBrokenTripWeightResolution resolution)
    {
        return
            $"Đánh dấu lượt cân {sessionNo} là hàng hoàn?\n\n" +
            $"TL hoàn thực cân: {FormatTon(resolution.ActualWeightTon)} tấn\n" +
            $"TL chuyến gần nhất: {FormatTon(resolution.PreviousTripWeightTon ?? 0m)} tấn\n\n" +
            $"Do TL hoàn thực cân lớn hơn chuyến gần nhất, hệ thống chỉ ghi nhận Hoàn là {FormatTon(resolution.RecognizedWeightTon)} tấn.";
    }

    private static decimal ResolveActualNetWeightKg(CrusherWeighingSessionListItem session)
    {
        if (session.Weight1.HasValue && session.Weight2.HasValue)
        {
            return Math.Abs(session.Weight2.Value - session.Weight1.Value);
        }

        if (session.Weight1.HasValue && session.StandardTareWeightSnapshot.HasValue)
        {
            return Math.Max(0m, session.Weight1.Value - session.StandardTareWeightSnapshot.Value);
        }

        return Math.Max(0m, session.NetWeight ?? 0m);
    }

    private static string FormatTon(decimal value)
    {
        return value.ToString("N3", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // Reset statistics date to today
        SelectedDate = DateTime.Today;

        // Clear search fields
        SearchSessionNo = null;
        SearchVehicle = null;

        // Clear selected session and vehicle explicitly
        SelectedSession = null;
        SelectedVehicle = null;

        // Force clear all weighing state and vehicle details
        ClearAllWeighingState();
        ApplyVehicleInfo(null);

        // Clear autocomplete input text
        InternalVehiclePlateInput.Clear();

        // Crusher Weighing: Reset product and customer to defaults
        SetDefaultProductAndCustomer();

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

    partial void OnSelectedPreviewCameraCodeChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _ = _deviceConnector.StartCameraPreviewAsync(value);
    }

    private async Task LoadCameraPreviewAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<ICameraSettingsProvider>();
            var settings = await provider.GetForStationAsync("CRUSHER", CancellationToken.None);
            _deviceConnector.InitializeCameraPreview(settings);
            _ = _deviceConnector.StartCameraPreviewAsync(SelectedPreviewCameraCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Load camera preview settings failed for crusher weighing");
            IsCameraPreviewAvailable = false;
            IsCamera1PreviewAvailable = false;
            IsCamera2PreviewAvailable = false;
            CameraPreviewStatusText = "Không tải được cấu hình camera";
            OnPropertyChanged(nameof(ShowCamera1Selector));
            OnPropertyChanged(nameof(ShowCamera2Selector));
            OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
        }
    }

    public void RaisePropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }

    partial void OnSelectedDateChanged(DateTime? value)
    {
        _ = LoadSessionsAsync();
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


    }

    private bool CanTakeCrusherWeight1()
    {
        if (IsLoading || SelectedVehicle == null)
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
    private void TakeCrusherWeight1()
    {
        if (SelectedVehicle == null)
        {
            _toastService.ShowWarning("Vui lòng chọn xe nội bộ có trong danh mục xe trước khi cân.");
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

        // Chỉ cho phép cân lần 2 khi đã lưu lần 1 vào DB (có active session và trạng thái là PENDING_WEIGHT2)
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

        if (_effectiveStandardTareForPendingWeight2.HasValue)
        {
            _pendingWeight2 = _effectiveStandardTareForPendingWeight2.Value;
            _pendingWeight2IsStable = true;
            _pendingWeight2Mode = WeightMode.AUTO;

            RefreshCapturedWeightState();
            _toastService.ShowSuccess("Đã lấy TL bì hiệu lực trong ngày làm số cân lần 2.");
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
                        _pendingWeight1Mode,
                        // Crusher Weighing: Product and Customer Information
                        ProductCodeInput.Text?.Trim(),
                        ProductNameInput.Text?.Trim(),
                        CustomerCodeInput.Text?.Trim(),
                        CustomerNameInput.Text?.Trim()),
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

            _toastService.ShowSuccess("Đã lưu lượt cân mỏ đá.");
            await RefreshAsync();
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save crusher weighing session.");
            _toastService.ShowError("Không thể lưu lượt cân mỏ đá.");
        }
    }

    partial void OnSelectedVehicleChanged(Vehicle? value)
    {
        ApplyVehicleInfo(value);
        ApplyVehicleWeighingMode(value);  // Auto chuyển chế độ cân dựa trên TL bì có hiệu lực
    }

    partial void OnSelectedSessionChanged(CrusherWeighingSessionListItem? value)
    {
        if (value != null)
        {
            SelectedWeighingMode = string.Equals(value.WeighingMode, CrusherWeighingModes.SingleWithStandardTare, StringComparison.OrdinalIgnoreCase)
                ? CrusherWeighingModes.SingleWithStandardTare
                : CrusherWeighingModes.TwoWeigh;

            // Clear pending weights but NOT active session yet (will set it based on session status)
            _pendingWeight1 = null;
            _pendingWeight2 = null;
            _effectiveStandardTareForPendingWeight2 = null;
            _pendingWeight1IsStable = false;
            _pendingWeight2IsStable = false;
            _pendingWeight1Mode = WeightMode.AUTO;
            _pendingWeight2Mode = WeightMode.AUTO;

            // Crusher Weighing: Notify read-only state change
            OnPropertyChanged(nameof(IsWeighingReadOnly));
            NotifyCrusherInfoFormStateChanged();

            var isTwoWeighPending = value.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
                && string.Equals(value.WeighingMode, CrusherWeighingModes.TwoWeigh, StringComparison.OrdinalIgnoreCase);

            if (isTwoWeighPending)
            {
                _activeCrusherSessionId = value.SessionId;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
                _ = RefreshEffectiveStandardTareForPendingWeight2Async(value);
                // Crusher Weighing: Set Product and Customer from session
                ProductCodeInput.SetText(value.ProductCode);
                ProductNameInput.SetText(value.ProductName);
                CustomerCodeInput.SetText(value.CustomerCode);
                CustomerNameInput.SetText(value.CustomerName);
            }
            else if (value.SessionStatus == WeighingSessionStatus.COMPLETED
                || value.SessionStatus == WeighingSessionStatus.CANCELLED)
            {
                // session hoàn tất hoặc đã hủy - chỉ hiển thị thông tin, không cho sửa
                _activeCrusherSessionId = null;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
                // Crusher Weighing: Set Product and Customer from session (read-only mode)
                ProductCodeInput.SetText(value.ProductCode);
                ProductNameInput.SetText(value.ProductName);
                CustomerCodeInput.SetText(value.CustomerCode);
                CustomerNameInput.SetText(value.CustomerName);
            }
            else if (value.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1)
            {
                // Session đang chờ cân lần 1 - không set active session (cần cân lại)
                _activeCrusherSessionId = null;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
                // Crusher Weighing: Set Product and Customer from session
                ProductCodeInput.SetText(value.ProductCode);
                ProductNameInput.SetText(value.ProductName);
                CustomerCodeInput.SetText(value.CustomerCode);
                CustomerNameInput.SetText(value.CustomerName);
            }
            else
            {
                // Other statuses (ALLOCATION_PENDING, READY_TO_COMPLETE)
                _activeCrusherSessionId = null;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
                // Crusher Weighing: Set Product and Customer from session
                ProductCodeInput.SetText(value.ProductCode);
                ProductNameInput.SetText(value.ProductName);
                CustomerCodeInput.SetText(value.CustomerCode);
                CustomerNameInput.SetText(value.CustomerName);
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
            PrintWeighTicketCommand.NotifyCanExecuteChanged();
            EditSessionVehicleCommand.NotifyCanExecuteChanged();
            ViewSessionHistoryCommand.NotifyCanExecuteChanged();
            return;
        }

        RefreshCapturedWeightState();
        NotifyCrusherInfoFormStateChanged();
        OnPropertyChanged(nameof(DisplayWeight1));
        OnPropertyChanged(nameof(DisplayWeight2));
        OnPropertyChanged(nameof(DisplayNetWeight));
        TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        TakeCrusherWeight2Command.NotifyCanExecuteChanged();
        SaveCrusherWeighingCommand.NotifyCanExecuteChanged();
        PrintWeighTicketCommand.NotifyCanExecuteChanged();
        EditSessionVehicleCommand.NotifyCanExecuteChanged();
        ViewSessionHistoryCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshEffectiveStandardTareForPendingWeight2Async(CrusherWeighingSessionListItem session)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var vehicles = await vehicleRepo.GetByPlateAsync(session.VehiclePlate, CancellationToken.None);
            var vehicle = vehicles.FirstOrDefault(v => v.IsInternalVehicle && v.IsActive);
            var effectiveStandardTare = StandardTarePolicy.GetEffectiveStandardTare(vehicle, clock.TodayLocal);

            if (SelectedSession?.SessionId != session.SessionId
                || _activeCrusherSessionId != session.SessionId)
            {
                return;
            }

            _effectiveStandardTareForPendingWeight2 = effectiveStandardTare;
            if (effectiveStandardTare.HasValue)
            {
                StandardTareText = effectiveStandardTare.Value.ToString("N0", CultureInfo.InvariantCulture);
                VehicleSelectionStatusText = $"Đã tìm thấy TL bì hiệu lực trong ngày: {effectiveStandardTare.Value:N0} kg. Bấm Cân lần 2 để sử dụng TL bì này.";
            }

            RefreshCapturedWeightState();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to refresh effective standard tare for crusher pending session {SessionId}.", session.SessionId);
        }
    }

    partial void OnSelectedDriverNameChanged(string? value)
    {
        CheckForChanges();
    }

    partial void OnStandardTareTextChanged(string? value)
    {
        CheckForChanges();
    }

    partial void OnIsVehicleFormReadOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsVehicleDetailsReadOnly));
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

    private void RefreshCapturedWeightState()
    {
        OnPropertyChanged(nameof(DisplayWeight1));
        OnPropertyChanged(nameof(DisplayWeight2));
        OnPropertyChanged(nameof(DisplayNetWeight));
        NotifyCrusherInfoFormStateChanged();
        TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        TakeCrusherWeight2Command.NotifyCanExecuteChanged();
        SaveCrusherWeighingCommand.NotifyCanExecuteChanged();
    }

    private void ClearPendingWeights()
    {
        _pendingWeight1 = null;
        _pendingWeight2 = null;
        _effectiveStandardTareForPendingWeight2 = null;
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
        _effectiveStandardTareForPendingWeight2 = null;
        _pendingWeight1IsStable = false;
        _pendingWeight2IsStable = false;
        _pendingWeight1Mode = WeightMode.AUTO;
        _pendingWeight2Mode = WeightMode.AUTO;
        _activeCrusherSessionId = null;
        RefreshCapturedWeightState();
        // Crusher Weighing: Reset Product and Customer to defaults
        SetDefaultProductAndCustomer();
    }

    private void ApplyVehicleInfo(Vehicle? vehicle)
    {
        if (vehicle != null)
        {
            var effectiveStandardTare = GetEffectiveStandardTare(vehicle);
            SelectedDriverName = vehicle.DriverName;
            StandardTareText = effectiveStandardTare?.ToString("N0", CultureInfo.InvariantCulture);
            _originalDriverName = vehicle.DriverName;
            _originalStandardTare = effectiveStandardTare;
            IsVehicleFormReadOnly = true;
            VehicleSelectionStatusText = $"Đã chọn xe nội bộ: {vehicle.VehiclePlate}";
        }
        else
        {
            SelectedDriverName = null;
            StandardTareText = null;
            _originalDriverName = null;
            _originalStandardTare = null;
            IsVehicleFormReadOnly = true;
            VehicleSelectionStatusText = string.Empty;
        }

        ApplyVehicleWeighingMode(vehicle);
        ShowUpdateButton = false;
    }

    private bool HasCapturedWeight1OrLater()
    {
        if (_pendingWeight1.HasValue || SelectedSession?.Weight1.HasValue == true)
        {
            return true;
        }

        return SelectedSession?.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT2
            or WeighingSessionStatus.ALLOCATION_PENDING
            or WeighingSessionStatus.READY_TO_COMPLETE
            or WeighingSessionStatus.COMPLETED
            or WeighingSessionStatus.CANCELLED;
    }

    private void NotifyCrusherInfoFormStateChanged()
    {
        OnPropertyChanged(nameof(IsCrusherInfoFormReadOnly));
        OnPropertyChanged(nameof(CanEditCrusherInfoForm));
        OnPropertyChanged(nameof(IsVehicleDetailsReadOnly));
    }

    private void CheckForChanges()
    {
        var hasDriverChanged = !string.Equals(SelectedDriverName, _originalDriverName, StringComparison.OrdinalIgnoreCase);
        ShowUpdateButton = hasDriverChanged;
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

    private DateTime GetTodayLocal()
    {
        using var scope = _scopeFactory.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        return clock.TodayLocal;
    }

    private decimal? GetEffectiveStandardTare(Vehicle? vehicle)
        => StandardTarePolicy.GetEffectiveStandardTare(vehicle, GetTodayLocal());

    private bool HasEffectiveStandardTare(Vehicle? vehicle)
        => GetEffectiveStandardTare(vehicle).HasValue;

    private void ApplyVehicleWeighingMode(Vehicle? vehicle)
    {
        if (SelectedSession != null || _activeCrusherSessionId.HasValue)
        {
            return;
        }

        var targetMode = HasEffectiveStandardTare(vehicle)
            ? CrusherWeighingModes.SingleWithStandardTare
            : CrusherWeighingModes.TwoWeigh;

        if (!string.Equals(SelectedWeighingMode, targetMode, StringComparison.Ordinal))
        {
            SelectedWeighingMode = targetMode;
        }
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

        try
        {
            IsLoading = true;
            using var scope = _scopeFactory.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            var vehicles = await vehicleRepo.GetByPlateAsync(vehiclePlate, CancellationToken.None);
            var vehicle = vehicles.FirstOrDefault(v => v.IsInternalVehicle && v.IsActive);

            if (vehicle == null)
            {
                var hasExternal = vehicles.Any(v => string.IsNullOrEmpty(v.MoocNumber));
                _toastService.ShowWarning(hasExternal
                    ? $"Xe {vehiclePlate} đã tồn tại dạng xe ngoài nhưng chưa là xe nội bộ. Vui lòng cập nhật tại màn Danh mục xe trước khi cân."
                    : $"Xe {vehiclePlate} chưa có trong danh mục xe nội bộ. Vui lòng tạo xe tại màn Danh mục xe trước khi cân.");
                SelectedVehicle = null;
                return;
            }

            if (!vehicle.IsActive)
            {
                _toastService.ShowWarning("Xe nội bộ này đang ngừng sử dụng, không thể chọn để cân.");
                SelectedVehicle = null;
                return;
            }

            InternalVehiclePlateInput.SetText(vehicle.VehiclePlate);
            SelectedVehicle = vehicle;
            _toastService.ShowSuccess($"Đã chọn xe nội bộ {vehicle.VehiclePlate}.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to confirm crusher internal vehicle {VehiclePlate}.", vehiclePlate);
            _toastService.ShowError("Không thể chọn xe nội bộ.");
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
                _toastService.ShowWarning("Trọng lượng bì không đúng định dạng.");
                return;
            }

            vehicle.DriverName = SelectedDriverName;

            await vehicleRepo.UpdateAsync(vehicle, CancellationToken.None);

            _toastService.ShowSuccess("Đã cập nhật master data xe nội bộ thành công.");

            _originalDriverName = vehicle.DriverName;
            _originalStandardTare = GetEffectiveStandardTare(vehicle);
            ShowUpdateButton = false;
            VehicleSelectionStatusText = $"Đã chọn xe nội bộ: {vehicle.VehiclePlate}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update vehicle master data.");
            _toastService.ShowError("Không thể cập nhật master data xe nội bộ.");
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

            var vehicles = await vehicleRepo.GetByPlateAsync(vehiclePlate, CancellationToken.None);
            var vehicle = vehicles.FirstOrDefault(v => v.IsInternalVehicle && v.IsActive);

            if (lookupVersion == Volatile.Read(ref _vehicleMasterLookupVersion))
            {
                SelectedVehicle = vehicle;
                if (vehicle == null)
                {
                    var hasExternal = vehicles.Any(v => string.IsNullOrEmpty(v.MoocNumber));
                    IsVehicleFormReadOnly = true;
                    ApplyVehicleWeighingMode(null);
                    VehicleSelectionStatusText = hasExternal
                        ? $"Xe {vehiclePlate} đã tồn tại dạng xe ngoài nhưng chưa là xe nội bộ. Vui lòng cập nhật tại màn Danh mục xe trước khi cân."
                        : $"Xe {vehiclePlate} chưa có trong danh mục xe nội bộ. Vui lòng tạo xe tại màn Danh mục xe trước khi cân.";
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

    private void SetDefaultProductAndCustomer()
    {
        ProductCodeInput.SetText(_defaultProductCode);
        ProductNameInput.SetText(_defaultProductName);
        CustomerCodeInput.SetText(_defaultCustomerCode);
        CustomerNameInput.SetText(_defaultCustomerName);
    }

    private void OnProductCodeSelected(AutocompleteItem item)
    {
        ProductCodeInput.SetText(item.Value);
        if (!string.IsNullOrWhiteSpace(item.Payload?.ProductCode))
        {
            ProductCodeInput.SetText(item.Payload.ProductCode);
        }
        if (!string.IsNullOrWhiteSpace(item.Payload?.ProductName))
        {
            ProductNameInput.SetText(item.Payload.ProductName);
        }
    }

    private void OnProductNameSelected(AutocompleteItem item)
    {
        ProductNameInput.SetText(item.Value);
        if (!string.IsNullOrWhiteSpace(item.Payload?.ProductName))
        {
            ProductNameInput.SetText(item.Payload.ProductName);
        }
        if (!string.IsNullOrWhiteSpace(item.Payload?.ProductCode))
        {
            ProductCodeInput.SetText(item.Payload.ProductCode);
        }
    }

    private void OnCustomerCodeSelected(AutocompleteItem item)
    {
        CustomerCodeInput.SetText(item.Value);
        if (!string.IsNullOrWhiteSpace(item.Payload?.CustomerCode))
        {
            CustomerCodeInput.SetText(item.Payload.CustomerCode);
        }
        if (!string.IsNullOrWhiteSpace(item.Payload?.CustomerName))
        {
            CustomerNameInput.SetText(item.Payload.CustomerName);
        }
    }

    private void OnCustomerNameSelected(AutocompleteItem item)
    {
        CustomerNameInput.SetText(item.Value);
        if (!string.IsNullOrWhiteSpace(item.Payload?.CustomerName))
        {
            CustomerNameInput.SetText(item.Payload.CustomerName);
        }
        if (!string.IsNullOrWhiteSpace(item.Payload?.CustomerCode))
        {
            CustomerCodeInput.SetText(item.Payload.CustomerCode);
        }
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
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var internalVehicles = new List<Vehicle>();

            foreach (var item in results)
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    var vehicles = await vehicleRepo.GetByPlateAsync(item.Value, ct);
                    var internalVeh = vehicles.FirstOrDefault(v => v.IsInternalVehicle && v.IsActive);
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
                    StandardTarePolicy.GetEffectiveStandardTare(v, clock.TodayLocal)?.ToString("N0"),
                    AutocompleteFieldType.Vehicle,
                    new AutocompletePayload
                    {
                        VehiclePlate = v.VehiclePlate,
                        DriverName = v.DriverName,
                        TtcpWeight = StandardTarePolicy.GetEffectiveStandardTare(v, clock.TodayLocal)
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



    public bool CanPrintWeighTicket => SelectedSession != null && SelectedSession.NetWeight.HasValue && SelectedSession.NetWeight.Value > 0;

    [RelayCommand(CanExecute = nameof(CanPrintWeighTicket))]
    private async Task PrintWeighTicketAsync()
    {
        await ExecutePrintFlowAsync();
    }

    private async Task ExecutePrintFlowAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            using var scope = _scopeFactory.CreateScope();
            
            var templateProvider = scope.ServiceProvider.GetRequiredService<IPrintTemplateProvider>();
            var printerDiscovery = scope.ServiceProvider.GetRequiredService<IPrinterDiscoveryService>();
            var printService = scope.ServiceProvider.GetRequiredService<IPrintService>();
            var renderer = scope.ServiceProvider.GetRequiredService<PrintOverlayRenderer>();
            var printDocumentExporter = scope.ServiceProvider.GetRequiredService<IPrintDocumentExporter>();
            var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            
            var mockCutOrder = new CutOrder
            {
                Id = Guid.NewGuid(),
                VehiclePlate = SelectedSession.VehiclePlate,
                CustomerName = SelectedSession.CustomerName,
                ProductName = SelectedSession.ProductName,
                IsExportScale = false
            };
            
            var mockTicket = new WeighTicket
            {
                Id = SelectedSession.SessionId,
                TicketNo = SelectedSession.SessionNo,
                VehiclePlate = SelectedSession.VehiclePlate,
                TransactionType = TransactionType.INBOUND,
                Weight1 = SelectedSession.Weight1 ?? 0m,
                Weight2 = SelectedSession.Weight2 ?? 0m,
                NetWeight = SelectedSession.NetWeight ?? 0m,
                CustomerName = SelectedSession.CustomerName,
                ProductName = SelectedSession.ProductName,
                Weight1Time = SelectedSession.Weight1Time ?? clock.NowLocal,
                Weight2Time = SelectedSession.Weight2Time ?? clock.NowLocal,
                RecordRole = WeighTicketRecordRoles.MasterSession
            };

            var composer = scope.ServiceProvider.GetRequiredService<IWeighTicketPrintComposer>();
            var printedAtLocal = clock.NowLocal;
            
            var page = composer.Compose(
                mockCutOrder,
                mockTicket,
                actualBagCount: null,
                isReturnedBrokenTrip: SelectedSession.IsReturnedBrokenTrip,
                vehicle: null,
                printedAtLocal: printedAtLocal,
                printedByDisplayName: _currentUserContext.DisplayName);
            
            var stationName = _currentStationContext.StationName;
            if (!string.IsNullOrWhiteSpace(stationName))
            {
                var fieldsList = new List<PrintFieldValue>(page.Fields);
                fieldsList.RemoveAll(x => string.Equals(x.FieldKey, "StaticFooterLeft", StringComparison.OrdinalIgnoreCase));
                fieldsList.Add(new PrintFieldValue("StaticFooterLeft", $"XMCP c\u00e2n 120 t\u1ea5n - {stationName}"));
                
                page = new WeighTicketPrintModel
                {
                    DocumentId = page.DocumentId,
                    DisplayNumber = page.DisplayNumber,
                    TicketNo = page.TicketNo,
                    VehiclePlate = page.VehiclePlate,
                    MoocNumber = page.MoocNumber,
                    NetWeight = page.NetWeight,
                    PreviewGroupKey = page.PreviewGroupKey,
                    PreviewGroupName = page.PreviewGroupName,
                    Fields = fieldsList
                };
            }

            var preview = new PrintBatchPreviewModel
            {
                Kind = PrintDocumentKind.WeighTicket,
                Title = UiText.Weighing.PrintPreviewWeighMaster,
                Pages = new List<PrintPreviewPageModel> { page }
            };

            var template = await templateProvider.GetTemplateAsync(PrintDocumentKind.WeighTicket, CancellationToken.None);
            var profiles = await templateProvider.GetProfilesAsync(PrintDocumentKind.WeighTicket, CancellationToken.None);
            
            var printerKey = AppConfigKeys.DefaultWeighTicketPrinter;
            var preferredPrinter = await appConfig.GetValueAsync(printerKey, CancellationToken.None);
            var printers = PrinterSelectionHelper.ApplyPreferredPrinter(
                printerDiscovery.GetInstalledPrinters(),
                preferredPrinter);

            var dialogVm = new PrintOptionsDialogViewModel(
                UiText.Weighing.PrintDialogWeighTicket,
                template,
                preview,
                profiles,
                printers,
                renderer,
                templateProvider,
                printDocumentExporter,
                false,
                1,
                editablePrintDataContext: null);

            var printOptions = await _dialogService.ShowCustomDialogAsync<PrintOptionsDialogViewModel, PrintOptionsModel>(dialogVm);
            if (printOptions == null)
            {
                return;
            }

            var batchToPrint = dialogVm.CurrentBatch;
            var result = await printService.PrintAsync(template, batchToPrint, printOptions, CancellationToken.None);
            
            if (result.HasFailures)
            {
                _toastService.ShowError(string.Format(UiText.Weighing.PrintErrorFormat, "phiếu cân"));
                return;
            }

            _toastService.ShowSuccess(string.Format(UiText.Weighing.PrintSuccessFormat, "phiếu cân"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Crusher print flow failed");
            _toastService.ShowError(string.Format(UiText.Weighing.PrintErrorFormat, "phiếu cân"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Dispose()
    {
        _deviceConnector.Dispose();
    }

    public bool CanEditSessionVehicle => SelectedSession != null;
    public bool CanViewSessionHistory => SelectedSession != null;

    [RelayCommand(CanExecute = nameof(CanEditSessionVehicle))]
    private async Task EditSessionVehicleAsync()
    {
        if (SelectedSession == null)
        {
            _toastService.ShowWarning("Vui lòng chọn lượt cân cần sửa.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var autocompleteService = scope.ServiceProvider.GetRequiredService<IAutocompleteService>();
            var vehicleRepository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var useCases = scope.ServiceProvider.GetRequiredService<CrusherWeighingUseCases>();

            var dialogVm = new EditWeighingSessionVehicleViewModel(
                SelectedSession.SessionId,
                SelectedSession.SessionNo,
                SelectedSession.WeighingMode,
                SelectedSession.Weight1 ?? 0,
                SelectedSession.Weight2,
                SelectedSession.NetWeight,
                SelectedSession.VehiclePlate,
                SelectedSession.StandardTareWeightSnapshot,
                autocompleteService,
                vehicleRepository,
                clock
            );

            var result = await _dialogService.ShowCustomDialogAsync<EditWeighingSessionVehicleViewModel, EditWeighingSessionVehicleResult>(dialogVm);
            if (result != null)
            {
                IsLoading = true;
                await useCases.UpdateSessionVehicleAsync(
                    dialogVm.SessionId,
                    result.NewVehicleId,
                    result.Reason,
                    CancellationToken.None
                );

                _toastService.ShowSuccess("Đã cập nhật biển số xe mới.");
                await LoadSessionsAsync();
                
                var updated = Sessions.FirstOrDefault(s => s.SessionId == dialogVm.SessionId);
                if (updated != null)
                {
                    SelectedSession = updated;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to edit weighing session vehicle.");
            _toastService.ShowError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanViewSessionHistory))]
    private void ViewSessionHistory()
    {
        if (SelectedSession == null)
        {
            _toastService.ShowWarning("Vui lòng chọn lượt cân để xem lịch sử.");
            return;
        }

        NavigateToEditHistoryRequested?.Invoke(SelectedSession.VehiclePlate, SelectedSession.SessionNo);
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
