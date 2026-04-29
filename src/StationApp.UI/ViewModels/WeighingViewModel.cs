using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Printing;
using StationApp.Application.UseCases;
using StationApp.Application.UseCases.MasterData;
using StationApp.Device.Abstractions;
using StationApp.Device.Models;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.UI.Printing;
using StationApp.UI.Services;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels;

public partial class WeighingViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly IToastService _toastService;
    private readonly IClock _clock;
    private readonly ILogger<WeighingViewModel>? _logger;
    private readonly IDialogService _dialogService;

    private readonly Helpers.LatestScaleReadingSnapshot _scaleSnapshot = new();
    private readonly SemaphoreSlim _ticketLoadGate = new(1, 1);
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private CancellationTokenSource? _initializationCts;
    private Task? _initializationTask;
    private bool _initializeRequested;
    private int _uiThrottleMs = 200;
    private DateTime _lastUiUpdate = DateTime.MinValue;
    private int _isUiUpdatePending;
    private static readonly SolidColorBrush _stableBrush = new(Color.FromRgb(46, 213, 115));
    private static readonly SolidColorBrush _unstableBrush = new(Colors.Orange);

    [ObservableProperty] private decimal _currentWeight;
    [ObservableProperty] private bool _isStable;
    [ObservableProperty] private string _stabilityText = "UNSTABLE";
    [ObservableProperty] private SolidColorBrush _stabilityBrush = new(Colors.Orange);
    [ObservableProperty] private string _currentCaptureMode = "AUTO";
    [ObservableProperty] private string _deviceStatusText = "Dang khoi tao...";
    [ObservableProperty] private bool _isDeviceConnected;
    [ObservableProperty] private bool _isInitializing;
    [ObservableProperty] private bool _isTicketsLoading;
    [ObservableProperty] private string? _initializationError;

    [ObservableProperty] private string? _searchErpVehicleRegistrationId;
    [ObservableProperty] private string? _searchVehiclePlate;

    [ObservableProperty] private string? _vehiclePlate;
    [ObservableProperty] private string? _moocNumber;
    [ObservableProperty] private string? _driverName;
    [ObservableProperty] private decimal? _ttcpWeight;
    [ObservableProperty] private string? _erpVehicleRegistrationId;
    [ObservableProperty] private TransportMethod? _transportMethod;
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private string? _productCode;
    [ObservableProperty] private decimal? _plannedWeight;
    [ObservableProperty] private int? _bagCount;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private bool _isCancelled;

    [ObservableProperty] private string? _vehicleRegistrationNo;
    [ObservableProperty] private DateTime? _vehicleRegistrationExpiry;
    [ObservableProperty] private string? _moocRegistrationNo;
    [ObservableProperty] private DateTime? _moocRegistrationExpiry;
    [ObservableProperty] private bool _isVehicleRegistrationExpired;
    [ObservableProperty] private bool _isMoocRegistrationExpired;
    [ObservableProperty] private string? _registrationWarningMessage;
    
    private WeightMode _capturedWeight1Mode = WeightMode.AUTO;
    private bool _capturedWeight1IsStable = true;
    private WeightMode _capturedWeight2Mode = WeightMode.AUTO;
    private bool _capturedWeight2IsStable = true;

    private System.Threading.CancellationTokenSource? _searchCts;

    public bool IsAutoMode => CurrentCaptureMode == "AUTO";
    public bool IsManualMode => CurrentCaptureMode == "MANUAL";

    public bool IsPlateReadOnly => !IsManualMode || !string.IsNullOrWhiteSpace(ErpVehicleRegistrationId);
    public bool IsCustomerReadOnly => !IsManualMode || !string.IsNullOrWhiteSpace(ErpVehicleRegistrationId);
    public bool IsProductReadOnly => !IsManualMode || !string.IsNullOrWhiteSpace(ErpVehicleRegistrationId);
    public bool IsMoocReadOnly => !IsManualMode;
    public bool IsDriverReadOnly => !IsManualMode;
    public bool IsPlannedWeightReadOnly => !IsManualMode;
    public bool IsBagCountReadOnly => !IsManualMode;
    public bool IsNotesReadOnly => false;

    public decimal DisplayTtcpKg => (TtcpWeight ?? 0);
    public decimal DisplayTtcp10PercentKg => ((TtcpWeight ?? 0) * 1.10m);
    public string DisplayPlannedWeightCombined => PlannedWeight.HasValue 
        ? $"{PlannedWeight.Value:N0} kg ({PlannedWeight.Value / 1000m:N2} tấn)" 
        : string.Empty;
    public string TransportMethodDisplay => TransportMethod?.ToString() ?? "N/A";

    [ObservableProperty] private string? _ticketNo;
    [ObservableProperty] private decimal? _weight1;
    [ObservableProperty] private decimal? _weight2;
    [ObservableProperty] private decimal? _netWeight;

    public decimal DisplayWeight1 => (Weight1 ?? 0);
    public decimal DisplayWeight2 => (Weight2 ?? 0);
    public decimal DisplayNetWeight => (NetWeight ?? 0);

    [ObservableProperty] private ObservableCollection<string> _vehicleSuggestions = new();
    [ObservableProperty] private ObservableCollection<string> _moocOptions = new();
    [ObservableProperty] private ObservableCollection<Customer> _customerSuggestions = new();
    [ObservableProperty] private ObservableCollection<Product> _productSuggestions = new();

    [ObservableProperty] private ObservableCollection<WeightViewListItem> _tickets = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CaptureWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(CaptureWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintDeliveryTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintWeighTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowRelatedTicketsCommand))]
    private WeightViewListItem? _selectedTicket;

    [ObservableProperty] private int _statsOutboundRoadCount;
    [ObservableProperty] private int _statsOutboundWaterCount;
    [ObservableProperty] private int _statsInboundCount;

    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
    private readonly EventHandler _clockTickHandler;
    [ObservableProperty] private string _currentTimeDisplay = DateTime.Now.ToString("HH:mm:ss tt");

    [ObservableProperty] private bool _isOverweightModalVisible;
    [ObservableProperty] private string? _overweightWarningMessage;
    [ObservableProperty] private bool _isRelatedTicketsVisible;
    [ObservableProperty] private ObservableCollection<RelatedDocumentListItem> _relatedTickets = new();

    public WeighingViewModel(IServiceScopeFactory scopeFactory, IScaleDevice scaleDevice, IToastService toastService, IDialogService dialogService, IClock clock, ILogger<WeighingViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
        _toastService = toastService;
        _dialogService = dialogService;
        _clock = clock;
        _logger = logger;
        _scaleDevice.WeightReceived += OnWeightReceived;

        UpdateStats();

        _clockTickHandler = (_, _) => CurrentTimeDisplay = DateTime.Now.ToString("HH:mm:ss tt");
        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += _clockTickHandler;
        _clockTimer.Start();
    }

    partial void OnCurrentCaptureModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsAutoMode));
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(IsPlateReadOnly));
        OnPropertyChanged(nameof(IsCustomerReadOnly));
        OnPropertyChanged(nameof(IsProductReadOnly));
        OnPropertyChanged(nameof(IsMoocReadOnly));
        OnPropertyChanged(nameof(IsDriverReadOnly));
        OnPropertyChanged(nameof(IsPlannedWeightReadOnly));
        OnPropertyChanged(nameof(IsBagCountReadOnly));
        OnPropertyChanged(nameof(IsNotesReadOnly));
        UpdateCommandCanExecuteStates();
    }

    partial void OnSearchErpVehicleRegistrationIdChanged(string? value) => DebounceSearch();
    partial void OnSearchVehiclePlateChanged(string? value) => DebounceSearch();
    partial void OnWeight1Changed(decimal? value) => UpdateCommandCanExecuteStates();
    partial void OnWeight2Changed(decimal? value) => UpdateCommandCanExecuteStates();

    private void DebounceSearch()
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;

        Task.Delay(500, token).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && !token.IsCancellationRequested)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(async () =>
                {
                    await LoadTicketsCoreAsync(CancellationToken.None);
                });
            }
        }, TaskScheduler.Default);
    }

    private void UpdateCommandCanExecuteStates()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            CaptureWeight1Command.NotifyCanExecuteChanged();
            CaptureWeight2Command.NotifyCanExecuteChanged();
            PrintWeighTicketCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            PrintDeliveryTicketCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            ShowRelatedTicketsCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanCaptureWeight1() => SelectedTicket != null && SelectedTicket.RegistrationStatus == RegistrationStatus.REGISTERED;
    private bool CanCaptureWeight2() => SelectedTicket != null && SelectedTicket.RegistrationStatus == RegistrationStatus.LOADING_IN_PROGRESS;
    private bool CanPrintWeighTicket() => SelectedTicket != null && SelectedTicket.RegistrationStatus == RegistrationStatus.COMPLETED;
    private bool CanCancel() => SelectedTicket != null
        && (SelectedTicket.RegistrationStatus == RegistrationStatus.REGISTERED
            || SelectedTicket.RegistrationStatus == RegistrationStatus.LOADING_IN_PROGRESS);
    private bool CanPrintDeliveryTicket() => SelectedTicket != null && SelectedTicket.RegistrationStatus == RegistrationStatus.COMPLETED;
    private bool CanSave()
    {
        if (SelectedTicket == null) return false;
        if (SelectedTicket.RegistrationStatus == RegistrationStatus.REGISTERED)
        {
            return Weight1.HasValue && Weight1.Value > 0;
        }
        if (SelectedTicket.RegistrationStatus == RegistrationStatus.LOADING_IN_PROGRESS)
        {
            return Weight2.HasValue && Weight2.Value > 0;
        }
        return false;
    }
    private bool CanShowRelatedTickets() => SelectedTicket != null && SelectedTicket.RegistrationStatus != RegistrationStatus.CANCELLED;

    private void UpdateStats()
    {
        StatsOutboundRoadCount = Tickets.Count(t => t.TransactionType == TransactionType.OUTBOUND && t.TransportMethod == StationApp.Domain.Enums.TransportMethod.ROAD);
        StatsOutboundWaterCount = Tickets.Count(t => t.TransactionType == TransactionType.OUTBOUND && t.TransportMethod == StationApp.Domain.Enums.TransportMethod.WATERWAY);
        StatsInboundCount = Tickets.Count(t => t.TransactionType == TransactionType.INBOUND);
    }

    private void OnWeightReceived(object? sender, ScaleReading reading)
    {
        lock (_scaleSnapshot)
        {
            _scaleSnapshot.Weight = reading.Weight;
            _scaleSnapshot.IsStable = reading.IsStable;
            _scaleSnapshot.ReceivedAt = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastUiUpdate).TotalMilliseconds < _uiThrottleMs && reading.IsStable == IsStable)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _isUiUpdatePending, 1, 0) != 0)
        {
            return;
        }

        _lastUiUpdate = now;
        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            try
            {
                decimal currentWeightToRender;
                bool isStableToRender;

                lock (_scaleSnapshot)
                {
                    currentWeightToRender = _scaleSnapshot.Weight;
                    isStableToRender = _scaleSnapshot.IsStable;
                }

                if (CurrentCaptureMode == "AUTO")
                {
                    CurrentWeight = currentWeightToRender;
                    IsStable = isStableToRender;
                    StabilityText = isStableToRender ? "STABLE" : "UNSTABLE";
                    StabilityBrush = isStableToRender ? _stableBrush : _unstableBrush;
                }
                else
                {
                    IsStable = true;
                    StabilityText = "MANUAL";
                    StabilityBrush = _stableBrush;
                }

                IsDeviceConnected = _scaleDevice.IsConnected;
                DeviceStatusText = _scaleDevice.IsConnected ? "Dang hoat dong" : "Mat ket noi";

                CheckOverweight();
            }
            finally
            {
                Interlocked.Exchange(ref _isUiUpdatePending, 0);
            }
        });
    }

    private void CheckOverweight()
    {
        if (!TtcpWeight.HasValue || TtcpWeight.Value <= 0)
        {
            return;
        }

        var limit = TtcpWeight.Value * 1.10m;
        if (CurrentWeight <= limit || IsOverweightModalVisible || CurrentCaptureMode != "AUTO")
        {
            return;
        }

        OverweightWarningMessage = $"Phat hien qua tai! Trong luong ({CurrentWeight:N0} kg) > 110% TTCP ({limit:N0} kg).";
        IsOverweightModalVisible = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetService<ILogger<WeighingViewModel>>();
            logger?.LogWarning("OVERWEIGHT ALERT: Vehicle={Plate}, Weight={Weight}kg, Limit={Limit}kg", VehiclePlate, CurrentWeight, limit);
        }
        catch
        {
        }
    }

    partial void OnSelectedTicketChanged(WeightViewListItem? value)
    {
        _ = LoadTicketDetailsAsync(value);
    }

    private async Task LoadTicketDetailsAsync(WeightViewListItem? value)
    {
        if (value == null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ClearDetailFields();
            });
            return;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            TicketNo = value.TicketNo;
            VehiclePlate = value.VehiclePlate;
            CustomerName = value.CustomerName;
            ProductCode = value.ProductName;
            PlannedWeight = value.PlannedWeight;
            BagCount = value.BagCount;
            Notes = value.Notes;
            IsCancelled = value.RegistrationStatus == RegistrationStatus.CANCELLED;
            Weight1 = value.Weight1;
            Weight2 = value.Weight2;
            NetWeight = value.NetWeight;
            TransportMethod = value.TransportMethod;
            ErpVehicleRegistrationId = value.ErpVehicleRegistrationId;

            MoocNumber = null;
            DriverName = value.WeighUser;
            TtcpWeight = null;
            VehicleRegistrationNo = null;
            VehicleRegistrationExpiry = null;
            MoocRegistrationNo = null;
            MoocRegistrationExpiry = null;
            
            OnPropertyChanged(nameof(IsPlateReadOnly));
            OnPropertyChanged(nameof(IsCustomerReadOnly));
            OnPropertyChanged(nameof(IsProductReadOnly));
            OnPropertyChanged(nameof(IsMoocReadOnly));
            OnPropertyChanged(nameof(IsDriverReadOnly));
            OnPropertyChanged(nameof(IsPlannedWeightReadOnly));
            OnPropertyChanged(nameof(IsBagCountReadOnly));
        });

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var regRepo = scope.ServiceProvider.GetRequiredService<IVehicleRegistrationRepository>();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            var registration = await regRepo.GetByIdAsync(value.RegistrationId, CancellationToken.None);
            if (registration != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MoocNumber = registration.MoocNumber;
                });

                var vehicle = await vehicleRepo.GetByPlateAndMoocAsync(value.VehiclePlate, registration.MoocNumber ?? "", CancellationToken.None);
                if (vehicle != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        DriverName = vehicle.DriverName;
                        TtcpWeight = vehicle.TtcpWeight;
                        VehicleRegistrationNo = vehicle.VehicleRegistrationNo;
                        VehicleRegistrationExpiry = vehicle.VehicleRegistrationExpiryDate;
                        MoocRegistrationNo = vehicle.MoocRegistrationNo;
                        MoocRegistrationExpiry = vehicle.MoocRegistrationExpiryDate;
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load detailed registration/master data for {Id}", value.RegistrationId);
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            CheckExpiry();
            UpdateCommandCanExecuteStates();
            OnPropertyChanged(nameof(DisplayTtcpKg));
            OnPropertyChanged(nameof(DisplayTtcp10PercentKg));
            OnPropertyChanged(nameof(DisplayPlannedWeightCombined));
            OnPropertyChanged(nameof(DisplayWeight1));
            OnPropertyChanged(nameof(DisplayWeight2));
            OnPropertyChanged(nameof(DisplayNetWeight));
            OnPropertyChanged(nameof(TransportMethodDisplay));
        });
    }

    private void ClearDetailFields()
    {
        TicketNo = null;
        VehiclePlate = null;
        MoocNumber = null;
        DriverName = null;
        TtcpWeight = null;
        ErpVehicleRegistrationId = null;
        TransportMethod = null;
        CustomerName = null;
        ProductCode = null;
        PlannedWeight = null;
        BagCount = null;
        Notes = null;
        IsCancelled = false;
        Weight1 = null;
        Weight2 = null;
        NetWeight = null;

        VehicleRegistrationNo = null;
        VehicleRegistrationExpiry = null;
        MoocRegistrationNo = null;
        MoocRegistrationExpiry = null;
        
        OnPropertyChanged(nameof(IsPlateReadOnly));
        OnPropertyChanged(nameof(IsCustomerReadOnly));
        OnPropertyChanged(nameof(IsProductReadOnly));
        OnPropertyChanged(nameof(IsMoocReadOnly));
        OnPropertyChanged(nameof(IsDriverReadOnly));
        OnPropertyChanged(nameof(IsPlannedWeightReadOnly));
        OnPropertyChanged(nameof(IsBagCountReadOnly));
    }

    private void CheckExpiry()
    {
        var now = _clock.TodayLocal;
        IsVehicleRegistrationExpired = VehicleRegistrationExpiry.HasValue && VehicleRegistrationExpiry.Value < now;
        IsMoocRegistrationExpired = MoocRegistrationExpiry.HasValue && MoocRegistrationExpiry.Value < now;
        RegistrationWarningMessage = IsVehicleRegistrationExpired || IsMoocRegistrationExpired
            ? "Xe / Mooc da het han dang kiem!"
            : null;
    }

    [RelayCommand]
    private async Task SearchVehiclesAsync(string? text)
    {
        try
        {
            using (Helpers.PerformanceLogger.Track("Search / Autocomplete Duration"))
            {
                using var scope = _scopeFactory.CreateScope();
                var uc = scope.ServiceProvider.GetRequiredService<SearchVehicleSuggestionsUseCase>();
                var suggestions = await uc.ExecuteAsync(text, CancellationToken.None);
                VehicleSuggestions = new ObservableCollection<string>(suggestions);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SearchVehicles failed");
            _toastService.ShowError("Không thể tải dữ liệu tìm kiếm. Vui lòng thử lại.");
        }
    }

    [RelayCommand]
    private async Task SelectVehicleAsync(string plate)
    {
        VehiclePlate = plate;
        using var scope = _scopeFactory.CreateScope();
        var uc = scope.ServiceProvider.GetRequiredService<SearchVehicleMoocOptionsUseCase>();
        var options = await uc.ExecuteAsync(plate, CancellationToken.None);
        MoocOptions = new ObservableCollection<string>(options.Select(o => o.MoocNumber).Distinct());
    }

    [RelayCommand]
    private async Task SelectMoocAsync(string mooc)
    {
        MoocNumber = mooc;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var vehicle = await repo.GetByPlateAndMoocAsync(VehiclePlate!, mooc, CancellationToken.None);
        if (vehicle == null)
        {
            return;
        }

        DriverName = vehicle.DriverName;
        TtcpWeight = vehicle.TtcpWeight;
        VehicleRegistrationNo = vehicle.VehicleRegistrationNo;
        VehicleRegistrationExpiry = vehicle.VehicleRegistrationExpiryDate;
        MoocRegistrationNo = vehicle.MoocRegistrationNo;
        MoocRegistrationExpiry = vehicle.MoocRegistrationExpiryDate;

        if (Enum.TryParse<TransportMethod>(vehicle.TransportMethod, out var transportMethod))
        {
            TransportMethod = transportMethod;
        }
        else
        {
            TransportMethod = null;
        }

        CheckExpiry();
        OnPropertyChanged(nameof(DisplayTtcpKg));
        OnPropertyChanged(nameof(DisplayTtcp10PercentKg));
    }

    [RelayCommand]
    private async Task LoadTicketsAsync()
    {
        await LoadTicketsCoreAsync(CancellationToken.None);
    }

    private async Task LoadTicketsCoreAsync(CancellationToken ct)
    {
        await _ticketLoadGate.WaitAsync(ct);
        try
        {
            IsTicketsLoading = true;
            using (Helpers.PerformanceLogger.Track("WeightView Load Duration"))
            using (Helpers.PerformanceLogger.Track("WeightView - Grid Data"))
            {
                using var scope = _scopeFactory.CreateScope();
                var uc = scope.ServiceProvider.GetRequiredService<GetWeightViewTicketsUseCase>();
                var keyword = !string.IsNullOrWhiteSpace(SearchVehiclePlate)
                    ? SearchVehiclePlate
                    : SearchErpVehicleRegistrationId;

                var currentSelectionId = SelectedTicket?.RegistrationId;

                var list = await uc.ExecuteAsync(keyword, ct);
                Tickets = new ObservableCollection<WeightViewListItem>(list);
                UpdateStats();

                if (currentSelectionId.HasValue)
                {
                    SelectedTicket = Tickets.FirstOrDefault(x => x.RegistrationId == currentSelectionId.Value);
                }

                if (list.Count == 0 && !string.IsNullOrWhiteSpace(keyword))
                {
                    _toastService.ShowInfo("Không tìm thấy dữ liệu phù hợp.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadTicketsCore failed");
            _toastService.ShowError("Không thể tải dữ liệu tìm kiếm. Vui lòng thử lại.");
        }
        finally
        {
            IsTicketsLoading = false;
            _ticketLoadGate.Release();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCaptureWeight1))]
    private Task CaptureWeight1Async()
    {
        if (SelectedTicket is null)
        {
            _toastService.ShowWarning("Vui lòng chọn một phiếu để thao tác.");
            return Task.CompletedTask;
        }

        decimal weightToCapture;
        bool isStableToCapture;
        
        if (CurrentCaptureMode == "AUTO")
        {
            lock (_scaleSnapshot)
            {
                weightToCapture = _scaleSnapshot.Weight;
                isStableToCapture = _scaleSnapshot.IsStable;
            }
        }
        else
        {
            weightToCapture = CurrentWeight;
            isStableToCapture = true;
        }

        if (CurrentCaptureMode == "AUTO" && (!IsDeviceConnected || weightToCapture <= 0))
        {
            _toastService.ShowError("Chưa nhận được dữ liệu cân hợp lệ từ thiết bị.");
            return Task.CompletedTask;
        }
        else if (CurrentCaptureMode != "AUTO" && weightToCapture <= 0)
        {
            _toastService.ShowError("Vui lòng nhập số cân lần 1 hợp lệ.");
            return Task.CompletedTask;
        }

        Weight1 = weightToCapture;
        _capturedWeight1Mode = CurrentCaptureMode == "AUTO" ? WeightMode.AUTO : WeightMode.MANUAL;
        _capturedWeight1IsStable = isStableToCapture;
        
        _toastService.ShowSuccess("Đã lấy số cân lần 1 lên màn hình (vui lòng nhấn LƯU để xác nhận).");
        UpdateCommandCanExecuteStates();
        return Task.CompletedTask;
    }


    [RelayCommand(CanExecute = nameof(CanCaptureWeight2))]
    private Task CaptureWeight2Async()
    {
        if (SelectedTicket is null)
        {
            _toastService.ShowWarning("Vui lòng chọn một phiếu để thao tác.");
            return Task.CompletedTask;
        }

        decimal weightToCapture;
        bool isStableToCapture;
        
        if (CurrentCaptureMode == "AUTO")
        {
            lock (_scaleSnapshot)
            {
                weightToCapture = _scaleSnapshot.Weight;
                isStableToCapture = _scaleSnapshot.IsStable;
            }
        }
        else
        {
            weightToCapture = CurrentWeight;
            isStableToCapture = true;
        }

        if (CurrentCaptureMode == "AUTO" && (!IsDeviceConnected || weightToCapture <= 0))
        {
            _toastService.ShowError("Chưa nhận được dữ liệu cân hợp lệ từ thiết bị.");
            return Task.CompletedTask;
        }
        else if (CurrentCaptureMode != "AUTO" && weightToCapture <= 0)
        {
            _toastService.ShowError("Vui lòng nhập số cân lần 2 hợp lệ.");
            return Task.CompletedTask;
        }

        Weight2 = weightToCapture;
        _capturedWeight2Mode = CurrentCaptureMode == "AUTO" ? WeightMode.AUTO : WeightMode.MANUAL;
        _capturedWeight2IsStable = isStableToCapture;
        
        _toastService.ShowSuccess("Đã lấy số cân lần 2 lên màn hình (vui lòng nhấn LƯU để xác nhận).");
        UpdateCommandCanExecuteStates();
        return Task.CompletedTask;
    }



    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync()
    {
        if (SelectedTicket is null)
        {
            _toastService.ShowWarning("Vui lòng chọn một phiếu để thao tác.");
            return;
        }

        if (SelectedTicket.RegistrationStatus == RegistrationStatus.CANCELLED)
        {
            _toastService.ShowWarning("Phiếu đã hủy.");
            return;
        }

        if (SelectedTicket.RegistrationStatus == RegistrationStatus.COMPLETED)
        {
            _toastService.ShowWarning("Phiếu đã hoàn thành, không thể hủy.");
            return;
        }

        if (SelectedTicket.RegistrationStatus != RegistrationStatus.REGISTERED
            && SelectedTicket.RegistrationStatus != RegistrationStatus.LOADING_IN_PROGRESS)
        {
            _toastService.ShowWarning("Phiếu ở trạng thái hiện tại không thể hủy.");
            return;
        }

        var confirm = await _dialogService.ShowConfirmAsync(
            "Xác nhận hủy phiếu", 
            "Bạn có chắc muốn hủy phiếu này không? Tất cả phiếu cân và phiếu giao nhận liên quan sẽ bị xóa.", 
            "Hủy phiếu", 
            "Không"
        );

        if (!confirm) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<CancelTicketUseCase>();
            await uc.ExecuteAsync(new CancelTicketRequest(SelectedTicket.RegistrationId), CancellationToken.None);
            _toastService.ShowSuccess("Đã hủy phiếu thành công.");
            await LoadTicketsCoreAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CancelTicket failed");
            _toastService.ShowError("Không thể hủy phiếu. Vui lòng thử lại.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (SelectedTicket is null)
        {
            _toastService.ShowWarning("Vui lòng chọn một phiếu để thao tác.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            
            if (SelectedTicket.RegistrationStatus == RegistrationStatus.REGISTERED)
            {
                if (!Weight1.HasValue || Weight1.Value <= 0)
                {
                    _toastService.ShowWarning("Vui lòng lấy số cân lần 1 hợp lệ.");
                    return;
                }
                
                var uc = scope.ServiceProvider.GetRequiredService<CaptureWeight1UseCase>();
                await uc.ExecuteAsync(new CaptureWeightRequest(
                    SelectedTicket.RegistrationId, 
                    Weight1.Value, 
                    _capturedWeight1IsStable, 
                    _capturedWeight1Mode
                ), CancellationToken.None);
                
                _toastService.ShowSuccess("Đã lưu thông tin cân lần 1 thành công.");
            }
            else if (SelectedTicket.RegistrationStatus == RegistrationStatus.LOADING_IN_PROGRESS)
            {
                if (!Weight2.HasValue || Weight2.Value <= 0)
                {
                    _toastService.ShowWarning("Vui lòng lấy số cân lần 2 hợp lệ.");
                    return;
                }

                decimal netWeight = Math.Abs((Weight1 ?? 0m) - Weight2.Value);
                decimal allowedThreshold = (TtcpWeight ?? PlannedWeight ?? 0m) * 1.10m;

                if (netWeight <= allowedThreshold)
                {
                    var uc = scope.ServiceProvider.GetRequiredService<CaptureWeight2UseCase>();
                    await uc.ExecuteAsync(new CaptureWeightRequest(
                        SelectedTicket.RegistrationId, 
                        Weight2.Value, 
                        _capturedWeight2IsStable, 
                        _capturedWeight2Mode
                    ), CancellationToken.None);

                    var completeUc = scope.ServiceProvider.GetRequiredService<CompleteTicketUseCase>();
                    await completeUc.ExecuteAsync(new CompleteTicketRequest(SelectedTicket.RegistrationId), CancellationToken.None);
                    
                    _toastService.ShowSuccess("Đã lưu thông tin cân lần 2 và hoàn tất phiếu.");
                }
                else
                {
                    var result = await _dialogService.ShowConfirmAsync(
                        "Cảnh báo quá tải", 
                        "Trọng lượng hàng vượt TTCP 10%. Bạn có muốn tách phiếu cân không?", 
                        "Tách phiếu", 
                        "Không"
                    );

                    if (result)
                    {
                        var splitUc = scope.ServiceProvider.GetRequiredService<SplitOverweightTicketUseCase>();
                        await splitUc.ExecuteAsync(new SplitOverweightTicketRequest(
                            SelectedTicket.RegistrationId,
                            Weight2.Value,
                            _capturedWeight2IsStable,
                            _capturedWeight2Mode
                        ), CancellationToken.None);

                        _toastService.ShowSuccess("Đã thực hiện tách phiếu ngay trong transaction thành công.");
                    }
                    else
                    {
                        var withoutSplitUc = scope.ServiceProvider.GetRequiredService<CompleteOverweightTicketWithoutSplitUseCase>();
                        await withoutSplitUc.ExecuteAsync(new CompleteOverweightTicketWithoutSplitRequest(
                            SelectedTicket.RegistrationId,
                            Weight2.Value,
                            _capturedWeight2IsStable,
                            _capturedWeight2Mode
                        ), CancellationToken.None);

                        _toastService.ShowSuccess("Đã lưu thông tin cân lần 2 mà không tách phiếu quá tải.");
                    }
                }
            }
            else
            {
                _toastService.ShowWarning("Phiếu ở trạng thái hiện tại không cho phép chỉnh sửa.");
                return;
            }

            await LoadTicketsCoreAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Save failed");
            _toastService.ShowError("Không thể lưu dữ liệu cân. Vui lòng kiểm tra lại kết nối hoặc dữ liệu.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrintDeliveryTicket))]
    private async Task PrintDeliveryTicketAsync()
    {
        if (SelectedTicket is null)
        {
            _toastService.ShowWarning("Vui lòng chọn một phiếu để thao tác.");
            return;
        }

        if (SelectedTicket.RegistrationStatus == RegistrationStatus.CANCELLED)
        {
            _toastService.ShowWarning("Phiếu đã hủy, không thể in phiếu giao nhận.");
            return;
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var ensureUc = scope.ServiceProvider.GetRequiredService<EnsurePrimaryDeliveryTicketUseCase>();
            await ensureUc.ExecuteAsync(SelectedTicket.RegistrationId, CancellationToken.None);
        }

        _toastService.ShowInfo("Đang mở cấu hình in phiếu giao nhận...");
        await ExecutePrintFlowAsync(PrintDocumentKind.DeliveryTicket, SelectedTicket.RegistrationId, "phiếu giao nhận");
    }

    [RelayCommand(CanExecute = nameof(CanPrintWeighTicket))]
    private async Task PrintWeighTicketAsync()
    {
        if (SelectedTicket is null)
        {
            _toastService.ShowWarning("Vui lòng chọn một phiếu để thao tác.");
            return;
        }

        if (SelectedTicket.RegistrationStatus == RegistrationStatus.CANCELLED)
        {
            _toastService.ShowWarning("Phiếu đã hủy, không thể in phiếu cân.");
            return;
        }

        _toastService.ShowInfo("Đang mở cấu hình in phiếu cân...");
        await ExecutePrintFlowAsync(PrintDocumentKind.WeighTicket, SelectedTicket.RegistrationId, "phiếu cân");
    }

    private async Task ExecutePrintFlowAsync(PrintDocumentKind kind, Guid registrationId, string documentDisplayName)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = await LoadPrintContextAsync(scope, registrationId);
            if (context == null)
            {
                _toastService.ShowWarning($"Chưa có {documentDisplayName} để in.");
                return;
            }

            var templateProvider = scope.ServiceProvider.GetRequiredService<IPrintTemplateProvider>();
            var printerDiscovery = scope.ServiceProvider.GetRequiredService<IPrinterDiscoveryService>();
            var printService = scope.ServiceProvider.GetRequiredService<IPrintService>();
            var renderer = scope.ServiceProvider.GetRequiredService<PrintOverlayRenderer>();
            var template = await templateProvider.GetTemplateAsync(kind, CancellationToken.None);
            var preview = BuildPrintBatchPreview(scope, context, kind);

            if (preview.Pages.Count == 0)
            {
                _toastService.ShowWarning($"Chưa có {documentDisplayName} để in.");
                return;
            }

            var dialogVm = new PrintOptionsDialogViewModel(
                kind == PrintDocumentKind.WeighTicket ? "In phiếu cân" : "In phiếu giao nhận",
                template,
                preview,
                printerDiscovery.GetInstalledPrinters(),
                renderer);

            var printOptions = await _dialogService.ShowCustomDialogAsync<PrintOptionsDialogViewModel, PrintOptionsModel>(dialogVm);
            if (printOptions == null)
            {
                return;
            }

            _toastService.ShowInfo($"Đang in {documentDisplayName}...");
            var result = await printService.PrintAsync(template, preview, printOptions, CancellationToken.None);
            await PersistPrintResultAsync(scope, context, kind, result);

            if (result.HasFailures)
            {
                _toastService.ShowError($"Không thể in {documentDisplayName}. Vui lòng kiểm tra máy in.");
                return;
            }

            _toastService.ShowSuccess(kind == PrintDocumentKind.WeighTicket
                ? "Đã in phiếu cân thành công."
                : "Đã in phiếu giao nhận thành công.");

            // Auto-move to OUT_YARD if all documents are printed
            try
            {
                using var moveScope = _scopeFactory.CreateScope();
                var moveUc = moveScope.ServiceProvider.GetRequiredService<TryMoveToOutYardUseCase>();
                var moved = await moveUc.ExecuteAsync(registrationId, CancellationToken.None);
                if (moved)
                {
                    _toastService.ShowSuccess("Xe đã hoàn tất - tự động chuyển sang Danh sách xe ra.");
                    await LoadTicketsCoreAsync(CancellationToken.None);
                }
            }
            catch (Exception moveEx)
            {
                _logger?.LogWarning(moveEx, "TryMoveToOutYard failed after print for {RegistrationId}", registrationId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Print flow failed for {Kind}", kind);
            _toastService.ShowError(kind == PrintDocumentKind.WeighTicket
                ? "Không thể in phiếu cân. Vui lòng kiểm tra máy in."
                : "Không thể in phiếu giao nhận. Vui lòng kiểm tra máy in.");
        }
    }

    private async Task<PrintContext?> LoadPrintContextAsync(IServiceScope scope, Guid registrationId)
    {
        var regRepo = scope.ServiceProvider.GetRequiredService<IVehicleRegistrationRepository>();
        var weighRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        var registration = await regRepo.GetByIdAsync(registrationId, CancellationToken.None);
        if (registration == null)
        {
            return null;
        }

        var weighTickets = (await weighRepo.GetByVehicleRegistrationIdAsync(registrationId, CancellationToken.None))
            .Where(t => string.Equals(t.RecordRole, "WORKING", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t.TicketNo))
            .OrderBy(t => t.SplitSequence ?? 0)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        var deliveryTickets = (await deliveryRepo.GetByVehicleRegistrationIdAsync(registrationId, CancellationToken.None))
            .Where(t => string.Equals(t.RecordRole, "WORKING", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t.DeliveryNo))
            .OrderBy(t => t.SplitSequence ?? 0)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        var vehicle = await vehicleRepo.GetByPlateAndMoocAsync(registration.VehiclePlate, registration.MoocNumber ?? string.Empty, CancellationToken.None)
            ?? (await vehicleRepo.GetByPlateAsync(registration.VehiclePlate, CancellationToken.None)).FirstOrDefault();

        return new PrintContext(registration, vehicle, weighTickets, deliveryTickets);
    }

    private PrintBatchPreviewModel BuildPrintBatchPreview(IServiceScope scope, PrintContext context, PrintDocumentKind kind)
    {
        var printedAtLocal = _clock.NowLocal;
        if (kind == PrintDocumentKind.WeighTicket)
        {
            var composer = scope.ServiceProvider.GetRequiredService<IWeighTicketPrintComposer>();
            return new PrintBatchPreviewModel
            {
                Kind = kind,
                Title = "In phiếu cân",
                Pages = context.WeighTickets
                    .Select(ticket => (PrintPreviewPageModel)composer.Compose(context.Registration, ticket, context.Vehicle, printedAtLocal))
                    .ToList()
            };
        }

        var deliveryComposer = scope.ServiceProvider.GetRequiredService<IDeliveryTicketPrintComposer>();
        var weighBySequence = context.WeighTickets.ToDictionary(t => t.SplitSequence ?? 0, t => t);
        var primaryWeigh = context.WeighTickets.FirstOrDefault();
        return new PrintBatchPreviewModel
        {
            Kind = kind,
            Title = "In phiếu giao nhận",
            Pages = context.DeliveryTickets
                .Select(ticket =>
                {
                    weighBySequence.TryGetValue(ticket.SplitSequence ?? 0, out var matchedWeigh);
                    matchedWeigh ??= primaryWeigh;
                    return (PrintPreviewPageModel)deliveryComposer.Compose(context.Registration, ticket, matchedWeigh, context.Vehicle, printedAtLocal);
                })
                .ToList()
        };
    }

    private async Task PersistPrintResultAsync(IServiceScope scope, PrintContext context, PrintDocumentKind kind, PrintExecutionResult result)
    {
        var now = _clock.NowLocal;
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUserContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await uow.ExecuteInTransactionAsync(async innerCt =>
        {
            if (kind == PrintDocumentKind.WeighTicket)
            {
                var repo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
                foreach (var ticket in context.WeighTickets)
                {
                    var ticketResult = result.Documents.FirstOrDefault(x => x.DocumentId == ticket.Id);
                    if (ticketResult == null)
                    {
                        continue;
                    }

                    if (ticketResult.Success)
                    {
                        ticket.IsPrinted = true;
                        ticket.LastPrintedAt = now;
                        ticket.LastPrintError = null;
                    }
                    else
                    {
                        ticket.LastPrintError = ticketResult.ErrorMessage;
                    }

                    ticket.UpdatedAt = now;
                    ticket.UpdatedBy = user.Username;
                    await repo.UpdateAsync(ticket, innerCt);
                }

                return;
            }

            var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
            foreach (var ticket in context.DeliveryTickets)
            {
                var ticketResult = result.Documents.FirstOrDefault(x => x.DocumentId == ticket.Id);
                if (ticketResult == null)
                {
                    continue;
                }

                if (ticketResult.Success)
                {
                    ticket.IsPrinted = true;
                    ticket.LastPrintedAt = now;
                    ticket.LastPrintError = null;
                }
                else
                {
                    ticket.LastPrintError = ticketResult.ErrorMessage;
                }

                ticket.UpdatedAt = now;
                ticket.UpdatedBy = user.Username;
                await deliveryRepo.UpdateAsync(ticket, innerCt);
            }
        }, CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanShowRelatedTickets))]
    private async Task ShowRelatedTicketsAsync()
    {
        if (SelectedTicket is null)
        {
            _toastService.ShowWarning("Vui lòng chọn một phiếu để thao tác.");
            return;
        }

        if (SelectedTicket.RegistrationStatus == RegistrationStatus.CANCELLED)
        {
            _toastService.ShowWarning("Phiếu đã hủy, không thể mở danh sách phiếu liên quan.");
            return;
        }

        try
        {
            _toastService.ShowInfo("Đang mở danh sách phiếu liên quan.");
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<GetRelatedTicketsUseCase>();
            var list = await uc.ExecuteAsync(SelectedTicket.RegistrationId, CancellationToken.None);
            if (list.Count == 0)
            {
                _toastService.ShowWarning("Phiếu hiện chưa có chứng từ liên quan.");
            }
            RelatedTickets = new ObservableCollection<RelatedDocumentListItem>(list);
            IsRelatedTicketsVisible = true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ShowRelatedTickets failed");
            _toastService.ShowError("Không thể tải dữ liệu tìm kiếm. Vui lòng thử lại.");
        }
    }

    [RelayCommand]
    private void CloseRelatedTickets() => IsRelatedTicketsVisible = false;

    [RelayCommand]
    private void CloseOverweightModal() => IsOverweightModalVisible = false;

    public Task InitializeAsync()
    {
        if (_initializeRequested)
        {
            return Task.CompletedTask;
        }

        _initializeRequested = true;
        _initializationCts?.Cancel();
        _initializationCts = new CancellationTokenSource();
        IsInitializing = true;
        DeviceStatusText = _scaleDevice.IsConnected ? "Dang hoat dong" : "Dang ket noi nen...";
        _initializationTask = InitializeCoreAsync(_initializationCts.Token);
        return Task.CompletedTask;
    }

    private async Task InitializeCoreAsync(CancellationToken ct)
    {
        await _initializationGate.WaitAsync(ct);
        try
        {
            using (Helpers.PerformanceLogger.Track("WeightView - Initialize Shell"))
            {
                await LoadUiThrottleAsync(ct);
            }

            var ticketLoadTask = LoadTicketsCoreAsync(ct);
            var deviceAttachTask = AttachDeviceAsync(ct);

            await Task.WhenAll(ticketLoadTask, deviceAttachTask);
            InitializationError = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger?.LogDebug("WeighingView initialization cancelled.");
        }
        catch (Exception ex)
        {
            InitializationError = ex.Message;
            DeviceStatusText = "Khoi tao loi";
            _logger?.LogError(ex, "WeighingView initialization failed.");
        }
        finally
        {
            IsInitializing = false;
            _initializationGate.Release();
        }
    }

    private async Task LoadUiThrottleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var appRepo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var throttleStr = await appRepo.GetValueAsync("device_ui_throttle_ms", ct);
        if (int.TryParse(throttleStr, out var throttleVal) && throttleVal >= 0)
        {
            _uiThrottleMs = throttleVal;
        }
    }

    private async Task AttachDeviceAsync(CancellationToken ct)
    {
        using (Helpers.PerformanceLogger.Track("WeightView - Device Attach"))
        {
            if (_scaleDevice.IsConnected)
            {
                DeviceStatusText = "Dang hoat dong";
                IsDeviceConnected = true;
                return;
            }

            DeviceStatusText = "Dang ket noi can...";
            await _scaleDevice.ConnectAsync(ct);
            await _scaleDevice.StartAsync(ct);
            IsDeviceConnected = _scaleDevice.IsConnected;
            DeviceStatusText = _scaleDevice.IsConnected ? "Dang hoat dong" : "Mat ket noi";
        }
    }

    private sealed record PrintContext(
        VehicleRegistration Registration,
        Vehicle? Vehicle,
        IReadOnlyList<WeighTicket> WeighTickets,
        IReadOnlyList<DeliveryTicket> DeliveryTickets);

    public void Dispose()
    {
        _initializationCts?.Cancel();
        _scaleDevice.WeightReceived -= OnWeightReceived;
        _clockTimer.Stop();
        _clockTimer.Tick -= _clockTickHandler;
        _initializationCts?.Dispose();
    }
}
