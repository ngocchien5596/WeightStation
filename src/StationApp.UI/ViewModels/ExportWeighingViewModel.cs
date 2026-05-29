using System.Collections.ObjectModel;
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
using StationApp.UI.Helpers;
using StationApp.UI.Printing;
using StationApp.UI.Resources;
using StationApp.UI.Services;
using StationApp.Application.UseCases.MasterData;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels;

public partial class ExportWeighingViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly ICameraPreviewService _cameraPreviewService;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ExportWeighingViewModel>? _logger;
    private readonly Dispatcher _uiDispatcher;
    private readonly object _scaleReadingLock = new();
    private readonly DispatcherTimer _scaleUiTimer;
    private CameraSystemSettings? _cameraSettings;
    private Guid? _currentPreviewSessionId;
    private long _lastRenderedPreviewSequence;
    private CameraPreviewFrameReceivedEventArgs? _latestPendingPreviewFrame;
    private int _isPreviewUiUpdatePending;
    private bool _hasStartedDeviceAttach;
    private LatestScaleReadingSnapshot? _pendingScaleReading;
    private bool _pendingScaleDeviceConnected;
    private decimal? _pendingCapturedWeight1;
    private decimal? _pendingCapturedWeight2;
    private bool _pendingWeight1IsStable;
    private bool _pendingWeight2IsStable;
    private WeightMode _pendingWeight1Mode = WeightMode.AUTO;
    private WeightMode _pendingWeight2Mode = WeightMode.AUTO;
    private int _vehicleMasterLookupVersion;

    private const string AutoModeText = "TỰ ĐỘNG";
    private const string ManualModeText = "CÂN TAY";
    private static readonly SolidColorBrush StableBrush = new(Color.FromRgb(46, 213, 115));
    private static readonly SolidColorBrush UnstableBrush = new(Colors.Orange);

    public event Action<Guid>? NavigateToWeighingRequested;

    public AutocompleteInputViewModel TripVehiclePlateInput { get; }
    public AutocompleteInputViewModel TripMoocInput { get; }
    public AutocompleteInputViewModel TripDriverInput { get; }

    [ObservableProperty] private ObservableCollection<ExportScaleCutOrderListItem> _cutOrders = new();
    [ObservableProperty] private ObservableCollection<ExportVehicleTripListItem> _trips = new();
    [ObservableProperty] private ExportScaleCutOrderListItem? _selectedCutOrder;
    [ObservableProperty] private ExportVehicleTripListItem? _selectedTrip;
    [ObservableProperty] private string? _searchErpCutOrderId;
    [ObservableProperty] private string? _searchVehiclePlate;
    [ObservableProperty] private string? _newVehiclePlate;
    [ObservableProperty] private string? _newMoocNumber;
    [ObservableProperty] private string? _newDriverName;
    [ObservableProperty] private decimal? _vehicleTtcpWeight;
    [ObservableProperty] private decimal? _vehicleTtcp10Weight;
    [ObservableProperty] private string? _vehicleRegistrationNo;
    [ObservableProperty] private DateTime? _vehicleRegistrationExpiryDate;
    [ObservableProperty] private string? _moocRegistrationNo;
    [ObservableProperty] private DateTime? _moocRegistrationExpiryDate;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private decimal _currentWeight;
    [ObservableProperty] private bool _isStable;
    [ObservableProperty] private string _stabilityText = "CHƯA ỔN ĐỊNH";
    [ObservableProperty] private SolidColorBrush _stabilityBrush = UnstableBrush;
    [ObservableProperty] private string _currentCaptureMode = AutoModeText;
    [ObservableProperty] private decimal? _weight1;
    [ObservableProperty] private decimal? _weight2;
    [ObservableProperty] private decimal? _netWeight;
    [ObservableProperty] private string _deviceStatusText = UiText.Weighing.InitializingDevice;
    [ObservableProperty] private bool _isDeviceConnected;
    [ObservableProperty] private string _cameraPreviewStatusText = "Chưa cấu hình camera";
    [ObservableProperty] private ImageSource? _cameraPreviewSource;
    [ObservableProperty] private string _selectedPreviewCameraCode = "CAM1";
    [ObservableProperty] private bool _isCameraPreviewAvailable;
    [ObservableProperty] private bool _isCamera1PreviewAvailable;
    [ObservableProperty] private bool _isCamera2PreviewAvailable;
    [ObservableProperty] private string _camera1PreviewName = "Camera 1";
    [ObservableProperty] private string _camera2PreviewName = "Camera 2";

    public bool CanCreateTrip =>
        SelectedCutOrder != null
        && SelectedTrip == null
        && !SelectedCutOrder.IsFinalized
        && !IsLoading
        && !string.IsNullOrWhiteSpace(NewVehiclePlate);

    public bool CanOpenTrip => SelectedTrip != null && !IsLoading;

    public bool CanFinalize =>
        SelectedCutOrder != null
        && !SelectedCutOrder.IsFinalized
        && !IsLoading
        && Trips.Any(x => x.SessionStatus is WeighingSessionStatus.READY_TO_COMPLETE or WeighingSessionStatus.COMPLETED);

    public bool ShowCamera1Selector => IsCameraPreviewAvailable && IsCamera1PreviewAvailable;
    public bool ShowCamera2Selector => IsCameraPreviewAvailable && IsCamera2PreviewAvailable;
    public bool ShowCameraPreviewPlaceholder =>
        !IsCameraPreviewAvailable
        || !_cameraPreviewService.IsPreviewRunning;
    public bool IsAutoMode => CurrentCaptureMode == AutoModeText;
    public bool IsManualMode => CurrentCaptureMode == ManualModeText;
    public bool CanUseManualMode => StationAuthorization.CanUseManualWeighing(_currentUserContext.RoleCode);
    public bool IsTripFormEditable => SelectedCutOrder != null && SelectedTrip == null && !SelectedCutOrder.IsFinalized && !IsLoading;
    public bool IsTripFormReadOnly => !IsTripFormEditable;
    public bool IsVehicleRegistrationExpired => IsExpiredDate(VehicleRegistrationExpiryDate);
    public bool IsMoocRegistrationExpired => IsExpiredDate(MoocRegistrationExpiryDate);
    public bool CanPrintWeighTicket =>
        SelectedTrip != null
        && SelectedTrip.SessionStatus is WeighingSessionStatus.ALLOCATION_PENDING
            or WeighingSessionStatus.READY_TO_COMPLETE
            or WeighingSessionStatus.COMPLETED
        && !IsLoading;
    public bool CanPrintDeliveryTicket =>
        SelectedTrip != null
        && SelectedTrip.SessionStatus is WeighingSessionStatus.READY_TO_COMPLETE
            or WeighingSessionStatus.COMPLETED
        && !IsLoading;
    public bool CanViewImageHistory => SelectedTrip?.Weight1.HasValue == true && !IsLoading;

    private bool CanCaptureWeight1() =>
        SelectedTrip?.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1
        && !IsLoading;

    private bool CanCaptureWeight2() =>
        SelectedTrip?.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
        && !IsLoading;

    private bool CanSaveCapturedWeight() =>
        !IsLoading
        && (SelectedTrip?.SessionStatus switch
        {
            WeighingSessionStatus.PENDING_WEIGHT1 => _pendingCapturedWeight1.HasValue,
            WeighingSessionStatus.PENDING_WEIGHT2 => _pendingCapturedWeight2.HasValue,
            _ => false
        });

    public ExportWeighingViewModel(
        IServiceScopeFactory scopeFactory,
        IScaleDevice scaleDevice,
        ICameraPreviewService cameraPreviewService,
        IToastService toastService,
        IDialogService dialogService,
        ICurrentUserContext currentUserContext,
        ILogger<ExportWeighingViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
        _cameraPreviewService = cameraPreviewService;
        _toastService = toastService;
        _dialogService = dialogService;
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

        TripVehiclePlateInput = CreateAutocompleteField(AutocompleteFieldType.Vehicle, 1, ApplyVehicleSelection);
        TripMoocInput = CreateAutocompleteField(AutocompleteFieldType.Mooc, 1, ApplyMoocSelection);
        TripDriverInput = CreateAutocompleteField(AutocompleteFieldType.Driver, 2, ApplyDriverSelection);

        WireTextState(TripVehiclePlateInput, value => NewVehiclePlate = value);
        WireTextState(TripMoocInput, value => NewMoocNumber = value);
        WireTextState(TripDriverInput, value => NewDriverName = value);
    }

    public async Task InitializeAsync()
    {
        EnsureDeviceAttachStarted();
        await LoadCutOrdersAsync();
        await LoadCameraPreviewAsync();
    }

    public async Task FocusCutOrderAsync(Guid cutOrderId)
    {
        await LoadCutOrdersAsync(cutOrderId);
        await LoadCameraPreviewAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        SearchErpCutOrderId = null;
        SearchVehiclePlate = null;
        SelectedCutOrder = null;
        SelectedTrip = null;
        ClearTripFormFields();
        await LoadCutOrdersAsync();
        await LoadCameraPreviewAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCreateTrip))]
    private async Task CreateTripAsync()
    {
        if (SelectedCutOrder == null)
        {
            return;
        }

        if (!ValidateCreateTripForm())
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<CreateExportVehicleSessionUseCase>();
            var result = await uc.ExecuteAsync(
                new CreateExportVehicleSessionRequest(
                    SelectedCutOrder.CutOrderId,
                    ResolveTripVehiclePlate()!,
                    NewMoocNumber,
                    NewDriverName,
                    VehicleTtcpWeight,
                    VehicleRegistrationNo,
                    VehicleRegistrationExpiryDate,
                    MoocRegistrationNo,
                    MoocRegistrationExpiryDate),
                CancellationToken.None);

            _toastService.ShowSuccess("\u0110\u00e3 t\u1ea1o chuy\u1ebfn xe xu\u1ea5t kh\u1ea9u.");
            await LoadCutOrdersAsync(SelectedCutOrder.CutOrderId);
            if (SelectedCutOrder != null)
            {
                await LoadTripsAsync(SelectedCutOrder.CutOrderId, result.SessionId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Create export vehicle session failed");
            _toastService.ShowError(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenTrip))]
    private void OpenTrip()
    {
        if (SelectedTrip == null)
        {
            return;
        }

        NavigateToWeighingRequested?.Invoke(SelectedTrip.SessionId);
    }

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

        var weight1ToCompare = _pendingCapturedWeight1 ?? SelectedTrip?.Weight1 ?? Weight1;
        if (!weight1ToCompare.HasValue)
        {
            _toastService.ShowWarning(UiText.Weighing.InvalidWeight1);
            return Task.CompletedTask;
        }

        _pendingCapturedWeight2 = weight;
        _pendingWeight2IsStable = IsStable;
        _pendingWeight2Mode = IsManualMode ? WeightMode.MANUAL : WeightMode.AUTO;
        Weight2 = weight;
        NetWeight = CalculatePreviewNetWeight(weight1ToCompare.Value, weight);
        SaveCapturedWeightCommand.NotifyCanExecuteChanged();
        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanSaveCapturedWeight))]
    private async Task SaveCapturedWeightAsync()
    {
        if (SelectedTrip == null)
        {
            return;
        }

        var selectedTripId = SelectedTrip.SessionId;
        var selectedCutOrderId = SelectedCutOrder?.CutOrderId;

        try
        {
            using var scope = _scopeFactory.CreateScope();

            if (SelectedTrip.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1)
            {
                var uc = scope.ServiceProvider.GetRequiredService<CaptureSessionWeight1UseCase>();
                await uc.ExecuteAsync(
                    new CaptureSessionWeightRequest(
                        selectedTripId,
                        _pendingCapturedWeight1!.Value,
                        _pendingWeight1IsStable,
                        _pendingWeight1Mode),
                    CancellationToken.None);

                _toastService.ShowSuccess(UiText.Weighing.Weight1Saved);
            }
            else if (SelectedTrip.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2)
            {
                if (!await ConfirmNegativeRemainingWeightAsync())
                {
                    return;
                }

                var uc = scope.ServiceProvider.GetRequiredService<CaptureSessionWeight2UseCase>();
                await uc.ExecuteAsync(
                    new CaptureSessionWeightRequest(
                        selectedTripId,
                        _pendingCapturedWeight2!.Value,
                        _pendingWeight2IsStable,
                        _pendingWeight2Mode),
                    CancellationToken.None);

                _toastService.ShowSuccess(UiText.Weighing.Weight2Saved);
            }

            ClearPendingCapturedWeights();
            if (selectedCutOrderId.HasValue)
            {
                await LoadCutOrdersAsync(selectedCutOrderId.Value);
                await LoadTripsAsync(selectedCutOrderId.Value, selectedTripId);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Save export trip weight rejected by business validation");
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Save export trip weight failed");
            _toastService.ShowError("Không thể lưu số cân. Vui lòng thử lại.");
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    private async Task<bool> ConfirmNegativeRemainingWeightAsync()
    {
        if (SelectedCutOrder == null || SelectedTrip == null || !_pendingCapturedWeight2.HasValue)
        {
            return true;
        }

        var weight1 = _pendingCapturedWeight1 ?? SelectedTrip.Weight1 ?? Weight1;
        if (!weight1.HasValue)
        {
            return true;
        }

        var currentTripNetWeight = CalculatePreviewNetWeight(weight1.Value, _pendingCapturedWeight2.Value);
        var remainingAfterSave = SelectedCutOrder.RemainingWeight - currentTripNetWeight;
        if (remainingAfterSave >= 0m)
        {
            return true;
        }

        var remainingText = SelectedCutOrder.RemainingWeight.ToString("N0");
        var tripNetText = currentTripNetWeight.ToString("N0");
        var exceededText = Math.Abs(remainingAfterSave).ToString("N0");

        return await _dialogService.ShowConfirmAsync(
            "Cảnh báo vượt số lượng còn lại",
            $"Số lượng còn lại của cắt lệnh chỉ còn {remainingText} kg, nhưng NET của chuyến này là {tripNetText} kg. Nếu lưu cân lần 2, số lượng còn lại sẽ âm {exceededText} kg. Bạn vẫn muốn tiếp tục lưu?",
            "Vẫn lưu",
            "Hủy");
    }

    [RelayCommand(CanExecute = nameof(CanFinalize))]
    private async Task FinalizeAsync()
    {
        if (SelectedCutOrder == null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            "Ch\u1ed1t c\u1eaft l\u1ec7nh xu\u1ea5t kh\u1ea9u",
            "Sau khi ch\u1ed1t, c\u1eaft l\u1ec7nh s\u1ebd kh\u00f4ng t\u1ea1o th\u00eam chuy\u1ebfn xe. Ti\u1ebfp t\u1ee5c?",
            "Ch\u1ed1t",
            "Kh\u00f4ng");
        if (!confirmed)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<FinalizeExportCutOrderUseCase>();
            await uc.ExecuteAsync(new FinalizeExportCutOrderRequest(SelectedCutOrder.CutOrderId), CancellationToken.None);
            _toastService.ShowSuccess("\u0110\u00e3 ch\u1ed1t c\u1eaft l\u1ec7nh xu\u1ea5t kh\u1ea9u.");
            await LoadCutOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Finalize export cut order failed");
            _toastService.ShowError(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrintWeighTicket))]
    private async Task PrintWeighTicketAsync()
    {
        if (SelectedTrip == null)
        {
            return;
        }

        await ExecutePrintFlowAsync(PrintDocumentKind.WeighTicket, SelectedTrip.SessionId, "phiếu cân");
    }

    [RelayCommand(CanExecute = nameof(CanPrintDeliveryTicket))]
    private async Task PrintDeliveryTicketAsync()
    {
        if (SelectedTrip == null)
        {
            return;
        }

        await ExecutePrintFlowAsync(PrintDocumentKind.DeliveryTicket, SelectedTrip.SessionId, "phiếu giao nhận");
    }

    [RelayCommand(CanExecute = nameof(CanViewImageHistory))]
    private async Task ViewImageHistoryAsync()
    {
        if (SelectedTrip == null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var imageRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionImageRepository>();
            var images = await imageRepo.GetByWeighingSessionIdAsync(SelectedTrip.SessionId, CancellationToken.None);

            if (images == null || images.Count == 0)
            {
                await _dialogService.ShowWarningAsync("Thông báo", "Không tìm thấy ảnh chụp lịch sử cho lượt cân này.");
                return;
            }

            await _dialogService.ShowCustomDialogAsync<CameraImageHistoryViewModel, bool>(
                new CameraImageHistoryViewModel(images, SelectedTrip.VehiclePlate ?? string.Empty, _toastService));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to view image history for export session {SessionId}", SelectedTrip.SessionId);
            _toastService.ShowError("Có lỗi xảy ra khi tải danh sách ảnh chụp.");
        }
    }

    private async Task LoadCutOrdersAsync(Guid? preserveCutOrderId = null)
    {
        try
        {
            IsLoading = true;
            var selectedId = preserveCutOrderId ?? SelectedCutOrder?.CutOrderId;
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
            var list = await repo.GetActiveExportScaleCutOrdersAsync(
                new ExportScaleCutOrderFilter(
                    SearchErpCutOrderId,
                    SearchVehiclePlate,
                    null,
                    null,
                    null),
                CancellationToken.None);

            CutOrders = new ObservableCollection<ExportScaleCutOrderListItem>(list);
            SelectedCutOrder = selectedId.HasValue
                ? CutOrders.FirstOrDefault(x => x.CutOrderId == selectedId.Value)
                : null;

            if (SelectedCutOrder == null)
            {
                Trips.Clear();
                SelectedTrip = null;
                ClearTripFormFields();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Load export scale cut orders failed");
            _toastService.ShowError("Kh\u00f4ng th\u1ec3 t\u1ea3i danh s\u00e1ch c\u1eaft l\u1ec7nh xu\u1ea5t kh\u1ea9u.");
        }
        finally
        {
            IsLoading = false;
            RefreshCommandStates();
        }
    }

    private async Task LoadTripsAsync(Guid cutOrderId, Guid? selectedTripId = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
            var list = await repo.GetExportVehicleTripsAsync(cutOrderId, CancellationToken.None);
            Trips = new ObservableCollection<ExportVehicleTripListItem>(list);
            SelectedTrip = selectedTripId.HasValue
                ? Trips.FirstOrDefault(x => x.SessionId == selectedTripId.Value)
                : null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Load export vehicle trips failed");
            _toastService.ShowError("Kh\u00f4ng th\u1ec3 t\u1ea3i danh s\u00e1ch chuy\u1ebfn xe.");
        }
        finally
        {
            RefreshCommandStates();
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
                false);

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
            await ReloadTripsAndReselectAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Export print flow failed");
            _toastService.ShowError(string.Format(UiText.Weighing.PrintErrorFormat, displayName));
        }
    }

    private string? ResolveTripVehiclePlate()
        => string.IsNullOrWhiteSpace(NewVehiclePlate)
            ? null
            : NewVehiclePlate.Trim();

    private decimal ResolveWeightToCapture()
    {
        return decimal.Round(CurrentWeight, 3, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculatePreviewNetWeight(decimal weight1, decimal weight2)
    {
        return Math.Abs(weight1 - weight2);
    }

    private void ApplySelectedTripWeights()
    {
        Weight1 = SelectedTrip?.Weight1;
        Weight2 = SelectedTrip?.Weight2;
        NetWeight = SelectedTrip?.NetWeight;
        ClearPendingCapturedWeights();
    }

    private void ClearTripFormFields()
    {
        Interlocked.Increment(ref _vehicleMasterLookupVersion);
        SetNewVehiclePlate(null);
        SetNewMoocNumber(null);
        SetNewDriverName(null);
        ApplyVehicleMasterInfo(null);
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

    private async Task ReloadTripsAndReselectAsync(Guid sessionId)
    {
        var cutOrderId = SelectedCutOrder?.CutOrderId;
        if (!cutOrderId.HasValue)
        {
            return;
        }

        await LoadCutOrdersAsync(cutOrderId.Value);
        if (SelectedCutOrder != null)
        {
            await LoadTripsAsync(SelectedCutOrder.CutOrderId, sessionId);
        }
    }

    private async Task AttachDeviceAsync()
    {
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
            _logger?.LogWarning(ex, "Background export weighing device attach failed");
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

    private async Task LoadCameraPreviewAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<ICameraSettingsProvider>();
            var settings = await provider.GetForStationAsync("C6", CancellationToken.None);
            ApplyCameraPreviewSettings(settings);
            _ = StartCameraPreviewAsync(SelectedPreviewCameraCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Load export camera preview settings failed");
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
            _logger?.LogError(ex, "Start export preview for camera {CameraCode} failed", camera.CameraCode);
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
            if (!string.Equals(CameraPreviewStatusText, e.StatusText, StringComparison.Ordinal))
            {
                CameraPreviewStatusText = e.StatusText;
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

    private void ResetPreviewRenderState()
    {
        _currentPreviewSessionId = null;
        _latestPendingPreviewFrame = null;
        Interlocked.Exchange(ref _lastRenderedPreviewSequence, 0);
        Interlocked.Exchange(ref _isPreviewUiUpdatePending, 0);
        CameraPreviewSource = null;
    }

    private async Task RefreshVehicleMasterInfoAsync()
    {
        var lookupVersion = Interlocked.Increment(ref _vehicleMasterLookupVersion);
        var vehiclePlate = NewVehiclePlate?.Trim();
        var moocNumber = NewMoocNumber?.Trim();

        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            if (lookupVersion == Volatile.Read(ref _vehicleMasterLookupVersion))
            {
                ApplyVehicleMasterInfo(null);
            }

            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            StationApp.Domain.Entities.Vehicle? vehicle = null;
            if (!string.IsNullOrWhiteSpace(moocNumber))
            {
                vehicle = await vehicleRepo.GetByPlateAndMoocAsync(vehiclePlate, moocNumber, CancellationToken.None);
            }

            vehicle ??= (await vehicleRepo.GetByPlateAsync(vehiclePlate, CancellationToken.None)).FirstOrDefault();

            if (lookupVersion == Volatile.Read(ref _vehicleMasterLookupVersion))
            {
                ApplyVehicleMasterInfo(vehicle);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Load export vehicle master info failed for plate {VehiclePlate}, mooc {MoocNumber}", vehiclePlate, moocNumber);
            if (lookupVersion == Volatile.Read(ref _vehicleMasterLookupVersion))
            {
                ApplyVehicleMasterInfo(null);
            }
        }
    }

    private void ApplyVehicleMasterInfo(StationApp.Domain.Entities.Vehicle? vehicle)
    {
        VehicleTtcpWeight = vehicle?.TtcpWeight;
        VehicleTtcp10Weight = vehicle?.TtcpWeight is > 0m
            ? decimal.Round(vehicle.TtcpWeight.Value * 1.10m, 3, MidpointRounding.AwayFromZero)
            : null;
        VehicleRegistrationNo = vehicle?.VehicleRegistrationNo;
        VehicleRegistrationExpiryDate = vehicle?.VehicleRegistrationExpiryDate;
        MoocRegistrationNo = vehicle?.MoocRegistrationNo;
        MoocRegistrationExpiryDate = vehicle?.MoocRegistrationExpiryDate;
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
        var printedAtLocal = DateTime.Now;
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
                : context.WeighTickets
                    .Where(x => (x.RecordRole == WeighTicketRecordRoles.MasterSession || x.RecordRole == WeighTicketRecordRoles.CutOrderDerived) && !x.IsDeleted)
                    .OrderBy(x => x.RecordRole == WeighTicketRecordRoles.MasterSession ? 0 : 1)
                    .ThenBy(x => x.CreatedAt);

            var weighPages = ticketsToPrint
                .Select(ticket =>
                {
                    var registration = context.RegistrationsById.GetValueOrDefault(ticket.CutOrderId)
                        ?? context.RegistrationsById.Values.OrderBy(x => x.CreatedAt).First();
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
            ? context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived && !x.IsDeleted).OrderBy(x => x.SplitSequence).ThenBy(x => x.CreatedAt)
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
        var now = DateTime.Now;
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
            VehiclePlate = primaryRegistration.VehiclePlate,
            MoocNumber = primaryRegistration.MoocNumber,
            ReceiverName = primaryRegistration.ReceiverName,
            CustomerCode = primaryRegistration.CustomerCode,
            CustomerName = distinctCustomerNames.Count switch
            {
                0 => primaryRegistration.CustomerName,
                1 => distinctCustomerNames[0],
                _ => string.Join(" / ", distinctCustomerNames)
            },
            ProductCode = primaryRegistration.ProductCode,
            ProductName = distinctProductNames.Count switch
            {
                0 => primaryRegistration.ProductName,
                1 => distinctProductNames[0],
                _ => string.Join(" / ", distinctProductNames)
            },
            PlannedWeight = context.Lines.Sum(x => x.PlannedWeight ?? 0m),
            BagCount = context.Lines.Sum(x => x.PlannedBagCount ?? 0),
            Notes = distinctNotes.Count switch
            {
                0 => primaryRegistration.Notes,
                1 => distinctNotes[0],
                _ => string.Join(" / ", distinctNotes)
            },
            Market = distinctMarkets.Count switch
            {
                0 => primaryRegistration.Market,
                1 => distinctMarkets[0],
                _ => string.Join(" / ", distinctMarkets)
            },
            ConsumptionPlace = distinctConsumptionPlaces.Count switch
            {
                0 => primaryRegistration.ConsumptionPlace,
                1 => distinctConsumptionPlaces[0],
                _ => string.Join(" / ", distinctConsumptionPlaces)
            },
            TransactionType = primaryRegistration.TransactionType,
            TransportMethod = primaryRegistration.TransportMethod,
            ProductType = primaryRegistration.ProductType,
            CreatedAt = primaryRegistration.CreatedAt,
            CreatedBy = primaryRegistration.CreatedBy,
            UpdatedAt = primaryRegistration.UpdatedAt,
            UpdatedBy = primaryRegistration.UpdatedBy
        };
    }

    private static WeighingSessionLine BuildDeliveryMasterLine(SessionPrintContext context)
    {
        var primaryLine = context.Lines.OrderBy(x => x.SequenceNo).First();
        return new WeighingSessionLine
        {
            Id = primaryLine.Id,
            WeighingSessionId = primaryLine.WeighingSessionId,
            CutOrderId = primaryLine.CutOrderId,
            SequenceNo = 1,
            CustomerCode = primaryLine.CustomerCode,
            CustomerName = primaryLine.CustomerName,
            DistributorName = primaryLine.DistributorName,
            ProductCode = primaryLine.ProductCode,
            ProductName = primaryLine.ProductName,
            PlannedWeight = context.Lines.Sum(x => x.PlannedWeight ?? 0m),
            PlannedBagCount = context.Lines.Sum(x => x.PlannedBagCount ?? 0),
            ActualAllocatedWeight = context.Lines.Sum(x => x.ActualAllocatedWeight ?? 0m),
            ActualAllocatedBagCount = context.Lines.Sum(x => x.ActualAllocatedBagCount ?? 0),
            LineStatus = primaryLine.LineStatus,
            HasPrintedDeliveryTicket = context.Lines.All(x => x.HasPrintedDeliveryTicket),
            CreatedAt = primaryLine.CreatedAt,
            CreatedBy = primaryLine.CreatedBy,
            UpdatedAt = primaryLine.UpdatedAt,
            UpdatedBy = primaryLine.UpdatedBy
        };
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
        return await service.SearchAsync(new AutocompleteQuery(fieldType, keyword), ct);
    }

    private static void WireTextState(AutocompleteInputViewModel state, Action<string?> setter)
    {
        state.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AutocompleteInputViewModel.Text))
            {
                setter(state.Text);
            }
        };
    }

    private void ApplyVehicleSelection(AutocompleteItem item)
    {
        SetNewVehiclePlate(item.Value);
        if (!string.IsNullOrWhiteSpace(item.Payload?.MoocNumber))
        {
            SetNewMoocNumber(item.Payload.MoocNumber);
        }

        if (!string.IsNullOrWhiteSpace(item.Payload?.DriverName))
        {
            SetNewDriverName(item.Payload.DriverName);
        }

        ApplyVehiclePayload(item.Payload);
    }

    private void ApplyMoocSelection(AutocompleteItem item)
    {
        SetNewMoocNumber(item.Value);
        ApplyVehiclePayload(item.Payload);
    }

    private void ApplyDriverSelection(AutocompleteItem item)
    {
        SetNewDriverName(item.Value);
        ApplyVehiclePayload(item.Payload);
    }

    private void ApplyVehiclePayload(AutocompletePayload? payload)
    {
        if (payload == null)
        {
            _ = RefreshVehicleMasterInfoAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(payload.VehiclePlate))
        {
            SetNewVehiclePlate(payload.VehiclePlate);
        }

        if (!string.IsNullOrWhiteSpace(payload.MoocNumber))
        {
            SetNewMoocNumber(payload.MoocNumber);
        }

        if (!string.IsNullOrWhiteSpace(payload.DriverName))
        {
            SetNewDriverName(payload.DriverName);
        }

        VehicleTtcpWeight = payload.TtcpWeight;
        VehicleRegistrationNo = payload.VehicleRegistrationNo;
        VehicleRegistrationExpiryDate = payload.VehicleRegistrationExpiryDate;
        MoocRegistrationNo = payload.MoocRegistrationNo;
        MoocRegistrationExpiryDate = payload.MoocRegistrationExpiryDate;

        if (payload.TtcpWeight == null && !string.IsNullOrWhiteSpace(NewVehiclePlate))
        {
            _ = RefreshVehicleMasterInfoAsync();
        }
    }

    private void SetNewVehiclePlate(string? value)
    {
        NewVehiclePlate = value;
        TripVehiclePlateInput.SetText(value);
    }

    private void SetNewMoocNumber(string? value)
    {
        NewMoocNumber = value;
        TripMoocInput.SetText(value);
    }

    private void SetNewDriverName(string? value)
    {
        NewDriverName = value;
        TripDriverInput.SetText(value);
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanCreateTrip));
        OnPropertyChanged(nameof(CanOpenTrip));
        OnPropertyChanged(nameof(CanFinalize));
        OnPropertyChanged(nameof(CanPrintWeighTicket));
        OnPropertyChanged(nameof(CanPrintDeliveryTicket));
        OnPropertyChanged(nameof(CanViewImageHistory));
        OnPropertyChanged(nameof(IsAutoMode));
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(CanUseManualMode));
        OnPropertyChanged(nameof(IsTripFormEditable));
        OnPropertyChanged(nameof(IsTripFormReadOnly));
        CreateTripCommand.NotifyCanExecuteChanged();
        OpenTripCommand.NotifyCanExecuteChanged();
        FinalizeCommand.NotifyCanExecuteChanged();
        PrintWeighTicketCommand.NotifyCanExecuteChanged();
        PrintDeliveryTicketCommand.NotifyCanExecuteChanged();
        ViewImageHistoryCommand.NotifyCanExecuteChanged();
        CaptureWeight1Command.NotifyCanExecuteChanged();
        CaptureWeight2Command.NotifyCanExecuteChanged();
        SaveCapturedWeightCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCutOrderChanged(ExportScaleCutOrderListItem? value)
    {
        if (value == null)
        {
            Trips.Clear();
            SelectedTrip = null;
            ClearTripFormFields();
            RefreshCommandStates();
            return;
        }

        SelectedTrip = null;
        ClearTripFormFields();
        _ = LoadTripsAsync(value.CutOrderId);
        RefreshCommandStates();
    }

    partial void OnSelectedTripChanged(ExportVehicleTripListItem? value)
    {
        if (value == null)
        {
            ClearTripFormFields();
        }
        else
        {
            SetNewVehiclePlate(value.VehiclePlate);
            SetNewMoocNumber(value.MoocNumber);
            SetNewDriverName(value.DriverName);
            _ = RefreshVehicleMasterInfoAsync();
        }

        ApplySelectedTripWeights();
        RefreshCommandStates();
    }

    partial void OnNewVehiclePlateChanged(string? value)
    {
        RefreshCommandStates();
        _ = RefreshVehicleMasterInfoAsync();
    }

    partial void OnNewMoocNumberChanged(string? value)
    {
        RefreshCommandStates();
        _ = RefreshVehicleMasterInfoAsync();
    }

    partial void OnVehicleRegistrationExpiryDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(IsVehicleRegistrationExpired));
    }

    partial void OnMoocRegistrationExpiryDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(IsMoocRegistrationExpired));
    }

    partial void OnVehicleTtcpWeightChanged(decimal? value)
    {
        VehicleTtcp10Weight = value is > 0m
            ? decimal.Round(value.Value * 1.10m, 3, MidpointRounding.AwayFromZero)
            : null;
    }

    partial void OnIsLoadingChanged(bool value) => RefreshCommandStates();

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

    partial void OnSelectedPreviewCameraCodeChanged(string value)
    {
        if (_cameraSettings == null)
        {
            return;
        }

        _ = StartCameraPreviewAsync(value);
    }

    private bool ValidateCreateTripForm()
    {
        if (string.IsNullOrWhiteSpace(NewVehiclePlate))
        {
            _toastService.ShowWarning(UiText.Common.RequiredVehiclePlate);
            return false;
        }

        if (string.IsNullOrWhiteSpace(NewDriverName))
        {
            _toastService.ShowWarning(UiText.Common.RequiredDriverName);
            return false;
        }

        var expiryValidationMessage = ValidateRegistrationExpiryForCreateTrip();
        if (!string.IsNullOrWhiteSpace(expiryValidationMessage))
        {
            _toastService.ShowWarning(expiryValidationMessage);
            return false;
        }

        return true;
    }

    private string? ValidateRegistrationExpiryForCreateTrip()
    {
        var issues = new List<string>();
        if (IsExpiredDate(VehicleRegistrationExpiryDate))
        {
            issues.Add($"hạn đăng kiểm xe ({VehicleRegistrationExpiryDate:dd/MM/yyyy})");
        }

        if (IsExpiredDate(MoocRegistrationExpiryDate))
        {
            issues.Add($"hạn đăng kiểm mooc ({MoocRegistrationExpiryDate:dd/MM/yyyy})");
        }

        if (issues.Count == 0)
        {
            return null;
        }

        return $"Không thể tạo chuyến xe cho cắt lệnh {SelectedCutOrder?.ErpCutOrderId ?? NewVehiclePlate} vì {string.Join(" và ", issues)} đã hết hạn.";
    }

    private static bool IsExpiredDate(DateTime? value)
    {
        return value.HasValue && value.Value.Date < DateTime.Today;
    }

    public void Dispose()
    {
        _scaleUiTimer.Stop();
        _scaleUiTimer.Tick -= OnScaleUiTimerTick;
        _scaleDevice.WeightReceived -= OnWeightReceived;
        _cameraPreviewService.StatusChanged -= OnCameraPreviewStatusChanged;
        _cameraPreviewService.FrameReceived -= OnCameraPreviewFrameReceived;
        TripVehiclePlateInput.Dispose();
        TripMoocInput.Dispose();
        TripDriverInput.Dispose();
        ResetPreviewRenderState();
        _ = _cameraPreviewService.StopPreviewAsync();
    }

    private sealed record SessionPrintContext(
        WeighingSession MasterSession,
        IReadOnlyList<WeighingSessionLine> Lines,
        IReadOnlyDictionary<Guid, CutOrder> RegistrationsById,
        IReadOnlyList<WeighTicket> WeighTickets,
        IReadOnlyList<DeliveryTicket> DeliveryTickets,
        Vehicle? Vehicle);
}
