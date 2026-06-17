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

public partial class ClayWeighingViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly ICameraPreviewService _cameraPreviewService;
    private readonly IToastService _toastService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICurrentStationContext _currentStationContext;
    private readonly ILogger<ClayWeighingViewModel>? _logger;
    private readonly Dispatcher _uiDispatcher;
    private readonly DispatcherTimer _scaleUiTimer;
    private readonly object _scaleReadingLock = new();
    private LatestScaleReadingSnapshot? _pendingScaleReading;
    private bool _pendingScaleDeviceConnected;
    private bool _hasStartedDeviceAttach;
    private CameraSystemSettings? _cameraSettings;
    private Guid? _currentPreviewSessionId;
    private long _lastRenderedPreviewSequence;
    private CameraPreviewFrameReceivedEventArgs? _latestPendingPreviewFrame;
    private int _isPreviewUiUpdatePending;

    // Clay Weighing: Default Product and Customer
    private string _defaultProductCode = ClayDefaults.ProductCode;
    private string _defaultProductName = ClayDefaults.ProductName;
    private string _defaultCustomerCode = ClayDefaults.CustomerCode;
    private string _defaultCustomerName = ClayDefaults.CustomerName;

    public ObservableCollection<ClayWeighingModeOption> WeighingModeOptions { get; } = new()
    {
        new(ClayWeighingModes.TwoWeigh, "C\u00e2n 2 l\u1ea7n"),
        new(ClayWeighingModes.SingleWithStandardTare, "C\u00e2n 1 l\u1ea7n")
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
    private CrusherWeighingSessionListItem? _selectedSession;
    [ObservableProperty] private string? _searchVehicle;
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    private string _selectedWeighingMode = ClayWeighingModes.TwoWeigh;
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
    [ObservableProperty] private string _vehicleSelectionStatusText = "Nhập số xe nội bộ để bắt đầu cân.";
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
    private Guid? _activeCrusherSessionId;
    private bool _pendingWeight1IsStable;
    private bool _pendingWeight2IsStable;
    private WeightMode _pendingWeight1Mode = WeightMode.AUTO;
    private WeightMode _pendingWeight2Mode = WeightMode.AUTO;
    private int _vehicleMasterLookupVersion;
    private const string AutoModeText = "T\u1ef0 \u0110\u1ed8NG";
    private const string ManualModeText = "C\u00c2N TAY";

    // Crusher Weighing: Default Product and Customer
    
    
    
    

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
    public bool IsSingleWeighMode => SelectedWeighingMode == ClayWeighingModes.SingleWithStandardTare;
    public bool IsTwoWeighMode => SelectedWeighingMode == ClayWeighingModes.TwoWeigh;
    public bool ShowCaptureWeight2Button => IsTwoWeighMode;
    public string CaptureWeight1ButtonText => IsSingleWeighMode ? "C\u00c2N" : "C\u00c2N L\u1ea6N 1";
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

    public ClayWeighingViewModel(
        IServiceScopeFactory scopeFactory,
        IScaleDevice scaleDevice,
        ICameraPreviewService cameraPreviewService,
        IToastService toastService,
        ICurrentUserContext currentUserContext,
        ICurrentStationContext currentStationContext,
        ILogger<ClayWeighingViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
        _cameraPreviewService = cameraPreviewService;
        _toastService = toastService;
        _currentUserContext = currentUserContext;
        _currentStationContext = currentStationContext;
        _logger = logger;
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        _scaleDevice.WeightReceived += OnWeightReceived;
        _scaleUiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _scaleUiTimer.Tick += OnScaleUiTimerTick;
        _cameraPreviewService.StatusChanged += OnCameraPreviewStatusChanged;
        _cameraPreviewService.FrameReceived += OnCameraPreviewFrameReceived;

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
                VehicleSelectionStatusText = $"Xe {trimmedText} chưa có trong danh mục. Nhập trọng lượng xe chuẩn rồi bắt đầu cân.";
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

    public async Task InitializeAsync()
    {
        await LoadDefaultSettingsAsync();

        using (var scope = _scopeFactory.CreateScope())
        {
            var useCases = scope.ServiceProvider.GetRequiredService<ClayWeighingUseCases>();
            try
            {
                SelectedWeighingMode = await useCases.GetDefaultWeighingModeAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get default weighing mode, using default TWO_WEIGH");
                SelectedWeighingMode = ClayWeighingModes.TwoWeigh;
            }
        }

        await LoadSessionsAsync();
        StartDeviceAttachIfNeeded();
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

            var dbProductCode = await settingsRepo.GetValueAsync(stationCode, ClayStationOperationSettingKeys.ClayDefaultProductCode, CancellationToken.None);
            var dbCustomerCode = await settingsRepo.GetValueAsync(stationCode, ClayStationOperationSettingKeys.ClayDefaultCustomerCode, CancellationToken.None);

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
            var useCases = scope.ServiceProvider.GetRequiredService<ClayWeighingUseCases>();
            var keyword = !string.IsNullOrWhiteSpace(SearchSessionNo)
                ? SearchSessionNo
                : SearchVehicle;
            Sessions = new ObservableCollection<CrusherWeighingSessionListItem>(
                await useCases.SearchSessionsAsync(keyword, SelectedDate, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load clay weighing sessions.");
            _toastService.ShowError("Không thể tải danh sách lượt cân mỏ sét.");
        }
        finally
        {
            IsLoading = false;
        }
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
            var useCases = scope.ServiceProvider.GetRequiredService<ClayWeighingUseCases>();
            try
            {
                SelectedWeighingMode = await useCases.GetDefaultWeighingModeAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get default weighing mode during refresh, using default TWO_WEIGH");
                SelectedWeighingMode = ClayWeighingModes.TwoWeigh;
            }
        }

        // Reload sessions list
        await LoadSessionsAsync();
        await LoadCameraPreviewAsync();
    }

    partial void OnSelectedPreviewCameraCodeChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _ = StartCameraPreviewAsync(value);
    }

    private async Task LoadCameraPreviewAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<ICameraSettingsProvider>();
            var settings = await provider.GetForStationAsync("CLAY", CancellationToken.None);
            ApplyCameraPreviewSettings(settings);
            _ = StartCameraPreviewAsync(SelectedPreviewCameraCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Load camera preview settings failed for clay weighing");
            IsCameraPreviewAvailable = false;
            IsCamera1PreviewAvailable = false;
            IsCamera2PreviewAvailable = false;
            CameraPreviewStatusText = "Không tải được cấu hình camera";
            OnPropertyChanged(nameof(ShowCamera1Selector));
            OnPropertyChanged(nameof(ShowCamera2Selector));
            OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
        }
    }

    private void ApplyCameraPreviewSettings(CameraSystemSettings settings)
    {
        _cameraSettings = settings;
        Camera1PreviewName = settings.Camera1.DisplayName;
        Camera2PreviewName = settings.Camera2.DisplayName;
        IsCamera1PreviewAvailable = settings.Camera1.IsEnabled && !string.IsNullOrWhiteSpace(settings.Camera1.EffectivePreviewRtspUrl);
        IsCamera2PreviewAvailable = settings.Camera2.IsEnabled && !string.IsNullOrWhiteSpace(settings.Camera2.EffectivePreviewRtspUrl);
        IsCameraPreviewAvailable = IsCamera1PreviewAvailable || IsCamera2PreviewAvailable;

        var preferred = string.IsNullOrWhiteSpace(settings.PreviewDefaultCameraCode)
            ? AppConfigDefaults.DefaultCameraPreview
            : settings.PreviewDefaultCameraCode.Trim().ToUpperInvariant();

        var targetCameraCode =
            preferred == "CAM2" && IsCamera2PreviewAvailable ? "CAM2" :
            IsCamera1PreviewAvailable ? "CAM1" :
            IsCamera2PreviewAvailable ? "CAM2" :
            preferred;

        OnPropertyChanged(nameof(ShowCamera1Selector));
        OnPropertyChanged(nameof(ShowCamera2Selector));

        if (!string.Equals(SelectedPreviewCameraCode, targetCameraCode, StringComparison.OrdinalIgnoreCase))
        {
            SelectedPreviewCameraCode = targetCameraCode;
        }
        else if (!IsCameraPreviewAvailable)
        {
            CameraPreviewStatusText = "Chưa cấu hình camera";
            OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
        }
    }

    private async Task StartCameraPreviewAsync(string cameraCode)
    {
        if (_cameraSettings == null)
        {
            return;
        }

        var camera = ResolvePreviewCamera(cameraCode);
        if (camera == null)
        {
            CameraPreviewStatusText = IsCameraPreviewAvailable ? "Camera chưa sẵn sàng" : "Chưa cấu hình camera";
            ResetPreviewRenderState();
            _ = _cameraPreviewService.StopPreviewAsync();
            OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
            return;
        }

        ResetPreviewRenderState();
        CameraPreviewStatusText = "Đang kết nối";
        try
        {
            await _cameraPreviewService.StartPreviewAsync(camera, CancellationToken.None);
            _currentPreviewSessionId = _cameraPreviewService.ActivePreviewSessionId;
            OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Start preview for clay camera {CameraCode} failed", camera.CameraCode);
            ResetPreviewRenderState();
            CameraPreviewStatusText = "Không kết nối được camera";
            OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
        }
    }

    private CameraEndpointSettings? ResolvePreviewCamera(string? cameraCode)
    {
        if (_cameraSettings == null || string.IsNullOrWhiteSpace(cameraCode))
        {
            return null;
        }

        return cameraCode.Trim().ToUpperInvariant() switch
        {
            "CAM1" when IsCamera1PreviewAvailable => _cameraSettings.Camera1,
            "CAM2" when IsCamera2PreviewAvailable => _cameraSettings.Camera2,
            _ => null
        };
    }

    private void OnCameraPreviewStatusChanged(object? sender, CameraPreviewStatusChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.CameraCode)
            && !string.Equals(e.CameraCode, SelectedPreviewCameraCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _uiDispatcher.BeginInvoke(() =>
        {
            if (string.Equals(CameraPreviewStatusText, e.StatusText, StringComparison.Ordinal))
            {
                return;
            }

            CameraPreviewStatusText = e.StatusText;
            OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
        });
    }

    private void OnCameraPreviewFrameReceived(object? sender, CameraPreviewFrameReceivedEventArgs e)
    {
        if (!_currentPreviewSessionId.HasValue || e.PreviewSessionId != _currentPreviewSessionId.Value)
        {
            return;
        }

        if (e.Sequence <= Interlocked.Read(ref _lastRenderedPreviewSequence))
        {
            return;
        }

        _latestPendingPreviewFrame = e;
        if (Interlocked.Exchange(ref _isPreviewUiUpdatePending, 1) == 1)
        {
            return;
        }

        _uiDispatcher.BeginInvoke(() =>
        {
            try
            {
                var latest = _latestPendingPreviewFrame;
                if (latest == null)
                {
                    return;
                }

                if (!_currentPreviewSessionId.HasValue || latest.PreviewSessionId != _currentPreviewSessionId.Value)
                {
                    return;
                }

                if (latest.Sequence <= Interlocked.Read(ref _lastRenderedPreviewSequence))
                {
                    return;
                }

                CameraPreviewSource = latest.Frame;
                Interlocked.Exchange(ref _lastRenderedPreviewSequence, latest.Sequence);
            }
            finally
            {
                Interlocked.Exchange(ref _isPreviewUiUpdatePending, 0);
                if (_latestPendingPreviewFrame != null && _latestPendingPreviewFrame.Sequence > Interlocked.Read(ref _lastRenderedPreviewSequence))
                {
                    OnCameraPreviewFrameReceived(this, _latestPendingPreviewFrame);
                }
            }
        }, DispatcherPriority.Render);
    }

    private void ResetPreviewRenderState()
    {
        _currentPreviewSessionId = null;
        _latestPendingPreviewFrame = null;
        Interlocked.Exchange(ref _lastRenderedPreviewSequence, 0);
        Interlocked.Exchange(ref _isPreviewUiUpdatePending, 0);
        CameraPreviewSource = null;
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

        if (IsManualMode)
        {
            ApplyStabilityDisplay(IsStable);
        }
    }

    private bool CanTakeCrusherWeight1()
    {
        if (IsLoading || string.IsNullOrWhiteSpace(InternalVehiclePlateInput.Text))
            return false;

        // KhÃƒÂ´ng cho phÃƒÂ©p bÃ¡ÂºÂ¯t Ã„â€˜Ã¡ÂºÂ§u cÃƒÂ¢n lÃ¡ÂºÂ§n 1 nÃ¡ÂºÂ¿u:
        // - Ã„Âang cÃƒÂ³ active session (Ã„â€˜ÃƒÂ£ cÃƒÂ¢n lÃ¡ÂºÂ§n 1 nhÃ†Â°ng chÃ†Â°a hoÃƒÂ n thÃƒÂ nh session Ã„â€˜ang cÃƒÂ¢n)
        // - Session Ã„â€˜ÃƒÂ£ hoÃƒÂ n thÃƒÂ nh hoÃ¡ÂºÂ·c Ã„â€˜ÃƒÂ£ hÃ¡Â»Â§y (ngÃ†Â°Ã¡Â»Âi dÃƒÂ¹ng cÃ¡ÂºÂ§n deselect Ã„â€˜Ã¡Â»Æ’ cÃƒÂ¢n xe khÃƒÂ¡c)
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

        // ChÃ¡Â»â€° cho phÃƒÂ©p cÃƒÂ¢n lÃ¡ÂºÂ§n 2 khi Ã„â€˜ÃƒÂ£ lÃ†Â°u lÃ¡ÂºÂ§n 1 vÃƒÂ o DB (cÃƒÂ³ active session vÃƒÂ  trÃ¡ÂºÂ¡ng thÃƒÂ¡i lÃƒÂ  PENDING_WEIGHT2)
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

        // KhÃƒÂ´ng cho phÃƒÂ©p lÃ†Â°u nÃ¡ÂºÂ¿u session Ã„â€˜ÃƒÂ£ hoÃƒÂ n thÃƒÂ nh hoÃ¡ÂºÂ·c Ã„â€˜ÃƒÂ£ hÃ¡Â»Â§y
        var sessionStatus = SelectedSession?.SessionStatus;
        if (sessionStatus == WeighingSessionStatus.COMPLETED
            || sessionStatus == WeighingSessionStatus.CANCELLED)
        {
            return false;
        }

        // CÃƒÂ¢n 1 lÃ¡ÂºÂ§n: cÃ¡ÂºÂ§n cÃƒÂ³ pending weight 1 vÃƒÂ  selected vehicle
        if (IsSingleWeighMode && _pendingWeight1.HasValue && SelectedVehicle != null)
            return true;

        // CÃƒÂ¢n 2 lÃ¡ÂºÂ§n - trÃ†Â°Ã¡Â»Âng hÃ¡Â»Â£p mÃ¡Â»â€ºi (chÃ†Â°a cÃƒÂ³ active session):
        //   - CÃ¡ÂºÂ§n cÃƒÂ³ pending weight 1 vÃƒÂ  selected vehicle (Ã„â€˜Ã¡Â»Æ’ tÃ¡ÂºÂ¡o session mÃ¡Â»â€ºi vÃ¡Â»â€ºi weight1)
        if (IsTwoWeighMode && _pendingWeight1.HasValue && SelectedVehicle != null && !_activeCrusherSessionId.HasValue)
            return true;

        // CÃƒÂ¢n 2 lÃ¡ÂºÂ§n - trÃ†Â°Ã¡Â»Âng hÃ¡Â»Â£p tiÃ¡ÂºÂ¿p tÃ¡Â»Â¥c (cÃƒÂ³ active session):
        //   - CÃ¡ÂºÂ§n cÃƒÂ³ pending weight 2 vÃƒÂ  session Ã„â€˜ang Ã¡Â»Å¸ trÃ¡ÂºÂ¡ng thÃƒÂ¡i PENDING_WEIGHT2
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
            var useCases = scope.ServiceProvider.GetRequiredService<ClayWeighingUseCases>();
            var sessionId = _activeCrusherSessionId;
            if (sessionId is null)
            {
                sessionId = await useCases.CreateSessionAsync(
                    new CreateClaySessionRequest(
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
                    new CaptureClayWeight2Request(
                        sessionId.Value,
                        _pendingWeight2.Value,
                        _pendingWeight2IsStable,
                        _pendingWeight2Mode),
                    CancellationToken.None);
            }

            _toastService.ShowSuccess("Đã lưu lượt cân mỏ sét.");

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
            _logger?.LogWarning(ex, "Failed to save clay weighing session.");
            _toastService.ShowError("Không thể lưu lượt cân mỏ sét.");
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

            // Crusher Weighing: Notify read-only state change
            OnPropertyChanged(nameof(IsWeighingReadOnly));
            NotifyCrusherInfoFormStateChanged();

            var isTwoWeighPending = value.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
                && string.Equals(value.WeighingMode, ClayWeighingModes.TwoWeigh, StringComparison.OrdinalIgnoreCase);

            if (isTwoWeighPending)
            {
                _activeCrusherSessionId = value.SessionId;
                InternalVehiclePlateInput.SetText(value.VehiclePlate);
                SelectedDriverName = value.DriverName;
                StandardTareText = value.StandardTareWeightSnapshot?.ToString("N0", CultureInfo.InvariantCulture);
                // Crusher Weighing: Set Product and Customer from session
                ProductCodeInput.SetText(value.ProductCode);
                ProductNameInput.SetText(value.ProductName);
                CustomerCodeInput.SetText(value.CustomerCode);
                CustomerNameInput.SetText(value.CustomerName);
            }
            else if (value.SessionStatus == WeighingSessionStatus.COMPLETED
                || value.SessionStatus == WeighingSessionStatus.CANCELLED)
            {
                // session hoÃƒÂ n tÃ¡ÂºÂ¥t hoÃ¡ÂºÂ·c Ã„â€˜ÃƒÂ£ hÃ¡Â»Â§y - chÃ¡Â»â€° hiÃ¡Â»Æ’n thÃ¡Â»â€¹ thÃƒÂ´ng tin, khÃƒÂ´ng cho sÃ¡Â»Â­a
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
                // Session Ã„â€˜ang chÃ¡Â»Â cÃƒÂ¢n lÃ¡ÂºÂ§n 1 - khÃƒÂ´ng set active session (cÃ¡ÂºÂ§n cÃƒÂ¢n lÃ¡ÂºÂ¡i)
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
            var vehicles = await vehicleRepo.GetByPlateAsync(vehiclePlate, CancellationToken.None);
            var vehicle = vehicles.FirstOrDefault(v => v.IsInternalVehicle);

            if (vehicle == null)
            {
                var existingExternal = vehicles.FirstOrDefault(v => string.IsNullOrEmpty(v.MoocNumber));
                if (existingExternal != null)
                {
                    if (standardTare is null or <= 0)
                    {
                        _toastService.ShowWarning("Xe nội bộ mới bắt buộc nhập TL xe chuẩn lớn hơn 0.");
                        return null;
                    }

                    existingExternal.IsInternalVehicle = true;
                    existingExternal.TtcpWeight = standardTare;
                    existingExternal.StandardTareSource = null;
                    existingExternal.StandardTareUpdatedAt = now;
                    existingExternal.StandardTareUpdatedBy = "Operator";
                    existingExternal.IsActive = true;
                    existingExternal.UpdatedAt = now;
                    existingExternal.UpdatedBy = "Operator";
                    if (!string.IsNullOrWhiteSpace(SelectedDriverName))
                    {
                        existingExternal.DriverName = SelectedDriverName.Trim();
                    }

                    await vehicleRepo.UpdateAsync(existingExternal, CancellationToken.None);
                    await unitOfWork.SaveChangesAsync(CancellationToken.None);
                    await EnqueueVehicleSyncAsync(scope.ServiceProvider, existingExternal, now);
                    vehicle = existingExternal;
                }
                else
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
            _logger?.LogError(ex, "Failed to ensure clay internal vehicle {VehiclePlate}.", vehiclePlate);
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
        NotifyCrusherInfoFormStateChanged();
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
        // Crusher Weighing: Reset Product and Customer to defaults
        SetDefaultProductAndCustomer();
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
                var existingExternal = vehicles.FirstOrDefault(v => string.IsNullOrEmpty(v.MoocNumber));
                if (existingExternal != null)
                {
                    if (standardTare is null or <= 0)
                    {
                        _toastService.ShowWarning("Xe nội bộ mới bắt buộc nhập trọng lượng xe chuẩn lớn hơn 0.");
                        return;
                    }

                    existingExternal.IsInternalVehicle = true;
                    existingExternal.TtcpWeight = standardTare;
                    existingExternal.StandardTareSource = null;
                    existingExternal.StandardTareUpdatedAt = now;
                    existingExternal.StandardTareUpdatedBy = "Operator";
                    existingExternal.IsActive = true;
                    existingExternal.UpdatedAt = now;
                    existingExternal.UpdatedBy = "Operator";
                    if (SelectedDriverName != null)
                    {
                        existingExternal.DriverName = SelectedDriverName.Trim();
                    }

                    await vehicleRepo.UpdateAsync(existingExternal, CancellationToken.None);
                    using (var innerUowScope = scope.ServiceProvider.CreateScope())
                    {
                        var uow = innerUowScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        await uow.SaveChangesAsync(CancellationToken.None);
                    }
                    await EnqueueVehicleSyncAsync(scope.ServiceProvider, existingExternal, now);
                    vehicle = existingExternal;
                }
                else
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
                    using (var innerUowScope = scope.ServiceProvider.CreateScope())
                    {
                        var uow = innerUowScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        await uow.SaveChangesAsync(CancellationToken.None);
                    }
                    await EnqueueVehicleSyncAsync(scope.ServiceProvider, vehicle, now);
                    created = true;
                }
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
            _logger?.LogError(ex, "Failed to confirm clay internal vehicle {VehiclePlate}.", vehiclePlate);
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
            _logger?.LogWarning(ex, "Failed to enqueue clay internal vehicle sync for {VehiclePlate}.", vehicle.VehiclePlate);
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
            var vehicle = vehicles.FirstOrDefault(v => v.IsInternalVehicle);

            if (lookupVersion == Volatile.Read(ref _vehicleMasterLookupVersion))
            {
                SelectedVehicle = vehicle;
                if (vehicle == null)
                {
                    var hasExternal = vehicles.Any(v => string.IsNullOrEmpty(v.MoocNumber));
                    IsVehicleFormReadOnly = false;
                    VehicleSelectionStatusText = hasExternal
                        ? $"Xe {vehiclePlate} đã tồn tại dạng xe ngoài. Nhập TL xe chuẩn rồi bấm Chọn/Tạo xe để chuyển thành xe nội bộ."
                        : $"Xe {vehiclePlate} chưa có trong danh mục. Nhập trọng lượng xe chuẩn rồi bấm Chọn/Tạo xe.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Load clay vehicle master info failed for plate {VehiclePlate}", vehiclePlate);
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
                _logger?.LogWarning(ex, "Clay weighing scale attach failed.");
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
            IsDeviceConnected = connected;
            IsStable = latest.IsStable;
            ApplyStabilityDisplay(latest.IsStable);
            DeviceStatusText = connected ? "Đang kết nối đầu cân" : "Mất kết nối đầu cân";
            return;
        }

        CurrentWeight = latest.Weight;
        IsStable = latest.IsStable;
        IsDeviceConnected = connected;
        StabilityText = latest.IsStable ? "ỔN ĐỊNH" : "CHƯA ỔN ĐỊNH";
        ApplyStabilityDisplay(latest.IsStable);
        DeviceStatusText = connected ? "Đang kết nối đầu cân" : "Mất kết nối đầu cân";
    }

    private void ApplyStabilityDisplay(bool isStable)
    {
        StabilityText = isStable ? "\u1ed4N \u0110\u1ecaNH" : "CH\u01afA \u1ed4N \u0110\u1ecaNH";
        StabilityBrush = isStable
            ? new SolidColorBrush(Color.FromRgb(46, 213, 115))
            : new SolidColorBrush(Colors.Orange);
    }

    public void Dispose()
    {
        _scaleUiTimer.Stop();
        _scaleUiTimer.Tick -= OnScaleUiTimerTick;
        _scaleDevice.WeightReceived -= OnWeightReceived;
        _cameraPreviewService.StatusChanged -= OnCameraPreviewStatusChanged;
        _cameraPreviewService.FrameReceived -= OnCameraPreviewFrameReceived;
        ResetPreviewRenderState();
        _ = _cameraPreviewService.StopPreviewAsync();
    }
}

public sealed record ClayWeighingModeOption(string Value, string DisplayName)
{
    public override string ToString()
        => Value switch
        {
            ClayWeighingModes.SingleWithStandardTare => "Cân 1 lần",
            ClayWeighingModes.TwoWeigh => "Cân 2 lần",
            _ => DisplayName
        };
}


