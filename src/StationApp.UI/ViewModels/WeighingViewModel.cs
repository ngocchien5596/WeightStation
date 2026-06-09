using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Printing;
using StationApp.Application.Security;
using StationApp.Application.UseCases;
using StationApp.Device.Abstractions;
using StationApp.Device.Models;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.UI.Converters;
using StationApp.UI.Printing;
using StationApp.UI.Resources;
using StationApp.UI.Services;
using StationApp.UI.Helpers;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels;

public partial class WeighingViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly ICameraPreviewService _cameraPreviewService;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<WeighingViewModel>? _logger;
    private readonly Dispatcher _uiDispatcher;
    private Guid? _focusSessionId;
    private decimal? _pendingCapturedWeight1;
    private decimal? _pendingCapturedWeight2;
    private bool _pendingWeight1IsStable;
    private bool _pendingWeight2IsStable;
    private WeightMode _pendingWeight1Mode = WeightMode.AUTO;
    private WeightMode _pendingWeight2Mode = WeightMode.AUTO;
    private bool _isUpdatingOverweightSplitInputs;
    private bool _isUpdatingPriorityAllocation;
    private int _overweightPreviewRequestVersion;
    private bool _hasStartedDeviceAttach;
    private int _selectedSessionLoadVersion;
    private readonly object _scaleReadingLock = new();
    private readonly DispatcherTimer _scaleUiTimer;
    private LatestScaleReadingSnapshot? _pendingScaleReading;
    private bool _pendingScaleDeviceConnected;
    private bool _isApplyingBaggedActualWeightOverrideState;
    private CameraSystemSettings? _cameraSettings;
    private Guid? _currentPreviewSessionId;
    private long _lastRenderedPreviewSequence;
    private CameraPreviewFrameReceivedEventArgs? _latestPendingPreviewFrame;
    private int _isPreviewUiUpdatePending;
    private bool _isApplyingNoLoadState;

    public event Action<Guid>? NavigateToExportWeighingRequested;

    private static readonly SolidColorBrush StableBrush = new(Color.FromRgb(46, 213, 115));
    private static readonly SolidColorBrush UnstableBrush = new(Colors.Orange);

    [ObservableProperty] private ObservableCollection<WeighingSessionListItem> _sessions = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CaptureWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(CaptureWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCapturedWeightCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenAllocationCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenAppendCutOrdersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowOverweightHandlingCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintWeighTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintDeliveryTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveToOutYardCommand))]
    [NotifyCanExecuteChangedFor(nameof(MarkNoLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowRelatedTicketsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewImageHistoryCommand))]
    private WeighingSessionListItem? _selectedSession;

    [ObservableProperty] private ObservableCollection<WeighingSessionLineRow> _sessionLines = new();
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty] private string? _searchErpCutOrderId;
    [ObservableProperty] private string? _searchVehiclePlate;

    [ObservableProperty] private string? _sessionNo;
    [ObservableProperty] private TransactionType _transactionType;
    [ObservableProperty] private string? _vehiclePlate;
    [ObservableProperty] private string? _moocNumber;
    [ObservableProperty] private string? _driverName;
    [ObservableProperty] private string? _customerSummary;
    [ObservableProperty] private string? _productSummary;
    [ObservableProperty] private string? _notesSummary;
    [ObservableProperty] private bool _useActualWeightForBaggedCutOrders;
    [ObservableProperty] private decimal? _weight1;
    [ObservableProperty] private decimal? _weight2;
    [ObservableProperty] private decimal? _netWeight;
    [ObservableProperty] private decimal? _ttcp10WeightSnapshot;
    [ObservableProperty] private decimal _overweightAmount;
    [ObservableProperty] private decimal _overweightSplitStepWeight;
    [ObservableProperty] private bool _isOverweight;
    [ObservableProperty] private string? _sessionStatusText;
    [ObservableProperty] private string? _overweightResolutionText;
    [ObservableProperty] private string _overweightSplitTicket1WeightText = string.Empty;
    [ObservableProperty] private string _overweightSplitTicket2WeightText = string.Empty;
    [ObservableProperty] private string _overweightSplitModeText = string.Empty;
    [ObservableProperty] private string _overweightSplitRandomFactorText = string.Empty;
    [ObservableProperty] private string? _overweightSplitValidationMessage;
    [ObservableProperty] private bool _isManualSplitOverride;
    [ObservableProperty] private bool _isOverweightSplitPlanValid;

    [ObservableProperty] private decimal _currentWeight;
    [ObservableProperty] private bool _isStable;
    [ObservableProperty] private string _stabilityText = "CHƯA ỔN ĐỊNH";
    [ObservableProperty] private SolidColorBrush _stabilityBrush = UnstableBrush;
    [ObservableProperty] private string _currentCaptureMode = "TỰ ĐỘNG";
    [ObservableProperty] private string _deviceStatusText = UiText.Weighing.InitializingDevice;
    [ObservableProperty] private bool _isDeviceConnected;
    [ObservableProperty] private bool _isInitializing;
    [ObservableProperty] private string _cameraPreviewStatusText = "Chưa cấu hình camera";
    [ObservableProperty] private System.Windows.Media.ImageSource? _cameraPreviewSource;
    [ObservableProperty] private string _selectedPreviewCameraCode = "CAM1";
    [ObservableProperty] private bool _isCameraPreviewAvailable;
    [ObservableProperty] private bool _isCamera1PreviewAvailable;
    [ObservableProperty] private bool _isCamera2PreviewAvailable;
    [ObservableProperty] private string _camera1PreviewName = "Camera 1";
    [ObservableProperty] private string _camera2PreviewName = "Camera 2";

    [ObservableProperty] private bool _isAllocationVisible;
    [ObservableProperty] private ObservableCollection<WeighingSessionLineRow> _allocationLines = new();
    [ObservableProperty] private bool _isAppendCutOrdersVisible;
    [ObservableProperty] private ObservableCollection<IncomingVehicleSelectionItem> _appendCutOrderCandidates = new();
    [ObservableProperty] private string? _appendCutOrdersWarningMessage;
    [ObservableProperty] private bool _isRelatedTicketsVisible;
    [ObservableProperty] private ObservableCollection<RelatedDocumentListItem> _relatedTickets = new();
    [ObservableProperty] private bool _isOverweightHandlingVisible;
    [ObservableProperty] private ObservableCollection<OverweightSplitPreviewGroupItem> _overweightPreviewGroups = new();
    [ObservableProperty] private ObservableCollection<OverweightSplitPreviewLineItem> _overweightPreviewLines = new();
    [ObservableProperty] private bool _isNoLoadMarked;

    public bool IsAutoMode => CurrentCaptureMode == "TỰ ĐỘNG";
    public bool IsManualMode => CurrentCaptureMode == "CÂN TAY";
    public bool CanUseManualMode => StationAuthorization.CanUseManualWeighing(_currentUserContext.RoleCode);
    public bool ShowAllocationAction =>
        SelectedSession != null
        && SessionLines.Count > 1
        && SelectedSession.SessionStatus is WeighingSessionStatus.ALLOCATION_PENDING or WeighingSessionStatus.READY_TO_COMPLETE
        && SessionLines.Any(x =>
            x.LineStatus != WeighingSessionLineStatus.ALLOCATED
            || !x.ActualAllocatedWeight.HasValue);
    public bool ShowOverweightHandlingAction => CanShowOverweightHandling();
    public bool ShowBaggedActualWeightOverride =>
        SessionLines.Count > 0
        && SessionLines.All(x =>
            string.Equals(
                ProductTypes.Normalize(x.ProductType),
                ProductTypes.Bagged,
                StringComparison.OrdinalIgnoreCase));
    public bool ShowCamera1Selector => IsCameraPreviewAvailable && IsCamera1PreviewAvailable;
    public bool ShowCamera2Selector => IsCameraPreviewAvailable && IsCamera2PreviewAvailable;
    public bool ShowCameraPreviewPlaceholder =>
        !IsCameraPreviewAvailable
        || !_cameraPreviewService.IsPreviewRunning;
    public bool CanToggleNoLoad => SelectedSession?.IsNoLoad == true || CanMarkNoLoad();

    public WeighingViewModel(
        IServiceScopeFactory scopeFactory,
        IScaleDevice scaleDevice,
        ICameraPreviewService cameraPreviewService,
        IToastService toastService,
        IDialogService dialogService,
        IClock clock,
        ICurrentUserContext currentUserContext,
        ILogger<WeighingViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
        _cameraPreviewService = cameraPreviewService;
        _toastService = toastService;
        _dialogService = dialogService;
        _clock = clock;
        _currentUserContext = currentUserContext;
        _logger = logger;
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        _scaleUiTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _scaleUiTimer.Tick += OnScaleUiTimerTick;

        _scaleDevice.WeightReceived += OnWeightReceived;
        _cameraPreviewService.StatusChanged += OnCameraPreviewStatusChanged;
        _cameraPreviewService.FrameReceived += OnCameraPreviewFrameReceived;
    }

    partial void OnCurrentCaptureModeChanged(string value)
    {
        if (value == "CÂN TAY" && !CanUseManualMode)
        {
            CurrentCaptureMode = "TỰ ĐỘNG";
            _toastService.ShowWarning(UiText.Weighing.ManualModeForbidden);
            return;
        }

        OnPropertyChanged(nameof(IsAutoMode));
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(CanUseManualMode));
    }

    partial void OnOverweightSplitTicket1WeightTextChanged(string value)
    {
        HandleOverweightSplitWeightEdited(value, isFirstTicket: true);
    }

    partial void OnOverweightSplitTicket2WeightTextChanged(string value)
    {
        HandleOverweightSplitWeightEdited(value, isFirstTicket: false);
    }

    partial void OnSelectedSessionChanged(WeighingSessionListItem? value)
    {
        ClearPendingCapturedWeights();
        var loadVersion = ++_selectedSessionLoadVersion;
        _ = LoadSelectedSessionAsync(value, loadVersion);
    }

    partial void OnIsNoLoadMarkedChanged(bool value)
    {
        if (_isApplyingNoLoadState)
        {
            return;
        }

        if (!value)
        {
            if (SelectedSession?.IsNoLoad == true)
            {
                _isApplyingNoLoadState = true;
                try
                {
                    IsNoLoadMarked = true;
                }
                finally
                {
                    _isApplyingNoLoadState = false;
                }
            }

            return;
        }

        _ = MarkNoLoadFromCheckboxAsync();
    }

    partial void OnUseActualWeightForBaggedCutOrdersChanged(bool value)
    {
        if (_isApplyingBaggedActualWeightOverrideState || SelectedSession == null)
        {
            return;
        }

        _ = PersistBaggedActualWeightOverrideAsync(value);
    }

    partial void OnSelectedPreviewCameraCodeChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _ = StartCameraPreviewAsync(value);
    }

    public async Task InitializeAsync()
    {
        if (IsInitializing)
        {
            return;
        }

        IsInitializing = true;
        try
        {
            using (Helpers.PerformanceLogger.Track("Weighing.Initialize"))
            {
                EnsureDeviceAttachStarted();
                await LoadSessionsAsync();
                await LoadCameraPreviewAsync();
            }
        }
        finally
        {
            IsInitializing = false;
        }
    }

    public async Task FocusSessionAsync(Guid sessionId)
    {
        _focusSessionId = sessionId;
        await LoadSessionsAsync();
    }

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {
        await LoadSessionsInternalAsync(true);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        SearchSessionNo = null;
        SearchErpCutOrderId = null;
        SearchVehiclePlate = null;
        _focusSessionId = null;
        IsAllocationVisible = false;
        IsRelatedTicketsVisible = false;
        ResetOverweightHandlingState();
        IsOverweightHandlingVisible = false;
        SelectedSession = null;
        await LoadSessionsInternalAsync(false);
        await LoadCameraPreviewAsync();
    }

    private async Task LoadSessionsInternalAsync(bool selectFirstWhenNoSelection)
    {
        using var perfScope = Helpers.PerformanceLogger.Track("Weighing.LoadSessions");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<GetWeighingSessionsUseCase>();
            var list = await uc.ExecuteAsync(null, null, CancellationToken.None);
            var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
            var filtered = new List<WeighingSessionListItem>();
            var cutOrderKeyword = SearchErpCutOrderId?.Trim();

            foreach (var item in list)
            {
                if (!MatchesSearch(item.SessionNo, SearchSessionNo)
                    || !MatchesSearch(item.VehiclePlate, SearchVehiclePlate))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cutOrderKeyword))
                {
                    var lineItems = await sessionRepo.GetLineItemsBySessionIdAsync(item.SessionId, CancellationToken.None);
                    if (!lineItems.Any(x => MatchesSearch(x.ErpCutOrderId, cutOrderKeyword)))
                    {
                        continue;
                    }
                }

                filtered.Add(item);
            }

            Sessions = new ObservableCollection<WeighingSessionListItem>(filtered);

            if (_focusSessionId.HasValue)
            {
                SelectedSession = Sessions.FirstOrDefault(x => x.SessionId == _focusSessionId.Value);
                _focusSessionId = null;
            }
            else if (SelectedSession != null)
            {
                SelectedSession = Sessions.FirstOrDefault(x => x.SessionId == SelectedSession.SessionId);
            }
            else if (selectFirstWhenNoSelection)
            {
                SelectedSession = Sessions.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Load weighing sessions failed");
            _toastService.ShowError(UiText.Weighing.LoadSessionsError);
        }
    }

    private async Task LoadSelectedSessionAsync(WeighingSessionListItem? value, int loadVersion)
    {
        if (value == null)
        {
            ClearSelectionDetails();
            return;
        }

        using var perfScope = Helpers.PerformanceLogger.Track("Weighing.LoadSelectedSession");
        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
        var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        var rawLineItems = await sessionRepo.GetLineItemsBySessionIdAsync(value.SessionId, CancellationToken.None);
        var lineItems = new List<WeighingSessionLineItem>();
        foreach (var item in rawLineItems)
        {
            if (string.IsNullOrWhiteSpace(item.ProductType) && !string.IsNullOrWhiteSpace(item.ProductCode))
            {
                var product = await productRepo.GetByCodeAsync(item.ProductCode, CancellationToken.None);
                if (product != null && !string.IsNullOrWhiteSpace(product.ProductType))
                {
                    lineItems.Add(item with { ProductType = product.ProductType });
                    continue;
                }
            }
            lineItems.Add(item);
        }

        if (loadVersion != _selectedSessionLoadVersion)
        {
            return;
        }

        decimal? ttcp10Fallback = null;
        if (!value.Ttcp10WeightSnapshot.HasValue && !string.IsNullOrWhiteSpace(value.VehiclePlate))
        {
            try
            {
                var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
                var vehicle = await vehicleRepo.GetByPlateAndMoocAsync(value.VehiclePlate, value.MoocNumber ?? string.Empty, CancellationToken.None)
                    ?? (await vehicleRepo.GetByPlateAsync(value.VehiclePlate, CancellationToken.None)).FirstOrDefault();
                if (vehicle != null && vehicle.TtcpWeight.HasValue && vehicle.TtcpWeight.Value > 0m)
                {
                    ttcp10Fallback = decimal.Round(vehicle.TtcpWeight.Value * 1.10m, 3, MidpointRounding.AwayFromZero);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load vehicle TTCP fallback for {VehiclePlate}", value.VehiclePlate);
            }
        }

        SessionNo = value.SessionNo;
        TransactionType = value.TransactionType;
        VehiclePlate = value.VehiclePlate;
        MoocNumber = value.MoocNumber;
        DriverName = value.DriverName;
        Weight1 = value.Weight1;
        Weight2 = value.Weight2;
        NetWeight = value.NetWeight;
        Ttcp10WeightSnapshot = value.Ttcp10WeightSnapshot ?? ttcp10Fallback;
        IsOverweight = value.IsOverweight;
        OverweightAmount = value.OverweightAmount;
        OverweightSplitStepWeight = 0m;
        SessionStatusText = SessionStatusMapper.ToDisplayString(value.SessionStatus);
        OverweightResolutionText = OverweightResolutionStatusMapper.ToDisplayString(value.OverweightResolutionStatus);
        CustomerSummary = string.Join(" / ", lineItems.Select(x => x.CustomerName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        ProductSummary = BuildProductSummary(lineItems);
        NotesSummary = string.Join(
            " / ",
            lineItems.Select(x => x.Notes?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct());
        _isApplyingBaggedActualWeightOverrideState = true;
        try
        {
            UseActualWeightForBaggedCutOrders = value.UseActualWeightForBaggedCutOrders;
        }
        finally
        {
            _isApplyingBaggedActualWeightOverrideState = false;
        }
        _isApplyingNoLoadState = true;
        try
        {
            IsNoLoadMarked = value.IsNoLoad;
        }
        finally
        {
            _isApplyingNoLoadState = false;
        }
        SessionLines = new ObservableCollection<WeighingSessionLineRow>(lineItems.Select(x => new WeighingSessionLineRow(x)));
        NotifySessionActionStateChanged();
        OnPropertyChanged(nameof(ShowBaggedActualWeightOverride));
    }

    private void ClearSelectionDetails()
    {
        ClearPendingCapturedWeights();
        SessionNo = null;
        VehiclePlate = null;
        MoocNumber = null;
        DriverName = null;
        CustomerSummary = null;
        ProductSummary = null;
        NotesSummary = null;
        _isApplyingBaggedActualWeightOverrideState = true;
        try
        {
            UseActualWeightForBaggedCutOrders = false;
        }
        finally
        {
            _isApplyingBaggedActualWeightOverrideState = false;
        }
        _isApplyingNoLoadState = true;
        try
        {
            IsNoLoadMarked = false;
        }
        finally
        {
            _isApplyingNoLoadState = false;
        }
        Weight1 = null;
        Weight2 = null;
        NetWeight = null;
        Ttcp10WeightSnapshot = null;
        IsOverweight = false;
        OverweightAmount = 0m;
        OverweightSplitStepWeight = 0m;
        OverweightSplitTicket1WeightText = string.Empty;
        OverweightSplitTicket2WeightText = string.Empty;
        OverweightSplitModeText = string.Empty;
        OverweightSplitRandomFactorText = string.Empty;
        OverweightSplitValidationMessage = null;
        IsManualSplitOverride = false;
        IsOverweightSplitPlanValid = false;
        SessionStatusText = null;
        OverweightResolutionText = null;
        AppendCutOrdersWarningMessage = null;
        SessionLines = new ObservableCollection<WeighingSessionLineRow>();
        NotifySessionActionStateChanged();
        OnPropertyChanged(nameof(ShowBaggedActualWeightOverride));
    }

    private async Task LoadCameraPreviewAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<ICameraSettingsProvider>();
            var settings = await provider.GetForStationAsync("C2", CancellationToken.None);
            ApplyCameraPreviewSettings(settings);
            _ = StartCameraPreviewAsync(SelectedPreviewCameraCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Load camera preview settings failed");
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
            _logger?.LogError(ex, "Start preview for camera {CameraCode} failed", camera.CameraCode);
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
            if (e.StatusText.Contains("Không", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("Mất", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("dừng", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("Chưa", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("Lỗi", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("Khong", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("Mat", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("dung", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("Chua", StringComparison.OrdinalIgnoreCase)
                || e.StatusText.Contains("Loi", StringComparison.OrdinalIgnoreCase))
            {
                OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
            }
            OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
        });
    }

    private void OnCameraPreviewFrameReceived(object? sender, CameraPreviewFrameReceivedEventArgs e)
    {
        if (!_currentPreviewSessionId.HasValue)
        {
            return;
        }

        if (e.PreviewSessionId != _currentPreviewSessionId.Value)
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

    public void AttachCameraPreviewHost(IntPtr hostHandle, int width, int height)
    {
        _cameraPreviewService.AttachHostWindow(hostHandle, width, height);
        OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
    }

    public void ResizeCameraPreviewHost(int width, int height)
    {
        _cameraPreviewService.ResizeHostWindow(width, height);
    }

    public void DetachCameraPreviewHost()
    {
        _cameraPreviewService.DetachHostWindow();
        OnPropertyChanged(nameof(ShowCameraPreviewPlaceholder));
    }

    private void ResetPreviewRenderState()
    {
        _currentPreviewSessionId = null;
        _latestPendingPreviewFrame = null;
        Interlocked.Exchange(ref _lastRenderedPreviewSequence, 0);
        Interlocked.Exchange(ref _isPreviewUiUpdatePending, 0);
        CameraPreviewSource = null;
    }

    private bool CanCaptureWeight1() => SelectedSession?.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1;
    private bool CanCaptureWeight2() => SelectedSession?.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2;
    private bool CanSaveCapturedWeight() =>
        SelectedSession?.SessionStatus switch
        {
            WeighingSessionStatus.PENDING_WEIGHT1 => _pendingCapturedWeight1.HasValue,
            WeighingSessionStatus.PENDING_WEIGHT2 => _pendingCapturedWeight2.HasValue,
            _ => false
        };
    private bool CanOpenAllocation()
    {
        return ShowAllocationAction;
    }
    private bool CanOpenAppendCutOrders() =>
        SelectedSession?.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT1 or WeighingSessionStatus.PENDING_WEIGHT2;
    private bool CanShowOverweightHandling() =>
        SelectedSession != null
        && SelectedSession.TransactionType != TransactionType.INBOUND
        && SelectedSession.IsOverweight
        && SelectedSession.OverweightResolutionStatus == OverweightResolutionStatus.PENDING;
    private bool CanPrintWeighTicket() =>
        SelectedSession != null
        && SelectedSession.SessionStatus is WeighingSessionStatus.ALLOCATION_PENDING
            or WeighingSessionStatus.READY_TO_COMPLETE
            or WeighingSessionStatus.COMPLETED;
    private bool CanPrintDeliveryTicket() => SelectedSession != null && SelectedSession.SessionStatus is WeighingSessionStatus.READY_TO_COMPLETE or WeighingSessionStatus.COMPLETED;
    private bool CanConfirmOverweightSplit() => SelectedSession != null && IsOverweightSplitPlanValid;
    private bool CanMoveToOutYard() =>
        SelectedSession != null
        && SelectedSession.SessionStatus == WeighingSessionStatus.READY_TO_COMPLETE
        && (SelectedSession.OverweightResolutionStatus is OverweightResolutionStatus.NOT_APPLICABLE
            or OverweightResolutionStatus.SPLIT_CONFIRMED
            or OverweightResolutionStatus.NO_SPLIT_CONFIRMED);
    private bool CanMarkNoLoad() =>
        SelectedSession != null
        && !SelectedSession.IsNoLoad
        && SelectedSession.SessionStatus != WeighingSessionStatus.PENDING_WEIGHT1
        && SelectedSession.SessionStatus != WeighingSessionStatus.PENDING_WEIGHT2
        && SelectedSession.SessionStatus != WeighingSessionStatus.COMPLETED
        && SelectedSession.SessionStatus != WeighingSessionStatus.CANCELLED;
    private bool CanCancel() => SelectedSession != null && SelectedSession.SessionStatus != WeighingSessionStatus.COMPLETED && SelectedSession.SessionStatus != WeighingSessionStatus.CANCELLED;
    private bool CanShowRelatedTickets() =>
        SelectedSession != null
        && SelectedSession.SessionStatus is WeighingSessionStatus.ALLOCATION_PENDING
            or WeighingSessionStatus.READY_TO_COMPLETE
            or WeighingSessionStatus.COMPLETED;
    private bool CanSuggestOverweightSplitAgain() => SelectedSession != null && IsOverweightHandlingVisible;
    private bool CanViewImageHistory() => SelectedSession?.Weight1.HasValue == true;

    [RelayCommand(CanExecute = nameof(CanCaptureWeight1))]
    private Task CaptureWeight1Async()
    {
        var weight = ResolveWeightToCapture();
        if (weight <= 0)
        {
            _toastService.ShowWarning(UiText.Weighing.InvalidWeight1);
            return Task.CompletedTask;
        }

        _pendingCapturedWeight1 = weight;
        _pendingWeight1IsStable = IsStable;
        _pendingWeight1Mode = IsManualMode ? WeightMode.MANUAL : WeightMode.AUTO;
        Weight1 = weight;
        Weight2 = null;
        NetWeight = null;
        SaveCapturedWeightCommand.NotifyCanExecuteChanged();
        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanCaptureWeight2))]
    private Task CaptureWeight2Async()
    {
        var weight = ResolveWeightToCapture();
        if (weight <= 0)
        {
            _toastService.ShowWarning(UiText.Weighing.InvalidWeight2);
            return Task.CompletedTask;
        }

        var weight1ToCompare = _pendingCapturedWeight1 ?? SelectedSession?.Weight1 ?? Weight1;
        if (!weight1ToCompare.HasValue)
        {
            _toastService.ShowWarning(UiText.Weighing.InvalidWeight1);
            return Task.CompletedTask;
        }

        _pendingCapturedWeight2 = weight;
        _pendingWeight2IsStable = IsStable;
        _pendingWeight2Mode = IsManualMode ? WeightMode.MANUAL : WeightMode.AUTO;
        Weight2 = weight;
        NetWeight = CalculatePreviewNetWeight(weight1ToCompare.Value, weight, SelectedSession?.TransactionType ?? TransactionType.OUTBOUND);
        SaveCapturedWeightCommand.NotifyCanExecuteChanged();
        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanSaveCapturedWeight))]
    private async Task SaveCapturedWeightAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        if (SelectedSession.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
            && SelectedSession.TransactionType == TransactionType.INBOUND
            && SelectedSession.Weight1.HasValue
            && _pendingCapturedWeight2.HasValue
            && _pendingCapturedWeight2.Value > SelectedSession.Weight1.Value)
        {
            _toastService.ShowWarning("Cân lần 1 phải lớn hơn hoặc bằng cân lần 2 đối với phiếu nhập hàng.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();

            if (SelectedSession.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1)
            {
                var uc = scope.ServiceProvider.GetRequiredService<CaptureSessionWeight1UseCase>();
                await uc.ExecuteAsync(
                    new CaptureSessionWeightRequest(
                        SelectedSession.SessionId,
                        _pendingCapturedWeight1!.Value,
                        _pendingWeight1IsStable,
                        _pendingWeight1Mode),
                    CancellationToken.None);

                _toastService.ShowSuccess(UiText.Weighing.Weight1Saved);
            }
            else if (SelectedSession.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2)
            {
                var uc = scope.ServiceProvider.GetRequiredService<CaptureSessionWeight2UseCase>();
                try
                {
                    await uc.ExecuteAsync(
                        new CaptureSessionWeightRequest(
                            SelectedSession.SessionId,
                            _pendingCapturedWeight2!.Value,
                            _pendingWeight2IsStable,
                            _pendingWeight2Mode,
                            BypassTolerance: false),
                        CancellationToken.None);
                }
                catch (BaggedWeightToleranceExceededException ex)
                {
                    var confirmed = await _dialogService.ShowConfirmAsync(
                        "Cảnh báo vượt dung sai",
                        $"{ex.Message}\n\nBạn vẫn muốn tiếp tục lưu cân lần 2?",
                        "Vẫn lưu",
                        "Hủy");
                    if (!confirmed)
                    {
                        return;
                    }

                    await uc.ExecuteAsync(
                        new CaptureSessionWeightRequest(
                            SelectedSession.SessionId,
                            _pendingCapturedWeight2!.Value,
                            _pendingWeight2IsStable,
                            _pendingWeight2Mode,
                            BypassTolerance: true),
                        CancellationToken.None);
                }

                _toastService.ShowSuccess(UiText.Weighing.Weight2Saved);
            }

            ClearPendingCapturedWeights();
            await FocusSessionAsync(SelectedSession.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Save captured weight rejected by business validation");
            _toastService.ShowWarning(ex.Message);
            _isApplyingNoLoadState = true;
            try
            {
                IsNoLoadMarked = SelectedSession?.IsNoLoad == true;
            }
            finally
            {
                _isApplyingNoLoadState = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Save captured weight failed");
            _toastService.ShowError("Không thể lưu số cân. Vui lòng thử lại.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenAllocation))]
    private Task OpenAllocationAsync()
    {
        AllocationLines = new ObservableCollection<WeighingSessionLineRow>(SessionLines.Select(x => x.Clone()));
        IsAllocationVisible = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void CloseAllocation()
    {
        IsAllocationVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanOpenAppendCutOrders))]
    private async Task OpenAppendCutOrdersAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
        var candidates = await repo.GetIncomingListAsync(
            new IncomingVehicleListFilter(
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        var existingIds = SessionLines.Select(x => x.CutOrderId).ToHashSet();
        var filtered = candidates
            .Where(x => x.TransactionType == SelectedSession.TransactionType)
            .Where(x => !existingIds.Contains(x.CutOrderId))
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpCutOrderId)
            .Select(x => new IncomingVehicleSelectionItem(x))
            .ToList();

        if (filtered.Count == 0)
        {
            _toastService.ShowInfo("Không còn cắt lệnh phù hợp để thêm vào lượt cân này.");
            return;
        }

        AppendCutOrderCandidates = new ObservableCollection<IncomingVehicleSelectionItem>(filtered);
        RefreshAppendCutOrdersWarningMessage();
        IsAppendCutOrdersVisible = true;
    }

    [RelayCommand]
    private void CloseAppendCutOrders()
    {
        IsAppendCutOrdersVisible = false;
        AppendCutOrdersWarningMessage = null;
    }

    [RelayCommand]
    private async Task ConfirmAppendCutOrdersAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        var selectedIds = AppendCutOrderCandidates
            .Where(x => x.IsSelected)
            .Select(x => x.CutOrderId)
            .ToList();

        if (selectedIds.Count == 0)
        {
            _toastService.ShowWarning("Vui lòng chọn ít nhất một cắt lệnh để thêm.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<AppendCutOrdersToWeighingSessionUseCase>();
            await uc.ExecuteAsync(
                new AppendCutOrdersToWeighingSessionRequest(SelectedSession.SessionId, selectedIds),
                CancellationToken.None);

            _toastService.ShowSuccess("Đã thêm cắt lệnh vào lượt cân.");
            IsAppendCutOrdersVisible = false;
            AppendCutOrdersWarningMessage = null;
            await FocusSessionAsync(SelectedSession.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
            _isApplyingNoLoadState = true;
            try
            {
                IsNoLoadMarked = SelectedSession?.IsNoLoad == true;
            }
            finally
            {
                _isApplyingNoLoadState = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Append cut orders to weighing session failed");
            _toastService.ShowError("Không thể thêm cắt lệnh vào lượt cân. Vui lòng thử lại.");
        }
    }

    [RelayCommand]
    private void AllocateByPlan()
    {
        if (!NetWeight.HasValue || AllocationLines.Count == 0)
        {
            return;
        }

        using var _ = BeginPriorityAllocationUpdateScope();
        foreach (var row in AllocationLines)
        {
            row.IsPriority = false;
        }

        AllocateByPlanInternal(AllocationLines.ToList(), NetWeight.Value);
    }

    [RelayCommand]
    private async Task ConfirmAllocationAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<AllocateWeighingSessionUseCase>();
            await uc.ExecuteAsync(
                new AllocateWeighingSessionRequest(
                    SelectedSession.SessionId,
                    AllocationLines
                        .OrderByDescending(x => x.IsPriority)
                        .ThenBy(x => x.SequenceNo)
                        .Select(x => new AllocateWeighingSessionLineRequest(
                            x.SessionLineId,
                            x.ActualAllocatedWeight,
                            x.ActualAllocatedBagCount,
                            x.IsPriority))
                        .ToList()),
                CancellationToken.None);

            _toastService.ShowSuccess(UiText.Weighing.AllocationSaved);
            IsAllocationVisible = false;
            await FocusSessionAsync(SelectedSession.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
            _isApplyingNoLoadState = true;
            try
            {
                IsNoLoadMarked = SelectedSession?.IsNoLoad == true;
            }
            finally
            {
                _isApplyingNoLoadState = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Confirm allocation failed");
            _toastService.ShowError("Không thể lưu phân bổ thực giao. Vui lòng thử lại.");
        }
    }

    partial void OnAllocationLinesChanging(ObservableCollection<WeighingSessionLineRow> value)
    {
        RewireAllocationLineSubscriptions(AllocationLines, null);
    }

    partial void OnAllocationLinesChanged(ObservableCollection<WeighingSessionLineRow> value)
    {
        RewireAllocationLineSubscriptions(null, value);
    }

    private void RewireAllocationLineSubscriptions(
        ObservableCollection<WeighingSessionLineRow>? oldValue,
        ObservableCollection<WeighingSessionLineRow>? newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= AllocationLines_CollectionChanged;
            foreach (var item in oldValue)
            {
                item.PropertyChanged -= AllocationLine_PropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += AllocationLines_CollectionChanged;
            foreach (var item in newValue)
            {
                item.PropertyChanged += AllocationLine_PropertyChanged;
            }
        }
    }

    partial void OnAppendCutOrderCandidatesChanging(ObservableCollection<IncomingVehicleSelectionItem> value)
    {
        RewireAppendCutOrderCandidateSubscriptions(AppendCutOrderCandidates, null);
    }

    partial void OnAppendCutOrderCandidatesChanged(ObservableCollection<IncomingVehicleSelectionItem> value)
    {
        RewireAppendCutOrderCandidateSubscriptions(null, value);
        RefreshAppendCutOrdersWarningMessage();
    }

    private void RewireAppendCutOrderCandidateSubscriptions(
        ObservableCollection<IncomingVehicleSelectionItem>? oldValue,
        ObservableCollection<IncomingVehicleSelectionItem>? newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= AppendCutOrderCandidates_CollectionChanged;
            foreach (var item in oldValue)
            {
                item.PropertyChanged -= AppendCutOrderCandidate_PropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += AppendCutOrderCandidates_CollectionChanged;
            foreach (var item in newValue)
            {
                item.PropertyChanged += AppendCutOrderCandidate_PropertyChanged;
            }
        }
    }

    private void AppendCutOrderCandidates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (IncomingVehicleSelectionItem item in e.OldItems)
            {
                item.PropertyChanged -= AppendCutOrderCandidate_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (IncomingVehicleSelectionItem item in e.NewItems)
            {
                item.PropertyChanged += AppendCutOrderCandidate_PropertyChanged;
            }
        }

        RefreshAppendCutOrdersWarningMessage();
    }

    private void AppendCutOrderCandidate_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IncomingVehicleSelectionItem.IsSelected))
        {
            RefreshAppendCutOrdersWarningMessage();
        }
    }

    private void RefreshAppendCutOrdersWarningMessage()
    {
        if (SelectedSession == null || AppendCutOrderCandidates.Count == 0)
        {
            AppendCutOrdersWarningMessage = null;
            return;
        }

        var selectedItems = AppendCutOrderCandidates.Where(x => x.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            AppendCutOrdersWarningMessage = null;
            return;
        }

        var sessionVehiclePlate = NormalizeText(SelectedSession.VehiclePlate);
        var sessionMoocNumber = NormalizeText(SelectedSession.MoocNumber);
        var hasVehicleMismatch = selectedItems.Any(x =>
            !string.Equals(NormalizeText(x.VehiclePlate), sessionVehiclePlate, StringComparison.OrdinalIgnoreCase));
        var hasMoocMismatch = selectedItems.Any(x =>
            !string.Equals(NormalizeText(x.MoocNumber), sessionMoocNumber, StringComparison.OrdinalIgnoreCase));

        if (!hasVehicleMismatch && !hasMoocMismatch)
        {
            AppendCutOrdersWarningMessage = null;
            return;
        }

        var warnings = new List<string>();
        if (hasVehicleMismatch)
        {
            warnings.Add("khác số PTVC");
        }
        if (hasMoocMismatch)
        {
            warnings.Add("khác mooc");
        }

        AppendCutOrdersWarningMessage =
            $"Cảnh báo: có cắt lệnh được chọn {string.Join(" và ", warnings)} so với lượt cân hiện tại. Vui lòng xác nhận kỹ trước khi thêm.";
    }

    private async Task PersistBaggedActualWeightOverrideAsync(bool value)
    {
        if (SelectedSession == null)
        {
            return;
        }

        var previousValue = !value;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<SetWeighingSessionBaggedActualWeightOverrideUseCase>();
            await useCase.ExecuteAsync(SelectedSession.SessionId, value, CancellationToken.None);
            _toastService.ShowSuccess(value
                ? "Đã bật dùng KL thực cho hàng Bao ở lượt cân này."
                : "Đã tắt dùng KL thực cho hàng Bao ở lượt cân này.");
            await FocusSessionAsync(SelectedSession.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
            RevertBaggedActualWeightOverride(previousValue);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Update bagged actual weight override failed");
            _toastService.ShowError("Không thể cập nhật tùy chọn hàng Bao. Vui lòng thử lại.");
            RevertBaggedActualWeightOverride(previousValue);
        }
    }

    private void RevertBaggedActualWeightOverride(bool value)
    {
        _isApplyingBaggedActualWeightOverrideState = true;
        try
        {
            UseActualWeightForBaggedCutOrders = value;
        }
        finally
        {
            _isApplyingBaggedActualWeightOverrideState = false;
        }
    }

    private void NotifySessionActionStateChanged()
    {
        OnPropertyChanged(nameof(ShowAllocationAction));
        OnPropertyChanged(nameof(ShowOverweightHandlingAction));
        OnPropertyChanged(nameof(CanToggleNoLoad));
        OpenAllocationCommand.NotifyCanExecuteChanged();
        CaptureWeight1Command.NotifyCanExecuteChanged();
        CaptureWeight2Command.NotifyCanExecuteChanged();
        SaveCapturedWeightCommand.NotifyCanExecuteChanged();
        OpenAppendCutOrdersCommand.NotifyCanExecuteChanged();
        ShowOverweightHandlingCommand.NotifyCanExecuteChanged();
        PrintWeighTicketCommand.NotifyCanExecuteChanged();
        PrintDeliveryTicketCommand.NotifyCanExecuteChanged();
        MoveToOutYardCommand.NotifyCanExecuteChanged();
        MarkNoLoadCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ShowRelatedTicketsCommand.NotifyCanExecuteChanged();
        ViewImageHistoryCommand.NotifyCanExecuteChanged();
    }

    private void AllocationLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (WeighingSessionLineRow item in e.OldItems)
            {
                item.PropertyChanged -= AllocationLine_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (WeighingSessionLineRow item in e.NewItems)
            {
                item.PropertyChanged += AllocationLine_PropertyChanged;
            }
        }
    }

    private void AllocationLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingPriorityAllocation || sender is not WeighingSessionLineRow row || !NetWeight.HasValue)
        {
            return;
        }

        if (e.PropertyName == nameof(WeighingSessionLineRow.IsPriority))
        {
            if (row.IsPriority)
            {
                using var _ = BeginPriorityAllocationUpdateScope();
                foreach (var other in AllocationLines.Where(x => !ReferenceEquals(x, row) && x.IsPriority))
                {
                    other.IsPriority = false;
                }
            }

            ApplyPriorityAllocation(row, useCurrentPriorityWeight: false);
            return;
        }

        if (e.PropertyName == nameof(WeighingSessionLineRow.ActualAllocatedWeight) && row.IsPriority)
        {
            ApplyPriorityAllocation(row, useCurrentPriorityWeight: true);
        }
    }

    private void ApplyPriorityAllocation(WeighingSessionLineRow priorityRow, bool useCurrentPriorityWeight)
    {
        if (!NetWeight.HasValue)
        {
            return;
        }

        if (!priorityRow.IsPriority)
        {
            if (!AllocationLines.Any(x => x.IsPriority))
            {
                using var _ = BeginPriorityAllocationUpdateScope();
                AllocateByPlanInternal(AllocationLines.ToList(), NetWeight.Value);
            }

            return;
        }

        var totalWeight = NetWeight.Value;
        var desiredWeight = useCurrentPriorityWeight && priorityRow.ActualAllocatedWeight.HasValue
            ? priorityRow.ActualAllocatedWeight.Value
            : priorityRow.PlannedWeight ?? totalWeight;

        desiredWeight = decimal.Round(decimal.Clamp(desiredWeight, 0m, totalWeight), 3, MidpointRounding.AwayFromZero);
        var remainingWeight = decimal.Round(totalWeight - desiredWeight, 3, MidpointRounding.AwayFromZero);

        using var __ = BeginPriorityAllocationUpdateScope();
        priorityRow.ActualAllocatedWeight = desiredWeight;

        var remainingRows = AllocationLines.Where(x => !ReferenceEquals(x, priorityRow)).ToList();
        AllocateByPlanInternal(remainingRows, remainingWeight);
    }

    private static void AllocateByPlanInternal(IReadOnlyList<WeighingSessionLineRow> rows, decimal totalWeight)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var boundedTotal = decimal.Max(0m, totalWeight);
        var totalPlanned = rows.Sum(x => x.PlannedWeight ?? 0m);
        decimal allocated = 0m;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (i == rows.Count - 1)
            {
                row.ActualAllocatedWeight = decimal.Round(boundedTotal - allocated, 3, MidpointRounding.AwayFromZero);
            }
            else if (totalPlanned > 0)
            {
                var proportional = decimal.Round(boundedTotal * ((row.PlannedWeight ?? 0m) / totalPlanned), 3, MidpointRounding.AwayFromZero);
                row.ActualAllocatedWeight = proportional;
                allocated += proportional;
            }
            else
            {
                var even = decimal.Round(boundedTotal / rows.Count, 3, MidpointRounding.AwayFromZero);
                row.ActualAllocatedWeight = even;
                allocated += even;
            }
        }
    }

    private IDisposable BeginPriorityAllocationUpdateScope() => new PriorityAllocationUpdateScope(this);

    private static string NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private sealed class PriorityAllocationUpdateScope : IDisposable
    {
        private readonly WeighingViewModel _owner;

        public PriorityAllocationUpdateScope(WeighingViewModel owner)
        {
            _owner = owner;
            _owner._isUpdatingPriorityAllocation = true;
        }

        public void Dispose()
        {
            _owner._isUpdatingPriorityAllocation = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanShowOverweightHandling))]
    private async Task ShowOverweightHandlingAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        if (SelectedSession.TransactionType == TransactionType.INBOUND)
        {
            _toastService.ShowWarning("Phiếu nhập hàng không áp dụng tách tải.");
            return;
        }

        await RefreshOverweightPreviewAsync(isManualOverride: false, firstSplitWeight: null);
        IsOverweightHandlingVisible = true;
    }

    [RelayCommand]
    private void CloseOverweightHandling()
    {
        ResetOverweightHandlingState();
        IsOverweightHandlingVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmOverweightSplit))]
    private async Task ConfirmOverweightSplitAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        if (!TryParseOverweightSplitWeight(OverweightSplitTicket1WeightText, out var firstSplitWeight))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<ResolveWeighingSessionOverweightSplitUseCase>();
        await useCase.ExecuteAsync(
            new ResolveWeighingSessionOverweightSplitRequest(
                SelectedSession.SessionId,
                firstSplitWeight,
                IsManualSplitOverride),
            CancellationToken.None);
            _toastService.ShowSuccess("Đã cập nhật tách tải.");
        IsOverweightHandlingVisible = false;
        ResetOverweightHandlingState();
        await FocusSessionAsync(SelectedSession.SessionId);
    }

    [RelayCommand(CanExecute = nameof(CanSuggestOverweightSplitAgain))]
    private async Task SuggestOverweightSplitAgainAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        await RefreshOverweightPreviewAsync(isManualOverride: false, firstSplitWeight: null);
    }

    [RelayCommand]
    private async Task ConfirmOverweightNoSplitAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<ResolveWeighingSessionOverweightNoSplitUseCase>();
        await useCase.ExecuteAsync(SelectedSession.SessionId, CancellationToken.None);
        _toastService.ShowSuccess("Đã cập nhật tách tải.");
        IsOverweightHandlingVisible = false;
        ResetOverweightHandlingState();
        await FocusSessionAsync(SelectedSession.SessionId);
    }

    private void HandleOverweightSplitWeightEdited(string value, bool isFirstTicket)
    {
        if (_isUpdatingOverweightSplitInputs || !IsOverweightHandlingVisible)
        {
            return;
        }

        _ = RefreshManualOverweightPreviewAsync(value, isFirstTicket);
    }

    private async Task RefreshManualOverweightPreviewAsync(string value, bool isFirstTicket)
    {
        if (SelectedSession == null || !NetWeight.HasValue)
        {
            return;
        }

        if (!TryParseOverweightSplitWeight(value, out var editedWeight))
        {
            SetOverweightSplitValidation("Khối lượng tách phải là số hợp lệ.");
            return;
        }

        var firstSplitWeight = isFirstTicket
            ? editedWeight
            : decimal.Round(NetWeight.Value - editedWeight, 3, MidpointRounding.AwayFromZero);

        await RefreshOverweightPreviewAsync(
            isManualOverride: true,
            firstSplitWeight: firstSplitWeight,
            editedTicket: isFirstTicket ? 1 : 2);
    }

    private async Task RefreshOverweightPreviewAsync(bool isManualOverride, decimal? firstSplitWeight, int? editedTicket = null)
    {
        if (SelectedSession == null)
        {
            return;
        }

        var requestVersion = ++_overweightPreviewRequestVersion;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var previewUseCase = scope.ServiceProvider.GetRequiredService<PreviewWeighingSessionOverweightSplitUseCase>();
            var preview = await previewUseCase.ExecuteAsync(
                new PreviewWeighingSessionOverweightSplitRequest(
                    SelectedSession.SessionId,
                    firstSplitWeight,
                    isManualOverride),
                CancellationToken.None);

            if (requestVersion != _overweightPreviewRequestVersion)
            {
                return;
            }

            ApplyOverweightPreview(preview, editedTicket);
        }
        catch (InvalidOperationException ex)
        {
            if (requestVersion != _overweightPreviewRequestVersion)
            {
                return;
            }

            ApplyOverweightPreviewError(isManualOverride, firstSplitWeight, ex.Message, editedTicket);
        }
    }

    private void ApplyOverweightPreview(OverweightSplitPreviewDto preview, int? editedTicket = null)
    {
        _isUpdatingOverweightSplitInputs = true;
        try
        {
            OverweightSplitStepWeight = preview.OverweightSplitStepWeight;
            if (editedTicket != 1)
            {
                OverweightSplitTicket1WeightText = FormatOverweightSplitWeight(preview.SplitTicket1NetWeight);
            }

            if (editedTicket != 2)
            {
                OverweightSplitTicket2WeightText = FormatOverweightSplitWeight(preview.SplitTicket2NetWeight);
            }
        }
        finally
        {
            _isUpdatingOverweightSplitInputs = false;
        }

        IsManualSplitOverride = preview.IsManualOverride;
        OverweightSplitModeText = SplitModeDisplayMapper.ToDisplayString(preview.IsManualOverride);
        OverweightSplitRandomFactorText = preview.IsManualOverride
            ? "Tùy chỉnh tay"
            : preview.RandomSplitFactor.HasValue
                ? preview.RandomSplitFactor.Value.ToString("P2", System.Globalization.CultureInfo.InvariantCulture)
                : "--";
        OverweightPreviewGroups = new ObservableCollection<OverweightSplitPreviewGroupItem>(preview.Groups);
        OverweightPreviewLines = new ObservableCollection<OverweightSplitPreviewLineItem>(preview.Lines);
        SetOverweightSplitValidation(null);
    }

    private void ApplyOverweightPreviewError(bool isManualOverride, decimal? firstSplitWeight, string message, int? editedTicket = null)
    {
        IsManualSplitOverride = isManualOverride;
        OverweightSplitModeText = SplitModeDisplayMapper.ToDisplayString(isManualOverride);
        OverweightSplitRandomFactorText = isManualOverride ? "Tùy chỉnh tay" : "--";

        if (firstSplitWeight.HasValue)
        {
            _isUpdatingOverweightSplitInputs = true;
            try
            {
                if (editedTicket != 1)
                {
                    OverweightSplitTicket1WeightText = FormatOverweightSplitWeight(firstSplitWeight.Value);
                }

                if (NetWeight.HasValue)
                {
                    if (editedTicket != 2)
                    {
                        OverweightSplitTicket2WeightText = FormatOverweightSplitWeight(
                            decimal.Round(NetWeight.Value - firstSplitWeight.Value, 3, MidpointRounding.AwayFromZero));
                    }
                }

            }
            finally
            {
                _isUpdatingOverweightSplitInputs = false;
            }
        }

        SetOverweightSplitValidation(message);
        OverweightPreviewGroups = new ObservableCollection<OverweightSplitPreviewGroupItem>();
        OverweightPreviewLines = new ObservableCollection<OverweightSplitPreviewLineItem>();
    }

    private void SetOverweightSplitValidation(string? validationMessage)
    {
        OverweightSplitValidationMessage = validationMessage;
        IsOverweightSplitPlanValid = string.IsNullOrWhiteSpace(validationMessage);
        ConfirmOverweightSplitCommand.NotifyCanExecuteChanged();
        SuggestOverweightSplitAgainCommand.NotifyCanExecuteChanged();
    }

    private bool TryParseOverweightSplitWeight(string? value, out decimal parsedWeight)
    {
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out parsedWeight))
        {
            parsedWeight = decimal.Round(parsedWeight, 3, MidpointRounding.AwayFromZero);
            return true;
        }

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out parsedWeight))
        {
            parsedWeight = decimal.Round(parsedWeight, 3, MidpointRounding.AwayFromZero);
            return true;
        }

        parsedWeight = 0m;
        return false;
    }

    private static string FormatOverweightSplitWeight(decimal weight)
    {
        return decimal.Round(weight, 0, MidpointRounding.AwayFromZero)
            .ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
    }

    private void ResetOverweightHandlingState()
    {
        OverweightSplitStepWeight = 0m;
        OverweightSplitTicket1WeightText = string.Empty;
        OverweightSplitTicket2WeightText = string.Empty;
        OverweightSplitModeText = string.Empty;
        OverweightSplitRandomFactorText = string.Empty;
        OverweightPreviewGroups = new ObservableCollection<OverweightSplitPreviewGroupItem>();
        OverweightPreviewLines = new ObservableCollection<OverweightSplitPreviewLineItem>();
        IsManualSplitOverride = false;
        OverweightSplitValidationMessage = null;
        IsOverweightSplitPlanValid = false;
        ConfirmOverweightSplitCommand.NotifyCanExecuteChanged();
        SuggestOverweightSplitAgainCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPrintWeighTicket))]
    private async Task PrintWeighTicketAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        await ExecutePrintFlowAsync(PrintDocumentKind.WeighTicket, SelectedSession.SessionId, "phiếu cân");
    }

    [RelayCommand(CanExecute = nameof(CanPrintDeliveryTicket))]
    private async Task PrintDeliveryTicketAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        await ExecutePrintFlowAsync(PrintDocumentKind.DeliveryTicket, SelectedSession.SessionId, "phiáº¿u giao nháº­n");
    }

    [RelayCommand(CanExecute = nameof(CanMoveToOutYard))]
    private async Task MoveToOutYardAsync()
    {
        if (SelectedSession == null)
        {
            _toastService.ShowWarning(UiText.Weighing.MoveOutNotReady);
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            UiText.Weighing.MoveOutConfirmTitle,
            UiText.Weighing.MoveOutConfirmMessage,
            UiText.Weighing.MoveOutConfirmAction,
            UiText.Common.No);

        if (!confirmed)
        {
            _isApplyingNoLoadState = true;
            try
            {
                IsNoLoadMarked = SelectedSession.IsNoLoad;
            }
            finally
            {
                _isApplyingNoLoadState = false;
            }
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<CompleteWeighingSessionUseCase>();
            await uc.ExecuteAsync(SelectedSession.SessionId, CancellationToken.None);
            _toastService.ShowSuccess(UiText.Weighing.MoveOutSuccess);
            await LoadSessionsAsync();
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MoveToOutYard failed");
            _toastService.ShowError(UiText.Weighing.MoveOutError);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMarkNoLoad))]
    private async Task MarkNoLoadAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            "Xác nhận không lấy hàng",
            "Lượt cân đã chọn sẽ được chuyển thẳng sang danh sách xe ra với trạng thái không lấy hàng. Tiếp tục?",
            "Chuyển xe ra",
            UiText.Common.No);

        if (!confirmed)
        {
            _isApplyingNoLoadState = true;
            try
            {
                IsNoLoadMarked = SelectedSession.IsNoLoad;
            }
            finally
            {
                _isApplyingNoLoadState = false;
            }
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<MarkWeighingSessionNoLoadUseCase>();
            await uc.ExecuteAsync(new MarkWeighingSessionNoLoadRequest(SelectedSession.SessionId), CancellationToken.None);
            _toastService.ShowSuccess("Đã chuyển xe sang danh sách xe ra theo luồng không lấy hàng.");
            await LoadSessionsAsync();
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
            _isApplyingNoLoadState = true;
            try
            {
                IsNoLoadMarked = SelectedSession?.IsNoLoad == true;
            }
            finally
            {
                _isApplyingNoLoadState = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MarkNoLoad failed");
            _isApplyingNoLoadState = true;
            try
            {
                IsNoLoadMarked = SelectedSession?.IsNoLoad == true;
            }
            finally
            {
                _isApplyingNoLoadState = false;
            }
            _toastService.ShowError("Không thể chuyển xe ra theo luồng không lấy hàng.");
        }
    }

    private async Task MarkNoLoadFromCheckboxAsync()
    {
        if (!CanMarkNoLoad())
        {
            _toastService.ShowWarning("Phải lưu cân lần 2 trước khi đánh dấu không lấy hàng.");
            _isApplyingNoLoadState = true;
            try
            {
                IsNoLoadMarked = false;
            }
            finally
            {
                _isApplyingNoLoadState = false;
            }

            return;
        }

        await MarkNoLoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            UiText.Weighing.CancelTitle,
            UiText.Weighing.CancelMessage,
            UiText.Weighing.CancelConfirm,
            UiText.Weighing.Close);

        if (!confirmed)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var uc = scope.ServiceProvider.GetRequiredService<CancelWeighingSessionUseCase>();
        await uc.ExecuteAsync(new CancelWeighingSessionRequest(SelectedSession.SessionId), CancellationToken.None);
        var exportCutOrderId = await ResolveExportCutOrderIdAsync(scope.ServiceProvider, SelectedSession.SessionId, CancellationToken.None);
        _toastService.ShowSuccess(UiText.Weighing.CancelSuccess);
        await LoadSessionsAsync();
        if (exportCutOrderId.HasValue)
        {
            NavigateToExportWeighingRequested?.Invoke(exportCutOrderId.Value);
        }
    }

    private static async Task<Guid?> ResolveExportCutOrderIdAsync(IServiceProvider serviceProvider, Guid sessionId, CancellationToken ct)
    {
        var regRepo = serviceProvider.GetRequiredService<ICutOrderRepository>();
        var registrations = await regRepo.GetByWeighingSessionIdAsync(sessionId, ct);
        return registrations.FirstOrDefault(x => x.IsExportScale)?.Id;
    }

    [RelayCommand(CanExecute = nameof(CanShowRelatedTickets))]
    private async Task ShowRelatedTicketsAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var weighRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var weighTickets = await weighRepo.GetByWeighingSessionIdAsync(SelectedSession.SessionId, CancellationToken.None);
        var deliveryTickets = await deliveryRepo.GetByWeighingSessionIdAsync(SelectedSession.SessionId, CancellationToken.None);

        RelatedTickets = new ObservableCollection<RelatedDocumentListItem>(
            weighTickets.Select(ticket => new RelatedDocumentListItem(
                    UiText.Weighing.RelatedWeighTicket,
                    ticket.TicketNo,
                    null,
                    ticket.RecordRole,
                    ticket.SplitSequence,
                    ticket.Weight1,
                    ticket.Weight2,
                    ticket.NetWeight,
                    ticket.CreatedAt))
                .Concat(deliveryTickets.Select(ticket => new RelatedDocumentListItem(
                    UiText.Weighing.RelatedDeliveryTicket,
                    null,
                    ticket.DeliveryNo,
                    ticket.RecordRole,
                    ticket.SplitSequence,
                    null,
                    null,
                    ticket.AllocatedWeight,
                    ticket.CreatedAt)))
                .OrderBy(x => x.DocumentType)
                .ThenBy(x => x.RecordRole == DeliveryTicketRecordRoles.Master ? 0 : 1)
                .ThenBy(x => x.SplitSequence ?? byte.MaxValue)
                .ThenBy(x => x.CreatedAt));

        IsRelatedTicketsVisible = true;
    }

    [RelayCommand]
    private void CloseRelatedTickets()
    {
        IsRelatedTicketsVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanViewImageHistory))]
    private async Task ViewImageHistoryAsync()
    {
        if (SelectedSession == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var imageRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionImageRepository>();
            var images = await imageRepo.GetByWeighingSessionIdAsync(SelectedSession.SessionId, CancellationToken.None);

            if (images == null || images.Count == 0)
            {
                await _dialogService.ShowWarningAsync("Thông báo", "Không tìm thấy ảnh chụp lịch sử cho lượt cân này.");
                return;
            }

            await _dialogService.ShowCustomDialogAsync<CameraImageHistoryViewModel, bool>(
                new CameraImageHistoryViewModel(images, SelectedSession.VehiclePlate ?? string.Empty, _toastService));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to view image history for session {SessionId}", SelectedSession.SessionId);
            _toastService.ShowError("Có lỗi xảy ra khi tải danh sách ảnh chụp.");
        }
    }

    private async Task ExecutePrintFlowAsync(PrintDocumentKind kind, Guid sessionId, string displayName)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = await LoadPrintContextAsync(scope, sessionId);
            if (context == null)
            {
                _toastService.ShowWarning(string.Format(UiText.Weighing.NoPrintableDocumentFormat, displayName));
                return;
            }

            var templateProvider = scope.ServiceProvider.GetRequiredService<IPrintTemplateProvider>();
            var printerDiscovery = scope.ServiceProvider.GetRequiredService<IPrinterDiscoveryService>();
            var printService = scope.ServiceProvider.GetRequiredService<IPrintService>();
            var renderer = scope.ServiceProvider.GetRequiredService<PrintOverlayRenderer>();
            var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var template = await templateProvider.GetTemplateAsync(kind, CancellationToken.None);
            var profiles = await templateProvider.GetProfilesAsync(kind, CancellationToken.None);
            var preview = BuildPrintBatchPreview(scope, context, kind);
            if (preview.Pages.Count == 0)
            {
                _toastService.ShowWarning(string.Format(UiText.Weighing.NoPrintableDocumentFormat, displayName));
                return;
            }

            var printerKey = kind == PrintDocumentKind.WeighTicket
                ? AppConfigKeys.DefaultWeighTicketPrinter
                : AppConfigKeys.DefaultDeliveryTicketPrinter;
            var preferredPrinter = await appConfig.GetValueAsync(printerKey, CancellationToken.None);
            var printers = PrinterSelectionHelper.ApplyPreferredPrinter(
                printerDiscovery.GetInstalledPrinters(),
                preferredPrinter);

            var dialogVm = new PrintOptionsDialogViewModel(
                kind == PrintDocumentKind.WeighTicket ? UiText.Weighing.PrintDialogWeighTicket : UiText.Weighing.PrintDialogDeliveryTicket,
                template,
                preview,
                profiles,
                printers,
                renderer,
                templateProvider,
                false,
                kind == PrintDocumentKind.WeighTicket
                    ? PrintCopyCountHelper.ResolveDefaultWeighTicketCopyCount(context.RegistrationsById.Values)
                    : 1);

            var printOptions = await _dialogService.ShowCustomDialogAsync<PrintOptionsDialogViewModel, PrintOptionsModel>(dialogVm);
            if (printOptions == null)
            {
                return;
            }

            var result = await printService.PrintAsync(template, preview, printOptions, CancellationToken.None);
            await PersistPrintResultAsync(scope, context, kind, result);

            if (result.HasFailures)
            {
                _toastService.ShowError(string.Format(UiText.Weighing.PrintErrorFormat, displayName));
                return;
            }

            _toastService.ShowSuccess(string.Format(UiText.Weighing.PrintSuccessFormat, displayName));
            await FocusSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Print flow failed");
            _toastService.ShowError(string.Format(UiText.Weighing.PrintErrorFormat, displayName));
        }
    }

    private async Task<SessionPrintContext?> LoadPrintContextAsync(IServiceScope scope, Guid sessionId)
    {
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
        var regRepo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
        var weighRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        var session = await sessionRepo.GetByIdAsync(sessionId, CancellationToken.None);
        if (session == null)
        {
            return null;
        }

        var lines = await sessionRepo.GetLinesBySessionIdAsync(sessionId, CancellationToken.None);
        var registrations = await regRepo.GetByWeighingSessionIdAsync(sessionId, CancellationToken.None);

        foreach (var registration in registrations.Where(x => string.IsNullOrWhiteSpace(x.ProductType) && !string.IsNullOrWhiteSpace(x.ProductCode)))
        {
            var product = await productRepo.GetByCodeAsync(registration.ProductCode!, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(product?.ProductType))
            {
                registration.ProductType = product.ProductType;
            }
        }

        var registrationsById = registrations.ToDictionary(x => x.Id);
        var weighTickets = await weighRepo.GetByWeighingSessionIdAsync(sessionId, CancellationToken.None);
        var deliveryTickets = await deliveryRepo.GetByWeighingSessionIdAsync(sessionId, CancellationToken.None);
        var vehicle = await vehicleRepo.GetByPlateAndMoocAsync(session.VehiclePlate, session.MoocNumber ?? string.Empty, CancellationToken.None)
            ?? (await vehicleRepo.GetByPlateAsync(session.VehiclePlate, CancellationToken.None)).FirstOrDefault();

        return new SessionPrintContext(session, lines, registrationsById, weighTickets, deliveryTickets, vehicle);
    }

    private PrintBatchPreviewModel BuildPrintBatchPreview(IServiceScope scope, SessionPrintContext context, PrintDocumentKind kind)
    {
        var printedAtLocal = _clock.NowLocal;
        var splitConfirmed = context.MasterSession.OverweightResolutionStatus == OverweightResolutionStatus.SPLIT_CONFIRMED;

        if (kind == PrintDocumentKind.WeighTicket)
        {
            var composer = scope.ServiceProvider.GetRequiredService<IWeighTicketPrintComposer>();
            if (context.RegistrationsById.Count == 0)
            {
                return new PrintBatchPreviewModel { Kind = kind, Title = UiText.Weighing.PrintPreviewWeigh, Pages = [] };
            }

            var ticketsToPrint = splitConfirmed
                ? context.WeighTickets
                    .Where(x => (x.RecordRole == WeighTicketRecordRoles.MasterSession || x.RecordRole == WeighTicketRecordRoles.SplitDerived) && !x.IsDeleted)
                    .OrderBy(x => x.RecordRole == WeighTicketRecordRoles.MasterSession ? 0 : 1)
                    .ThenBy(x => x.SplitSequence)
                    .ToList()
                : context.WeighTickets
                    .Where(x => (x.RecordRole == WeighTicketRecordRoles.MasterSession || x.RecordRole == WeighTicketRecordRoles.CutOrderDerived) && !x.IsDeleted)
                    .OrderBy(x => x.RecordRole == WeighTicketRecordRoles.MasterSession ? 0 : 1)
                    .ThenBy(x => x.CreatedAt)
                    .ToList();

            var weighPages = ticketsToPrint
                .Select(ticket =>
                {
                    var registration = context.RegistrationsById.GetValueOrDefault(ticket.CutOrderId);
                    if (registration == null || ticket.RecordRole == WeighTicketRecordRoles.MasterSession)
                    {
                        var primaryRegistration = context.RegistrationsById.Values.OrderBy(x => x.CreatedAt).First();
                        registration = BuildDeliveryMasterRegistration(context, primaryRegistration);
                    }
                    var page = composer.Compose(registration, ticket, context.Vehicle, printedAtLocal, _currentUserContext.DisplayName);
                    if (ticket.RecordRole == WeighTicketRecordRoles.MasterSession)
                    {
                        page.PreviewGroupKey = "weigh-master";
                        page.PreviewGroupName = "Phiếu tổng";
                    }
                    else if (ticket.RecordRole == WeighTicketRecordRoles.CutOrderDerived)
                    {
                        page.PreviewGroupKey = $"weigh-cutorder-{registration.Id:N}";
                        page.PreviewGroupName = $"Phiếu cắt lệnh {registration.ErpCutOrderId}";
                    }
                    else if (ticket.RecordRole == WeighTicketRecordRoles.SplitDerived)
                    {
                        page.PreviewGroupKey = "weigh-split";
                        page.PreviewGroupName = "Phiếu tách tải";
                    }

                    return (PrintPreviewPageModel)page;
                })
                .ToList();

            return new PrintBatchPreviewModel
            {
                Kind = kind,
                Title = splitConfirmed ? "In phiếu cân tách tải" : UiText.Weighing.PrintPreviewWeighMaster,
                Pages = weighPages
            };
        }

        var deliveryComposer = scope.ServiceProvider.GetRequiredService<IDeliveryTicketPrintComposer>();
        var pages = new List<PrintPreviewPageModel>();
        var deliveryTicketsToPrint = splitConfirmed
            ? context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived && !x.IsDeleted)
                .OrderBy(x => x.SplitSequence).ThenBy(x => x.CreatedAt)
            : context.DeliveryTickets.Where(x =>
                    (x.RecordRole == DeliveryTicketRecordRoles.Master || x.RecordRole == DeliveryTicketRecordRoles.Normal)
                    && !x.IsDeleted)
                .OrderBy(x => x.RecordRole == DeliveryTicketRecordRoles.Master ? 0 : 1)
                .ThenBy(x => x.WeighingSessionLineId).ThenBy(x => x.CreatedAt);

        foreach (var ticket in deliveryTicketsToPrint)
        {
            CutOrder? registration;
            WeighingSessionLine? line;
            if (ticket.RecordRole == DeliveryTicketRecordRoles.Master)
            {
                var primaryLine = context.Lines.OrderBy(x => x.SequenceNo).FirstOrDefault();
                if (primaryLine == null || !context.RegistrationsById.TryGetValue(primaryLine.CutOrderId, out var primaryRegistration))
                {
                    continue;
                }

                registration = BuildDeliveryMasterRegistration(context, primaryRegistration);
                line = BuildDeliveryMasterLine(context);
            }
            else
            {
                if (!ticket.WeighingSessionLineId.HasValue)
                {
                    continue;
                }

                line = context.Lines.FirstOrDefault(x => x.Id == ticket.WeighingSessionLineId.Value);
                if (line == null || !context.RegistrationsById.TryGetValue(line.CutOrderId, out registration))
                {
                    continue;
                }
            }

            var relatedWeighTicket = splitConfirmed
                ? context.WeighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived && x.SplitGroupId == ticket.SplitGroupId && !x.IsDeleted)
                : context.WeighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession && !x.IsDeleted);

            var page = deliveryComposer.Compose(registration!, ticket, relatedWeighTicket, line, context.Vehicle, printedAtLocal, _currentUserContext.DisplayName);
            if (ticket.RecordRole == DeliveryTicketRecordRoles.Master)
            {
                page.PreviewGroupKey = "delivery-master";
                page.PreviewGroupName = "Phiếu tổng";
            }
            else if (ticket.RecordRole == DeliveryTicketRecordRoles.Normal)
            {
                page.PreviewGroupKey = $"delivery-cutorder-{registration!.Id:N}";
                page.PreviewGroupName = $"Phiếu cắt lệnh {registration.ErpCutOrderId}";
            }
            else if (ticket.RecordRole == DeliveryTicketRecordRoles.SplitDerived)
            {
                page.PreviewGroupKey = "delivery-split";
                page.PreviewGroupName = "Phiếu tách tải";
            }

            pages.Add(page);
        }

        return new PrintBatchPreviewModel
        {
            Kind = kind,
            Title = UiText.Weighing.PrintPreviewDelivery,
            Pages = pages
        };
    }

    private async Task PersistPrintResultAsync(IServiceScope scope, SessionPrintContext context, PrintDocumentKind kind, PrintExecutionResult result)
    {
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
        var weighRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var now = _clock.NowLocal;
        var splitConfirmed = context.MasterSession.OverweightResolutionStatus == OverweightResolutionStatus.SPLIT_CONFIRMED;

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async innerCt =>
        {
            if (kind == PrintDocumentKind.WeighTicket)
            {
                var ticketsToPersist = splitConfirmed
                    ? context.WeighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived && !x.IsDeleted)
                    : context.WeighTickets.Where(x =>
                        (x.RecordRole == WeighTicketRecordRoles.MasterSession || x.RecordRole == WeighTicketRecordRoles.CutOrderDerived)
                        && !x.IsDeleted);

                foreach (var weighTicket in ticketsToPersist)
                {
                    var ticketResult = result.Documents.FirstOrDefault(x => x.DocumentId == weighTicket.Id);
                    if (ticketResult == null || !ticketResult.Success)
                    {
                        continue;
                    }

                    weighTicket.IsPrinted = true;
                    weighTicket.LastPrintedAt = now;
                    weighTicket.LastPrintError = null;
                    weighTicket.UpdatedAt = now;
                    weighTicket.UpdatedBy = _currentUserContext.Username;
                    await weighRepo.UpdateAsync(weighTicket, innerCt);
                }

                var masterTicketPrinted = context.WeighTickets.Any(x =>
                    x.RecordRole == WeighTicketRecordRoles.MasterSession
                    && !x.IsDeleted
                    && result.Documents.Any(d => d.DocumentId == x.Id && d.Success));

                if (!splitConfirmed && masterTicketPrinted)
                {
                    context.MasterSession.HasPrintedMasterWeighTicket = true;
                    context.MasterSession.UpdatedAt = now;
                    context.MasterSession.UpdatedBy = _currentUserContext.Username;
                    await sessionRepo.UpdateAsync(context.MasterSession, innerCt);
                }

                return;
            }

            foreach (var deliveryTicket in context.DeliveryTickets.Where(x => !x.IsDeleted))
            {
                var ticketResult = result.Documents.FirstOrDefault(x => x.DocumentId == deliveryTicket.Id);
                if (ticketResult == null || !ticketResult.Success)
                {
                    continue;
                }

                deliveryTicket.IsPrinted = true;
                deliveryTicket.LastPrintedAt = now;
                deliveryTicket.LastPrintError = null;
                deliveryTicket.UpdatedAt = now;
                deliveryTicket.UpdatedBy = _currentUserContext.Username;
                await deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            foreach (var line in context.Lines)
            {
                var relevantTickets = splitConfirmed
                    ? context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived && x.WeighingSessionLineId == line.Id && !x.IsDeleted).ToList()
                    : context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.Normal && x.WeighingSessionLineId == line.Id && !x.IsDeleted).ToList();

                line.HasPrintedDeliveryTicket = relevantTickets.Count > 0 && relevantTickets.All(x => x.IsPrinted);
                line.UpdatedAt = now;
                line.UpdatedBy = _currentUserContext.Username;
                await sessionRepo.UpdateLineAsync(line, innerCt);
            }
        }, CancellationToken.None);
    }

    private decimal ResolveWeightToCapture()
    {
        return decimal.Round(CurrentWeight, 3, MidpointRounding.AwayFromZero);
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

    private static string? BuildProductSummary(IEnumerable<WeighingSessionLineItem> lineItems)
    {
        var productGroups = lineItems
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
            .GroupBy(
                x => new
                {
                    ProductCode = (x.ProductCode ?? string.Empty).Trim(),
                    ProductName = (x.ProductName ?? string.Empty).Trim()
                })
            .Select(group => new
            {
                group.Key.ProductName,
                PlannedWeight = group.Sum(x => x.PlannedWeight ?? 0m)
            })
            .ToList();

        if (productGroups.Count == 0)
        {
            return null;
        }

        return string.Join(
            " / ",
            productGroups.Select(x => $"{x.ProductName} ({x.PlannedWeight:N0})"));
    }

    private static CutOrder BuildDeliveryMasterRegistration(SessionPrintContext context, CutOrder primaryRegistration)
    {
        var registrations = context.RegistrationsById.Values.OrderBy(x => x.CreatedAt).ToList();
        var distinctCustomerNames = registrations.Select(x => x.CustomerName?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var distinctProductNames = registrations.Select(x => x.ProductName?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var distinctNotes = registrations.Select(x => x.Notes?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var distinctMarkets = registrations.Select(x => x.Market?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var distinctConsumptionPlaces = registrations.Select(x => x.ConsumptionPlace?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new CutOrder
        {
            Id = primaryRegistration.Id,
            ErpCutOrderId = primaryRegistration.ErpCutOrderId,
            OrderCode = primaryRegistration.OrderCode,
            CustomerCode = primaryRegistration.CustomerCode,
            CustomerName = distinctCustomerNames.Count == 1 ? distinctCustomerNames[0] : "Nhiều khách hàng",
            ProductCode = primaryRegistration.ProductCode,
            ProductName = distinctProductNames.Count switch
            {
                0 => primaryRegistration.ProductName,
                1 => distinctProductNames[0],
                _ => string.Join(" / ", distinctProductNames)
            },
            ProductType = primaryRegistration.ProductType,
            Market = distinctMarkets.Count == 0 ? null : string.Join(" / ", distinctMarkets),
            ConsumptionPlace = distinctConsumptionPlaces.Count == 0 ? null : string.Join(" / ", distinctConsumptionPlaces),
            LoadingPlace = primaryRegistration.LoadingPlace,
            LotNo = primaryRegistration.LotNo,
            SealNo = primaryRegistration.SealNo,
            PlannedWeight = context.Lines.Sum(x => x.PlannedWeight ?? 0m),
            BagCount = context.Lines.Sum(x => x.PlannedBagCount ?? 0),
            VehiclePlate = context.MasterSession.VehiclePlate,
            MoocNumber = context.MasterSession.MoocNumber,
            Notes = distinctNotes.Count == 0 ? null : string.Join(" / ", distinctNotes)
        };
    }

    private static WeighingSessionLine BuildDeliveryMasterLine(SessionPrintContext context)
    {
        return new WeighingSessionLine
        {
            Id = Guid.Empty,
            WeighingSessionId = context.MasterSession.Id,
            CutOrderId = context.RegistrationsById.Values.OrderBy(x => x.CreatedAt).First().Id,
            SequenceNo = 0,
            PlannedWeight = context.Lines.Sum(x => x.PlannedWeight ?? 0m),
            PlannedBagCount = context.Lines.Sum(x => x.PlannedBagCount ?? 0),
            ActualAllocatedWeight = context.MasterSession.NetWeight,
            ActualAllocatedBagCount = context.Lines.Sum(x => x.ActualAllocatedBagCount ?? 0),
            LineStatus = WeighingSessionLineStatus.ALLOCATED
        };
    }

    private void ClearPendingCapturedWeights()
    {
        _pendingCapturedWeight1 = null;
        _pendingCapturedWeight2 = null;
        _pendingWeight1IsStable = false;
        _pendingWeight2IsStable = false;
        _pendingWeight1Mode = WeightMode.AUTO;
        _pendingWeight2Mode = WeightMode.AUTO;
        SaveCapturedWeightCommand.NotifyCanExecuteChanged();
    }

    private static decimal CalculatePreviewNetWeight(decimal weight1, decimal weight2, TransactionType transactionType)
    {
        if (transactionType == TransactionType.INBOUND && weight1 < weight2)
        {
            return 0m;
        }

        return Math.Abs(weight1 - weight2);
    }

    private async Task AttachDeviceAsync()
    {
        using var perfScope = Helpers.PerformanceLogger.Track("Weighing.AttachDevice");
        if (_scaleDevice.IsConnected)
        {
            DeviceStatusText = UiText.Weighing.ActiveConnection;
            IsDeviceConnected = true;
            return;
        }

        await _scaleDevice.ConnectAsync(CancellationToken.None);
        await _scaleDevice.StartAsync(CancellationToken.None);
        DeviceStatusText = _scaleDevice.IsConnected ? UiText.Weighing.ActiveConnection : UiText.Weighing.LostConnection;
        IsDeviceConnected = _scaleDevice.IsConnected;
    }

    private void EnsureDeviceAttachStarted()
    {
        if (_hasStartedDeviceAttach)
        {
            return;
        }

        _hasStartedDeviceAttach = true;
        _ = AttachDeviceInBackgroundAsync();
    }

    private async Task AttachDeviceInBackgroundAsync()
    {
        try
        {
            await AttachDeviceAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Background device attach failed");
            DeviceStatusText = UiText.Weighing.LostConnection;
            IsDeviceConnected = false;
        }
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
        LatestScaleReadingSnapshot? latestReading;
        bool deviceConnected;

        lock (_scaleReadingLock)
        {
            latestReading = _pendingScaleReading;
            deviceConnected = _pendingScaleDeviceConnected;
            _pendingScaleReading = null;
        }

        if (latestReading == null)
        {
            return;
        }

        if (IsAutoMode)
        {
            CurrentWeight = latestReading.Weight;
            IsStable = latestReading.IsStable;
            StabilityText = latestReading.IsStable ? "ỔN ĐỊNH" : "CHƯA ỔN ĐỊNH";
            StabilityBrush = latestReading.IsStable ? StableBrush : UnstableBrush;
        }
        else
        {
            IsStable = true;
            StabilityText = "CÂN TAY";
            StabilityBrush = StableBrush;
        }

        IsDeviceConnected = deviceConnected;
        DeviceStatusText = deviceConnected ? UiText.Weighing.ActiveConnection : UiText.Weighing.LostConnection;
    }

    public void Dispose()
    {
        _scaleUiTimer.Stop();
        _scaleUiTimer.Tick -= OnScaleUiTimerTick;
        _scaleDevice.WeightReceived -= OnWeightReceived;
        _cameraPreviewService.StatusChanged -= OnCameraPreviewStatusChanged;
        _cameraPreviewService.FrameReceived -= OnCameraPreviewFrameReceived;
        ResetPreviewRenderState();
        try
        {
            _ = _cameraPreviewService.StopPreviewAsync();
        }
        catch
        {
            // ignore preview stop failures during dispose
        }
    }

    private sealed record SessionPrintContext(
        WeighingSession MasterSession,
        IReadOnlyList<WeighingSessionLine> Lines,
        IReadOnlyDictionary<Guid, CutOrder> RegistrationsById,
        IReadOnlyList<WeighTicket> WeighTickets,
        IReadOnlyList<DeliveryTicket> DeliveryTickets,
        Vehicle? Vehicle);
}

public partial class WeighingSessionLineRow : ObservableObject
{
    private const decimal DefaultBagWeightKg = 50m;

    public WeighingSessionLineRow(WeighingSessionLineItem item)
    {
        SessionLineId = item.SessionLineId;
        CutOrderId = item.CutOrderId;
        SequenceNo = item.SequenceNo;
        ErpCutOrderId = item.ErpCutOrderId;
        CustomerName = item.CustomerName;
        DistributorName = item.DistributorName;
        ProductCode = item.ProductCode;
        ProductName = item.ProductName;
        ProductType = item.ProductType;
        Notes = item.Notes;
        PlannedWeight = item.PlannedWeight;

        var isBagged = string.Equals(StationApp.Domain.Constants.ProductTypes.Normalize(ProductType), StationApp.Domain.Constants.ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
        PlannedBagCount = isBagged ? item.PlannedBagCount : null;
        ActualAllocatedWeight = item.ActualAllocatedWeight;
        ActualAllocatedBagCount = isBagged ? item.ActualAllocatedBagCount : null;

        LineStatus = item.LineStatus;
        HasPrintedDeliveryTicket = item.HasPrintedDeliveryTicket;
    }

    [ObservableProperty] private decimal? _actualAllocatedWeight;
    [ObservableProperty] private int? _actualAllocatedBagCount;
    [ObservableProperty] private bool _isPriority;

    public Guid SessionLineId { get; }
    public Guid CutOrderId { get; }
    public int SequenceNo { get; }
    public string? ErpCutOrderId { get; }
    public string? CustomerName { get; }
    public string? DistributorName { get; }
    public string? ProductCode { get; }
    public string? ProductName { get; }
    public string? ProductType { get; }
    public string? Notes { get; }
    public decimal? PlannedWeight { get; }
    public int? PlannedBagCount { get; }
    public WeighingSessionLineStatus LineStatus { get; }
    public bool HasPrintedDeliveryTicket { get; set; }

    partial void OnActualAllocatedWeightChanged(decimal? value)
    {
        var isBagged = string.Equals(StationApp.Domain.Constants.ProductTypes.Normalize(ProductType), StationApp.Domain.Constants.ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
        if (!isBagged)
        {
            ActualAllocatedBagCount = null;
            return;
        }

        if (!value.HasValue || value <= 0)
        {
            ActualAllocatedBagCount = null;
            return;
        }

        ActualAllocatedBagCount = (int)decimal.Floor(value.Value / DefaultBagWeightKg);
    }

    public WeighingSessionLineRow Clone()
    {
        return new WeighingSessionLineRow(new WeighingSessionLineItem(
            SessionLineId,
            CutOrderId,
            SequenceNo,
            ErpCutOrderId,
            CustomerName,
            DistributorName,
            ProductCode,
            ProductName,
            PlannedWeight,
            PlannedBagCount,
            ActualAllocatedWeight,
            ActualAllocatedBagCount,
            LineStatus,
            HasPrintedDeliveryTicket,
            ProductType,
            Notes));
    }
}


