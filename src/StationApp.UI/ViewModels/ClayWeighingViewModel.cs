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

public partial class ClayWeighingViewModel : ObservableObject, IDisposable, IWeighingDeviceHost
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly ICameraPreviewService _cameraPreviewService;
    private readonly IToastService _toastService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICurrentStationContext _currentStationContext;
    private readonly IDialogService _dialogService;
    private readonly ILogger<ClayWeighingViewModel>? _logger;
    private readonly Dispatcher _uiDispatcher;
    private readonly WeighingDeviceConnector _deviceConnector;

    public event Action<string?, string?>? NavigateToEditHistoryRequested;

    // Clay Weighing: Default Product and Customer
    private string _defaultProductCode = ClayDefaults.ProductCode;
    private string _defaultProductName = ClayDefaults.ProductName;
    private string _defaultCustomerCode = ClayDefaults.CustomerCode;
    private string _defaultCustomerName = ClayDefaults.CustomerName;

    public AutocompleteInputViewModel InternalVehiclePlateInput { get; }

    // Crusher Weighing: Product and Customer Inputs
    public AutocompleteInputViewModel ProductCodeInput { get; }
    public AutocompleteInputViewModel ProductNameInput { get; }
    public AutocompleteInputViewModel CustomerCodeInput { get; }
    public AutocompleteInputViewModel CustomerNameInput { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateTripCommand))]
    private Vehicle? _selectedVehicle;
    [ObservableProperty] private ObservableCollection<CrusherWeighingSessionListItem> _sessions = new();
    [ObservableProperty] private ObservableCollection<ClayVesselListItem> _vessels = new();
    [ObservableProperty] private ObservableCollection<ClayVehicleTripListItem> _trips = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinalizeClayVesselCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateTripCommand))]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    private ClayVesselListItem? _selectedVessel;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransferTripCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteTripCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewImageHistoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleReturnedBrokenTripCommand))]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    private ClayVehicleTripListItem? _selectedTrip;
    [ObservableProperty] private int _selectedTripIndex = -1;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeCrusherWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCrusherWeighingCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintWeighTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditSessionVehicleCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewSessionHistoryCommand))]
    private CrusherWeighingSessionListItem? _selectedSession;
    [ObservableProperty] private string? _searchVessel;
    [ObservableProperty] private bool _showFinalizedVessels;
    [ObservableProperty] private string? _searchVehicle;
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty] private int _clearTripSelectionRequest;
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
    [ObservableProperty] private string _vehicleSelectionStatusText = string.Empty;
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
    private int _tripLoadVersion;
    private bool _suppressSelectedVesselTripLoad;
    private bool _suppressVesselFilterLoad;
    private bool _isTripSelectionResetting;
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
    public bool CanCreateClayVessel => !IsLoading;
    public bool CanEditClayVessel => SelectedVessel != null
        && !SelectedVessel.IsFinalized
        && !IsLoading;
    public bool CanFinalizeClayVessel => SelectedVessel != null
        && !SelectedVessel.IsFinalized
        && Sessions.Any(x => x.SessionStatus is WeighingSessionStatus.COMPLETED or WeighingSessionStatus.READY_TO_COMPLETE);
    public bool CanCreateTrip =>
        SelectedVessel != null
        && !SelectedVessel.IsFinalized
        && !string.IsNullOrWhiteSpace(InternalVehiclePlateInput.Text)
        && !IsLoading;
    public bool CanTransferTrip => SelectedVessel != null && !SelectedVessel.IsFinalized && SelectedTrip != null && !IsLoading;
    public bool CanDeleteTrip => SelectedVessel != null
        && !SelectedVessel.IsFinalized
        && SelectedTrip != null
        && !SelectedTrip.Weight2.HasValue
        && !SelectedTrip.Weight2Time.HasValue
        && !IsLoading;
    public bool CanViewImageHistory => SelectedTrip?.Weight1.HasValue == true && !IsLoading;
    public bool CanToggleReturnedBrokenTrip => SelectedVessel != null
        && !SelectedVessel.IsFinalized
        && SelectedTrip?.CanToggleReturnedBrokenTrip == true
        && !IsLoading;
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
        IDialogService dialogService,
        ILogger<ClayWeighingViewModel>? logger = null)
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
                IsVehicleFormReadOnly = false;
                VehicleSelectionStatusText = string.Empty;
                EnsureTwoWeighMode();
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                _ = RefreshVehicleMasterInfoAsync();
            }

            TakeCrusherWeight1Command.NotifyCanExecuteChanged();
            CreateTripCommand.NotifyCanExecuteChanged();
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

        EnsureTwoWeighMode();

        await LoadVesselsAsync();
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

    private async Task LoadVesselsAsync(Guid? preserveCutOrderId = null, bool loadTripsForSelectedVessel = true)
    {
        try
        {
            IsLoading = true;
            var selectedId = preserveCutOrderId ?? SelectedVessel?.CutOrderId;
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
            var vessels = await repo.GetClayVesselsAsync(
                new ClayVesselFilter(SearchVessel, null, null, ShowFinalizedVessels),
                CancellationToken.None);

            Vessels = new ObservableCollection<ClayVesselListItem>(vessels);
            var nextSelectedVessel = selectedId.HasValue
                ? Vessels.FirstOrDefault(x => x.CutOrderId == selectedId.Value)
                : null;
            _suppressSelectedVesselTripLoad = !loadTripsForSelectedVessel;
            try
            {
                SelectedVessel = nextSelectedVessel;
            }
            finally
            {
                _suppressSelectedVesselTripLoad = false;
            }

            if (SelectedVessel == null)
            {
                Sessions.Clear();
                Trips.Clear();
                ClearSelectedTrip();
                ClearAllWeighingState();
                ApplyVehicleInfo(null);
                InternalVehiclePlateInput.Clear();
                SetDefaultProductAndCustomer();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load clay vessels.");
            _toastService.ShowError("Không thể tải danh sách tàu mỏ sét.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadSessionsAsync()
        => await LoadSessionsForSelectedVesselAsync();

    private async Task ReloadVesselAndTripsAsync(Guid? cutOrderId, Guid? selectedTripId = null)
    {
        await LoadVesselsAsync(cutOrderId, loadTripsForSelectedVessel: false);
        if (SelectedVessel != null)
        {
            await LoadSessionsForSelectedVesselAsync(selectedTripId);
        }
    }

    private async Task LoadSessionsForSelectedVesselAsync(Guid? selectedTripId = null)
    {
        var cutOrderId = SelectedVessel?.CutOrderId;
        var loadVersion = ++_tripLoadVersion;
        try
        {
            IsLoading = true;
            if (!cutOrderId.HasValue)
            {
                Sessions.Clear();
                Trips.Clear();
                ClearSelectedTrip();
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
            IReadOnlyList<ClayVehicleTripListItem> trips = await repo.GetClayVehicleTripsAsync(cutOrderId.Value, CancellationToken.None);

            if (loadVersion != _tripLoadVersion || SelectedVessel?.CutOrderId != cutOrderId.Value)
            {
                _logger?.LogDebug(
                    "Ignored stale clay trip load. CutOrderId={CutOrderId}, LoadVersion={LoadVersion}, CurrentVersion={CurrentVersion}, SelectedVesselId={SelectedVesselId}",
                    cutOrderId.Value,
                    loadVersion,
                    _tripLoadVersion,
                    SelectedVessel?.CutOrderId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(SearchSessionNo))
            {
                trips = trips
                    .Where(x => x.SessionNo.Contains(SearchSessionNo, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            Trips = new ObservableCollection<ClayVehicleTripListItem>(trips);
            Sessions = new ObservableCollection<CrusherWeighingSessionListItem>(
                trips.Select(x => new CrusherWeighingSessionListItem(
                    x.SessionId,
                    x.SessionNo,
                    x.VehiclePlate,
                    x.DriverName,
                    x.Weight1,
                    x.Weight1Time,
                    x.Weight2,
                    x.Weight2Time,
                    x.NetWeight,
                    x.WeighingMode,
                    x.StandardTareWeightSnapshot,
                    x.StandardTareSourceSnapshot,
                    x.SessionStatus,
                    x.CreatedAt,
                    x.UpdatedAt,
                    SelectedVessel?.ProductCode,
                    SelectedVessel?.ProductName,
                    SelectedVessel?.CustomerCode,
                    SelectedVessel?.CustomerName,
                    x.IsReturnedBrokenTrip,
                    x.Weight1User,
                    x.Weight2User)));

            SelectedTrip = selectedTripId.HasValue
                ? Trips.FirstOrDefault(x => x.SessionId == selectedTripId.Value)
                : null;
            SelectedTripIndex = SelectedTrip is null ? -1 : Trips.IndexOf(SelectedTrip);
            if (!selectedTripId.HasValue || SelectedTrip == null)
            {
                ClearSelectedTrip();
            }
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

    [RelayCommand(CanExecute = nameof(CanCreateClayVessel))]
    private async Task CreateClayVesselAsync()
    {
        var dialogVm = new CreateClayVesselDialogViewModel(_scopeFactory);
        var dialogResult = await _dialogService.ShowCustomDialogAsync<CreateClayVesselDialogViewModel, CreateClayVesselDialogResult>(dialogVm);
        if (dialogResult == null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<CreateClayTemporaryCutOrderUseCase>();
            var cutOrderId = await useCase.ExecuteAsync(
                new CreateClayVesselRequest(
                    dialogResult.VesselName ?? string.Empty,
                    dialogResult.CustomerCode,
                    dialogResult.CustomerName,
                    dialogResult.ProductCode,
                    dialogResult.ProductName,
                    dialogResult.Notes),
                CancellationToken.None);

            _toastService.ShowSuccess("Đã tạo tàu mỏ sét.");
            await LoadVesselsAsync(cutOrderId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Create clay vessel failed");
            _toastService.ShowError("Không thể tạo tàu mỏ sét.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditClayVessel))]
    private async Task EditClayVesselAsync()
    {
        if (SelectedVessel == null)
        {
            return;
        }

        var cutOrderId = SelectedVessel.CutOrderId;
        var dialogVm = new CreateClayVesselDialogViewModel(_scopeFactory, SelectedVessel);
        var dialogResult = await _dialogService.ShowCustomDialogAsync<CreateClayVesselDialogViewModel, CreateClayVesselDialogResult>(dialogVm);
        if (dialogResult == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<UpdateClayVesselUseCase>();
            await useCase.ExecuteAsync(
                new UpdateClayVesselRequest(
                    cutOrderId,
                    dialogResult.VesselName,
                    dialogResult.CustomerCode,
                    dialogResult.CustomerName,
                    dialogResult.ProductCode,
                    dialogResult.ProductName,
                    dialogResult.Notes),
                CancellationToken.None);

            _toastService.ShowSuccess("Đã cập nhật tàu mỏ sét.");
            await LoadVesselsAsync(cutOrderId, loadTripsForSelectedVessel: false);
            if (SelectedVessel != null)
            {
                await LoadSessionsForSelectedVesselAsync();
            }
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Edit clay vessel failed. CutOrderId={CutOrderId}", cutOrderId);
            _toastService.ShowError("Không thể cập nhật tàu mỏ sét.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFinalizeClayVessel))]
    private async Task FinalizeClayVesselAsync()
    {
        if (SelectedVessel == null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            "Chốt tổng tàu",
            $"Chốt tổng tàu {SelectedVessel.VesselName}?",
            "Chốt",
            "Hủy");
        if (!confirmed)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<FinalizeClayCutOrderUseCase>();
            await useCase.ExecuteAsync(new FinalizeClayCutOrderRequest(SelectedVessel.CutOrderId), CancellationToken.None);
            _toastService.ShowSuccess("Đã chốt tổng tàu mỏ sét.");
            await LoadVesselsAsync();
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Finalize clay vessel failed");
            _toastService.ShowError("Không thể chốt tổng tàu mỏ sét.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateTrip))]
    private async Task CreateTripAsync()
    {
        if (SelectedVessel == null)
        {
            _toastService.ShowWarning("Vui lòng chọn tàu trước khi tạo chuyến xe.");
            return;
        }

        var vehicle = await EnsureInternalVehicleForWeighingAsync();
        if (vehicle == null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<CreateClayPendingVehicleTripUseCase>();
            var result = await useCase.ExecuteAsync(
                new CreateClayPendingVehicleTripRequest(
                    SelectedVessel.CutOrderId,
                    vehicle.Id,
                    SelectedWeighingMode),
                CancellationToken.None);

            _toastService.ShowSuccess("Đã tạo chuyến xe mỏ sét.");
            await ReloadVesselAndTripsAsync(SelectedVessel.CutOrderId, result.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Create clay vehicle trip failed");
            _toastService.ShowError("Không thể tạo chuyến xe mỏ sét.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanTransferTrip))]
    private async Task TransferTripAsync()
    {
        if (SelectedVessel == null || SelectedTrip == null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
            var vessels = await repo.GetClayVesselsAsync(
                new ClayVesselFilter(null, null, null, false),
                CancellationToken.None);
            var options = vessels
                .Where(x => x.CutOrderId != SelectedVessel.CutOrderId)
                .Where(x => !x.IsFinalized)
                .Select(x => new ExportTripTransferOption(
                    x.CutOrderId,
                    null,
                    x.VesselName,
                    x.VesselName,
                    x.CustomerName,
                    x.ProductName,
                    null,
                    x.AccumulatedWeight,
                    x.TripCount,
                    x.LastTripAt))
                .ToList();

            if (options.Count == 0)
            {
                _toastService.ShowWarning("Không có tàu mỏ sét chưa chốt để chuyển chuyến.");
                return;
            }

            var dialogVm = new ExportTripTransferDialogViewModel(options)
            {
                Title = "Chuyển chuyến xe sang tàu khác",
                Message = $"Chọn tàu đích để chuyển chuyến {SelectedTrip.SessionNo} từ tàu {SelectedVessel.VesselName}."
            };
            var selection = await _dialogService.ShowCustomDialogAsync<ExportTripTransferDialogViewModel, ExportTripTransferDialogResult>(dialogVm);
            if (selection == null)
            {
                return;
            }

            var confirmed = await _dialogService.ShowConfirmAsync(
                "Xác nhận chuyển chuyến",
                $"Chuyển chuyến {SelectedTrip.SessionNo} sang tàu {options.First(x => x.CutOrderId == selection.CutOrderId).DisplayCutOrderCode}?",
                "Chuyển",
                "Hủy");
            if (!confirmed)
            {
                return;
            }

            var useCase = scope.ServiceProvider.GetRequiredService<TransferClayVehicleTripUseCase>();
            var sessionId = SelectedTrip.SessionId;
            await useCase.ExecuteAsync(new TransferClayVehicleTripRequest(sessionId, selection.CutOrderId), CancellationToken.None);
            _toastService.ShowSuccess("Đã chuyển chuyến xe.");
            await ReloadVesselAndTripsAsync(selection.CutOrderId, sessionId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Transfer clay trip failed");
            _toastService.ShowError("Không thể chuyển chuyến xe mỏ sét.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteTrip))]
    private async Task DeleteTripAsync()
    {
        if (SelectedTrip == null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            "Xóa chuyến xe",
            $"Xóa chuyến xe {SelectedTrip.SessionNo}? Chỉ chuyến chưa cân lần 2 mới được xóa.",
            "Xóa",
            "Hủy");
        if (!confirmed)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<DeleteClayVehicleTripUseCase>();
            await useCase.ExecuteAsync(SelectedTrip.SessionId, CancellationToken.None);
            _toastService.ShowSuccess("Đã xóa chuyến xe.");
            await ReloadVesselAndTripsAsync(SelectedVessel?.CutOrderId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Delete clay trip failed");
            _toastService.ShowError("Không thể xóa chuyến xe mỏ sét.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanToggleReturnedBrokenTrip))]
    private async Task ToggleReturnedBrokenTripAsync(ClayVehicleTripListItem? trip)
    {
        trip ??= SelectedTrip;
        if (trip == null || !trip.CanToggleReturnedBrokenTrip)
        {
            return;
        }

        var newState = !trip.IsReturnedBrokenTrip;
        var confirmMessage = $"Bỏ đánh dấu chuyến {trip.SessionNo} là hàng hoàn?";

        if (newState)
        {
            using var lookupScope = _scopeFactory.CreateScope();
            var sessionRepo = lookupScope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
            var previousTrip = await sessionRepo.GetPreviousClayTripForReturnedAsync(trip.SessionLineId, CancellationToken.None);
            if (previousTrip == null)
            {
                await _dialogService.ShowWarningAsync(
                    "Không đủ dữ liệu đối chiếu",
                    "Không có dữ liệu chuyến xe gần nhất trước đó của xe này. Vui lòng kiểm tra lại.");
                return;
            }

            var actualWeight = ResolveActualAllocatedWeightKg(trip);
            var resolution = ReturnedBrokenTripWeightLimiter.Resolve(actualWeight, previousTrip.NetWeightKg);
            confirmMessage = resolution.IsCapped
                ? BuildReturnedBrokenTripCappedConfirmMessage(trip.SessionNo, previousTrip, resolution)
                : $"Đánh dấu chuyến {trip.SessionNo} là hàng hoàn?";
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            newState ? "Đánh dấu Hoàn" : "Bỏ đánh dấu Hoàn",
            confirmMessage,
            "Đồng ý",
            "Hủy");
        if (!confirmed)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<ToggleClayReturnedBrokenTripUseCase>();
            await useCase.ExecuteAsync(trip.SessionLineId, newState, CancellationToken.None);
            _toastService.ShowSuccess(newState ? "Đã đánh dấu Hoàn." : "Đã bỏ đánh dấu Hoàn.");
            var sessionId = trip.SessionId;
            await ReloadVesselAndTripsAsync(SelectedVessel?.CutOrderId, sessionId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Toggle clay returned trip failed");
            _toastService.ShowError("Không thể cập nhật trạng thái Hoàn.");
        }
    }

    private static string BuildReturnedBrokenTripCappedConfirmMessage(
        string sessionNo,
        ReturnedBrokenTripPreviousTripInfo previousTrip,
        ReturnedBrokenTripWeightResolution resolution)
    {
        return
            $"Đánh dấu chuyến {sessionNo} là hàng hoàn?\n\n" +
            $"TL hoàn thực cân: {FormatTon(resolution.ActualWeightTon)} tấn\n" +
            $"TL chuyến gần nhất trong cùng tàu: {FormatTon(resolution.PreviousTripWeightTon ?? 0m)} tấn\n\n" +
            $"Do TL hoàn thực cân lớn hơn chuyến gần nhất, hệ thống chỉ ghi nhận Hoàn là {FormatTon(resolution.RecognizedWeightTon)} tấn.";
    }

    private static decimal ResolveActualAllocatedWeightKg(ClayVehicleTripListItem trip)
    {
        if (trip.NetWeight.HasValue && trip.NetWeight.Value > 0m)
        {
            return trip.NetWeight.Value;
        }

        return Math.Max(0m, trip.ActualAllocatedWeight ?? 0m);
    }

    private static string FormatTon(decimal value)
    {
        return value.ToString("N3", CultureInfo.CurrentCulture);
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
            await _dialogService.ShowCustomDialogAsync<CameraImageHistoryViewModel, bool>(
                new CameraImageHistoryViewModel(images, SelectedTrip.VehiclePlate ?? string.Empty, _toastService));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "View clay trip image history failed");
            _toastService.ShowError("Không thể xem ảnh chuyến xe.");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _suppressVesselFilterLoad = true;
        try
        {
            SearchSessionNo = null;
            SearchVessel = null;

            SelectedVessel = null;
            SelectedSession = null;
            SelectedVehicle = null;
            Sessions.Clear();
            Trips.Clear();
            ClearSelectedTrip();

            ClearAllWeighingState();
            ApplyVehicleInfo(null);
            InternalVehiclePlateInput.Clear();
            SetDefaultProductAndCustomer();
            ShowUpdateButton = false;
        }
        finally
        {
            _suppressVesselFilterLoad = false;
        }

        EnsureTwoWeighMode();

        // Reload vessel and session list
        await LoadVesselsAsync(loadTripsForSelectedVessel: false);
    }

    private async Task RefreshAfterSaveAsync(Guid sessionId)
    {
        var cutOrderId = SelectedVessel?.CutOrderId;
        ClearAllWeighingState();
        ApplyVehicleInfo(null);
        InternalVehiclePlateInput.Clear();

        if (cutOrderId.HasValue)
        {
            await ReloadVesselAndTripsAsync(cutOrderId.Value, sessionId);
        }
        else
        {
            await LoadVesselsAsync(loadTripsForSelectedVessel: false);
        }
    }

    private void ClearSelectedTrip()
    {
        SelectedTrip = null;
        SelectedTripIndex = -1;
        SelectedSession = null;
        ClearTripSelectionRequest++;
        NotifyTripSelectionCommandStates();
    }

    public void BeginTripSelectionReset()
    {
        _isTripSelectionResetting = true;
    }

    public void CompleteTripSelectionReset()
    {
        _isTripSelectionResetting = false;
        NotifyTripSelectionCommandStates();
    }

    public void LogTripGridSelectionState(
        string source,
        ClayVehicleTripListItem? gridSelectedTrip,
        int gridSelectedIndex,
        ClayVehicleTripListItem? gridCurrentTrip,
        bool isResettingSelection)
    {
        _logger?.LogDebug(
            "Clay trip grid selection state. Source={Source}, GridSelectedIndex={GridSelectedIndex}, GridSelectedSessionNo={GridSelectedSessionNo}, GridSelectedStatus={GridSelectedStatus}, GridCurrentSessionNo={GridCurrentSessionNo}, VmSelectedSessionNo={VmSelectedSessionNo}, VmSelectedStatus={VmSelectedStatus}, IsLoading={IsLoading}, IsResettingSelection={IsResettingSelection}",
            source,
            gridSelectedIndex,
            gridSelectedTrip?.SessionNo,
            gridSelectedTrip?.SessionStatus,
            gridCurrentTrip?.SessionNo,
            SelectedTrip?.SessionNo,
            SelectedTrip?.SessionStatus,
            IsLoading,
            isResettingSelection);
    }

    private void NotifyTripSelectionCommandStates()
    {
        OnPropertyChanged(nameof(CanTransferTrip));
        OnPropertyChanged(nameof(CanDeleteTrip));
        OnPropertyChanged(nameof(CanViewImageHistory));
        OnPropertyChanged(nameof(CanToggleReturnedBrokenTrip));
        TransferTripCommand.NotifyCanExecuteChanged();
        DeleteTripCommand.NotifyCanExecuteChanged();
        ViewImageHistoryCommand.NotifyCanExecuteChanged();
        ToggleReturnedBrokenTripCommand.NotifyCanExecuteChanged();
        TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        TakeCrusherWeight2Command.NotifyCanExecuteChanged();
        SaveCrusherWeighingCommand.NotifyCanExecuteChanged();
        EditSessionVehicleCommand.NotifyCanExecuteChanged();
        ViewSessionHistoryCommand.NotifyCanExecuteChanged();
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
            var settings = await provider.GetForStationAsync("CLAY", CancellationToken.None);
            _deviceConnector.InitializeCameraPreview(settings);
            _ = _deviceConnector.StartCameraPreviewAsync(SelectedPreviewCameraCode);
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

    private async Task TryCaptureClayTripImagesAsync(Guid sessionId, CameraCaptureStage stage, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cameraSettingsProvider = scope.ServiceProvider.GetRequiredService<ICameraSettingsProvider>();
            var cameraCaptureService = scope.ServiceProvider.GetRequiredService<ICameraCaptureService>();
            var imageRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionImageRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var settings = await cameraSettingsProvider.GetForStationAsync("CLAY", ct);
            if (settings.EnabledCameras.Count == 0)
            {
                return;
            }

            var captures = await cameraCaptureService.CaptureAsync(
                settings.EnabledCameras,
                settings.CaptureTimeoutMs,
                settings.CaptureJpegQuality,
                settings.CaptureMaxDimension,
                settings.CaptureWarmupFrames,
                ct);

            var successfulCaptures = captures
                .Where(x => x.Success && x.ImageBytes.Length > 0)
                .ToList();
            if (successfulCaptures.Count == 0)
            {
                return;
            }

            var now = clock.NowLocal;
            await uow.ExecuteInTransactionAsync(async innerCt =>
            {
                foreach (var capture in successfulCaptures)
                {
                    await imageRepo.AddAsync(
                        new WeighingSessionImage
                        {
                            Id = Guid.NewGuid(),
                            WeighingSessionId = sessionId,
                            CaptureStage = stage,
                            CameraCode = capture.CameraCode,
                            CameraName = capture.CameraName,
                            RtspUrlSnapshot = capture.RtspUrlSnapshot,
                            ImageFormat = capture.ImageFormat,
                            ImageBytes = capture.ImageBytes,
                            FileSizeBytes = capture.ImageBytes.LongLength,
                            CapturedAt = capture.CapturedAt,
                            CapturedBy = _currentUserContext.Username,
                            CreatedAt = now,
                            CreatedBy = _currentUserContext.Username,
                            UpdatedAt = now,
                            UpdatedBy = _currentUserContext.Username
                        },
                        innerCt);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to capture clay trip images for session {SessionId}.", sessionId);
        }
    }

    public void RaisePropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }

    partial void OnSessionsChanged(ObservableCollection<CrusherWeighingSessionListItem> value)
    {
        OnPropertyChanged(nameof(CanFinalizeClayVessel));
        OnPropertyChanged(nameof(CanTransferTrip));
        OnPropertyChanged(nameof(CanDeleteTrip));
        OnPropertyChanged(nameof(CanViewImageHistory));
        OnPropertyChanged(nameof(CanToggleReturnedBrokenTrip));
        FinalizeClayVesselCommand.NotifyCanExecuteChanged();
        TransferTripCommand.NotifyCanExecuteChanged();
        DeleteTripCommand.NotifyCanExecuteChanged();
        ViewImageHistoryCommand.NotifyCanExecuteChanged();
        ToggleReturnedBrokenTripCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchVesselChanged(string? value)
    {
        if (_suppressVesselFilterLoad)
        {
            return;
        }

        _ = LoadVesselsAsync();
    }

    partial void OnShowFinalizedVesselsChanged(bool value)
    {
        if (_suppressVesselFilterLoad)
        {
            return;
        }

        _ = LoadVesselsAsync();
    }

    partial void OnSelectedVesselChanged(ClayVesselListItem? value)
    {
        Trips.Clear();
        Sessions.Clear();
        ClearSelectedTrip();
        ClearAllWeighingState();
        if (value != null)
        {
            ProductCodeInput.SetText(value.ProductCode);
            ProductNameInput.SetText(value.ProductName);
            CustomerCodeInput.SetText(value.CustomerCode);
            CustomerNameInput.SetText(value.CustomerName);
        }
        else
        {
            SetDefaultProductAndCustomer();
        }

        OnPropertyChanged(nameof(CanFinalizeClayVessel));
        OnPropertyChanged(nameof(CanCreateTrip));
        OnPropertyChanged(nameof(CanEditClayVessel));
        TakeCrusherWeight1Command.NotifyCanExecuteChanged();
        SaveCrusherWeighingCommand.NotifyCanExecuteChanged();
        CreateTripCommand.NotifyCanExecuteChanged();
        EditClayVesselCommand.NotifyCanExecuteChanged();
        FinalizeClayVesselCommand.NotifyCanExecuteChanged();
        if (!_suppressSelectedVesselTripLoad && value != null)
        {
            _ = LoadSessionsForSelectedVesselAsync();
        }
    }

    partial void OnSelectedTripChanged(ClayVehicleTripListItem? value)
    {
        if (_isTripSelectionResetting && value != null)
        {
            _logger?.LogDebug(
                "Ignored clay trip selection while reset is active. SessionNo={SessionNo}, SessionId={SessionId}, Status={Status}",
                value.SessionNo,
                value.SessionId,
                value.SessionStatus);
            SelectedTrip = null;
            SelectedTripIndex = -1;
            NotifyTripSelectionCommandStates();
            return;
        }

        SelectedTripIndex = value is null ? -1 : Trips.IndexOf(value);
        SelectedSession = value is null
            ? null
            : Sessions.FirstOrDefault(x => x.SessionId == value.SessionId);
        NotifyTripSelectionCommandStates();
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
        if (IsLoading
            || SelectedVessel == null
            || SelectedVessel.IsFinalized
            || SelectedTrip?.SessionStatus != WeighingSessionStatus.PENDING_WEIGHT1
            || string.IsNullOrWhiteSpace(InternalVehiclePlateInput.Text))
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
        if (SelectedTrip == null)
        {
            SelectedSession = null;
        }

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
            _toastService.ShowWarning("Vui l\u00f2ng c\u00e2n l\u1ea7n 1 tr\u01b0\u1edbc khi c\u00e2n l\u1ea7n 2.");
            return;
        }

        if (CurrentWeight <= 0)
        {
            _toastService.ShowWarning("S\u1ed1 c\u00e2n l\u1ea7n 2 ph\u1ea3i l\u1edbn h\u01a1n 0.");
            return;
        }

        _pendingWeight2 = CurrentWeight;
        _pendingWeight2IsStable = IsStable;
        _pendingWeight2Mode = IsManualMode ? WeightMode.MANUAL : WeightMode.AUTO;

        RefreshCapturedWeightState();
        _toastService.ShowSuccess("\u0110\u00e3 l\u1ea5y s\u1ed1 c\u00e2n l\u1ea7n 2.");
    }

    private bool CanSaveCrusherWeighing()
    {
        if (IsLoading)
            return false;

        if (SelectedVessel == null || SelectedVessel.IsFinalized)
            return false;

        // Không cho phép lưu nếu session đã hoàn thành hoặc đã hủy
        var sessionStatus = SelectedSession?.SessionStatus;
        if (sessionStatus == WeighingSessionStatus.COMPLETED
            || sessionStatus == WeighingSessionStatus.CANCELLED)
        {
            return false;
        }

        // Cân 1 lần: cần có pending weight 1 và selected vehicle
        if (SelectedTrip?.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT1 && _pendingWeight1.HasValue && SelectedVehicle != null)
            return true;

        // Cân 2 lần - trường hợp mới (chưa có active session):
        //   - Cần có pending weight 1 và selected vehicle (để tạo session mới với weight1)
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
            var sessionId = _activeCrusherSessionId;
            if (sessionId is null)
            {
                if (SelectedTrip == null || SelectedTrip.SessionStatus != WeighingSessionStatus.PENDING_WEIGHT1)
                {
                    _toastService.ShowWarning("Vui lòng chọn tàu mỏ sét trước khi lưu chuyến xe.");
                    return;
                }

                var captureWeight1UseCase = scope.ServiceProvider.GetRequiredService<CaptureClayWeight1ForTripUseCase>();
                await captureWeight1UseCase.ExecuteAsync(
                    new CaptureClayWeight1ForTripRequest(
                        SelectedTrip.SessionId,
                        _pendingWeight1!.Value,
                        _pendingWeight1IsStable,
                        _pendingWeight1Mode),
                    CancellationToken.None);
                sessionId = SelectedTrip.SessionId;
                await TryCaptureClayTripImagesAsync(sessionId.Value, CameraCaptureStage.WEIGHT1, CancellationToken.None);

                if (IsSingleWeighMode)
                {
                    var completeLineUseCase = scope.ServiceProvider.GetRequiredService<CompleteClayVehicleSessionLineUseCase>();
                    await completeLineUseCase.ExecuteAsync(sessionId.Value, CancellationToken.None);
                }
            }

            if (IsTwoWeighMode && _pendingWeight2.HasValue)
            {
                var useCases = scope.ServiceProvider.GetRequiredService<ClayWeighingUseCases>();
                await useCases.CaptureWeight2Async(
                    new CaptureClayWeight2Request(
                        sessionId.Value,
                        _pendingWeight2.Value,
                        _pendingWeight2IsStable,
                        _pendingWeight2Mode),
                    CancellationToken.None);
                await TryCaptureClayTripImagesAsync(sessionId.Value, CameraCaptureStage.WEIGHT2, CancellationToken.None);
                var completeLineUseCase = scope.ServiceProvider.GetRequiredService<CompleteClayVehicleSessionLineUseCase>();
                await completeLineUseCase.ExecuteAsync(sessionId.Value, CancellationToken.None);
            }

            _toastService.ShowSuccess("Đã lưu lượt cân mỏ sét.");
            await RefreshAfterSaveAsync(sessionId.Value);
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
        EnsureTwoWeighMode();
    }

    partial void OnSelectedSessionChanged(CrusherWeighingSessionListItem? value)
    {
        if (value != null)
        {
            EnsureTwoWeighMode();

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

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCreateClayVessel));
        OnPropertyChanged(nameof(CanEditClayVessel));
        OnPropertyChanged(nameof(CanFinalizeClayVessel));
        OnPropertyChanged(nameof(CanCreateTrip));
        OnPropertyChanged(nameof(CanTransferTrip));
        OnPropertyChanged(nameof(CanDeleteTrip));
        OnPropertyChanged(nameof(CanViewImageHistory));
        OnPropertyChanged(nameof(CanToggleReturnedBrokenTrip));
        CreateClayVesselCommand.NotifyCanExecuteChanged();
        EditClayVesselCommand.NotifyCanExecuteChanged();
        FinalizeClayVesselCommand.NotifyCanExecuteChanged();
        CreateTripCommand.NotifyCanExecuteChanged();
        TransferTripCommand.NotifyCanExecuteChanged();
        DeleteTripCommand.NotifyCanExecuteChanged();
        ViewImageHistoryCommand.NotifyCanExecuteChanged();
        ToggleReturnedBrokenTripCommand.NotifyCanExecuteChanged();
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
        var vehiclePlate = VehicleIdentifierNormalizer.NormalizePlate(InternalVehiclePlateInput.Text);
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            _toastService.ShowWarning("Vui lòng nhập số xe nội bộ.");
            return null;
        }

        var standardTare = ParseStandardTare(StandardTareText);
        if (standardTare == null && !string.IsNullOrWhiteSpace(StandardTareText))
        {
            _toastService.ShowWarning("Trọng lượng bì không đúng định dạng.");
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
                _toastService.ShowWarning("Xe chưa có trong danh mục xe nội bộ. Vui lòng thêm xe tại màn Danh mục xe trước khi cân mỏ sét.");
                return null;
            }

            if (vehicle == null)
            {
                var existingExternal = vehicles.FirstOrDefault(v => string.IsNullOrEmpty(v.MoocNumber));
                if (existingExternal != null)
                {
                    if (standardTare is <= 0)
                    {
                        _toastService.ShowWarning("Trọng lượng bì (nếu nhập) phải lớn hơn 0.");
                        return null;
                    }

                    existingExternal.IsInternalVehicle = true;
                    existingExternal.TtcpWeight = standardTare;
                    existingExternal.StandardTareSource = null;
                    existingExternal.StandardTareUpdatedAt = standardTare.HasValue ? now : null;
                    existingExternal.StandardTareUpdatedBy = standardTare.HasValue ? "Operator" : null;
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
                    if (standardTare is <= 0)
                    {
                        _toastService.ShowWarning("Trọng lượng bì (nếu nhập) phải lớn hơn 0.");
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
                        StandardTareUpdatedAt = standardTare.HasValue ? now : null,
                        StandardTareUpdatedBy = standardTare.HasValue ? "Operator" : null,
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
                _toastService.ShowWarning("Xe nội bộ chưa có TL bì, không thể cân 1 lần.");
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
            var effectiveStandardTare = GetEffectiveStandardTare(vehicle);
            SelectedDriverName = vehicle.DriverName;
            StandardTareText = effectiveStandardTare?.ToString("N0", CultureInfo.InvariantCulture);
            _originalDriverName = vehicle.DriverName;
            _originalStandardTare = effectiveStandardTare;
            IsVehicleFormReadOnly = false;
            VehicleSelectionStatusText = string.Empty;
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

        ShowUpdateButton = false;
    }

    private void EnsureTwoWeighMode()
    {
        if (!string.Equals(SelectedWeighingMode, ClayWeighingModes.TwoWeigh, StringComparison.Ordinal))
        {
            SelectedWeighingMode = ClayWeighingModes.TwoWeigh;
        }
    }

    private bool HasEffectiveStandardTare(Vehicle? vehicle)
        => GetEffectiveStandardTare(vehicle).HasValue;

    private decimal? GetEffectiveStandardTare(Vehicle? vehicle)
        => StandardTarePolicy.GetEffectiveStandardTare(vehicle, GetTodayLocal());

    private DateTime GetTodayLocal()
    {
        using var scope = _scopeFactory.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        return clock.TodayLocal;
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
        var vehiclePlate = VehicleIdentifierNormalizer.NormalizePlate(InternalVehiclePlateInput.Text);
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
                    if (standardTare is <= 0)
                    {
                        _toastService.ShowWarning("Trọng lượng bì (nếu nhập) phải lớn hơn 0.");
                        return;
                    }

                    existingExternal.IsInternalVehicle = true;
                    existingExternal.TtcpWeight = standardTare;
                    existingExternal.StandardTareSource = null;
                    existingExternal.StandardTareUpdatedAt = standardTare.HasValue ? now : null;
                    existingExternal.StandardTareUpdatedBy = standardTare.HasValue ? "Operator" : null;
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
                    if (standardTare is <= 0)
                    {
                        _toastService.ShowWarning("Trọng lượng bì (nếu nhập) phải lớn hơn 0.");
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
                        StandardTareUpdatedAt = standardTare.HasValue ? now : null,
                        StandardTareUpdatedBy = standardTare.HasValue ? "Operator" : null,
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

            // Check nếu chế độ cân 1 lần nhưng xe chưa có TL bì hiệu lực
            if (IsSingleWeighMode && !StandardTarePolicy.GetEffectiveStandardTare(vehicle, GetTodayLocal()).HasValue)
            {
                _toastService.ShowWarning("Xe nội bộ chưa có TL bì, không thể cân 1 lần.");
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
                _toastService.ShowWarning("Trọng lượng bì không đúng định dạng.");
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
            VehicleSelectionStatusText = string.Empty;
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
        var vehiclePlate = VehicleIdentifierNormalizer.NormalizePlate(InternalVehiclePlateInput.Text);

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
                    EnsureTwoWeighMode();
                    VehicleSelectionStatusText = string.Empty;
                }
                else
                {
                    // Xe đã tồn tại, tự động chuyển chế độ cân dựa trên TL bì có hiệu lực
                    EnsureTwoWeighMode();
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
        var results = await service.SearchAsync(new AutocompleteQuery(fieldType, keyword, TransactionType: TransactionType.INBOUND), ct);

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
                isReturnedBrokenTrip: false,
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
            _logger?.LogError(ex, "Clay print flow failed");
            _toastService.ShowError(string.Format(UiText.Weighing.PrintErrorFormat, "phiếu cân"));
        }
        finally
        {
            IsLoading = false;
        }
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
            var useCases = scope.ServiceProvider.GetRequiredService<ClayWeighingUseCases>();

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
                var cutOrderId = SelectedVessel?.CutOrderId;
                if (cutOrderId.HasValue)
                {
                    await ReloadVesselAndTripsAsync(cutOrderId.Value, dialogVm.SessionId);
                }
                else
                {
                    await LoadVesselsAsync(loadTripsForSelectedVessel: false);
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

    public void Dispose()
    {
        _deviceConnector.Dispose();
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


