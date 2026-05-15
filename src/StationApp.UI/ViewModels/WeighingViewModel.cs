using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
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
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels;

public partial class WeighingViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScaleDevice _scaleDevice;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<WeighingViewModel>? _logger;
    private Guid? _focusSessionId;
    private decimal? _pendingCapturedWeight1;
    private decimal? _pendingCapturedWeight2;
    private bool _pendingWeight1IsStable;
    private bool _pendingWeight2IsStable;
    private WeightMode _pendingWeight1Mode = WeightMode.AUTO;
    private WeightMode _pendingWeight2Mode = WeightMode.AUTO;
    private bool _isUpdatingOverweightSplitInputs;
    private int _overweightPreviewRequestVersion;

    public event Action? NavigateToOutgoingRequested;

    private static readonly SolidColorBrush StableBrush = new(Color.FromRgb(46, 213, 115));
    private static readonly SolidColorBrush UnstableBrush = new(Colors.Orange);

    [ObservableProperty] private ObservableCollection<WeighingSessionListItem> _sessions = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CaptureWeight1Command))]
    [NotifyCanExecuteChangedFor(nameof(CaptureWeight2Command))]
    [NotifyCanExecuteChangedFor(nameof(SaveCapturedWeightCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenAllocationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowOverweightHandlingCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintWeighTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintDeliveryTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveToOutYardCommand))]
    [NotifyCanExecuteChangedFor(nameof(MarkNoLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowRelatedTicketsCommand))]
    private WeighingSessionListItem? _selectedSession;

    [ObservableProperty] private ObservableCollection<WeighingSessionLineRow> _sessionLines = new();
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty] private string? _searchVehiclePlate;

    [ObservableProperty] private string? _sessionNo;
    [ObservableProperty] private TransactionType _transactionType;
    [ObservableProperty] private string? _vehiclePlate;
    [ObservableProperty] private string? _moocNumber;
    [ObservableProperty] private string? _driverName;
    [ObservableProperty] private string? _customerSummary;
    [ObservableProperty] private string? _productSummary;
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

    [ObservableProperty] private bool _isAllocationVisible;
    [ObservableProperty] private ObservableCollection<WeighingSessionLineRow> _allocationLines = new();
    [ObservableProperty] private bool _isRelatedTicketsVisible;
    [ObservableProperty] private ObservableCollection<RelatedDocumentListItem> _relatedTickets = new();
    [ObservableProperty] private bool _isOverweightHandlingVisible;
    [ObservableProperty] private ObservableCollection<OverweightSplitPreviewGroupItem> _overweightPreviewGroups = new();
    [ObservableProperty] private ObservableCollection<OverweightSplitPreviewLineItem> _overweightPreviewLines = new();

    public bool IsAutoMode => CurrentCaptureMode == "TỰ ĐỘNG";
    public bool IsManualMode => CurrentCaptureMode == "CÂN TAY";
    public bool CanUseManualMode => StationAuthorization.CanUseManualWeighing(_currentUserContext.RoleCode);

    public WeighingViewModel(
        IServiceScopeFactory scopeFactory,
        IScaleDevice scaleDevice,
        IToastService toastService,
        IDialogService dialogService,
        IClock clock,
        ICurrentUserContext currentUserContext,
        ILogger<WeighingViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _scaleDevice = scaleDevice;
        _toastService = toastService;
        _dialogService = dialogService;
        _clock = clock;
        _currentUserContext = currentUserContext;
        _logger = logger;

        _scaleDevice.WeightReceived += OnWeightReceived;
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
        _ = LoadSelectedSessionAsync(value);
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
            await AttachDeviceAsync();
            await LoadSessionsAsync();
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
        SearchVehiclePlate = null;
        _focusSessionId = null;
        IsAllocationVisible = false;
        IsRelatedTicketsVisible = false;
        ResetOverweightHandlingState();
        IsOverweightHandlingVisible = false;
        SelectedSession = null;
        await LoadSessionsInternalAsync(false);
    }

    private async Task LoadSessionsInternalAsync(bool selectFirstWhenNoSelection)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<GetWeighingSessionsUseCase>();
            var list = await uc.ExecuteAsync(null, CancellationToken.None);
            var filtered = list.Where(x =>
                MatchesSearch(x.SessionNo, SearchSessionNo)
                && MatchesSearch(x.VehiclePlate, SearchVehiclePlate));
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

    private async Task LoadSelectedSessionAsync(WeighingSessionListItem? value)
    {
        if (value == null)
        {
            ClearSelectionDetails();
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();

        var lineItems = await sessionRepo.GetLineItemsBySessionIdAsync(value.SessionId, CancellationToken.None);

        SessionNo = value.SessionNo;
        TransactionType = value.TransactionType;
        VehiclePlate = value.VehiclePlate;
        MoocNumber = value.MoocNumber;
        DriverName = value.DriverName;
        Weight1 = value.Weight1;
        Weight2 = value.Weight2;
        NetWeight = value.NetWeight;
        Ttcp10WeightSnapshot = value.Ttcp10WeightSnapshot;
        IsOverweight = value.IsOverweight;
        OverweightAmount = value.OverweightAmount;
        OverweightSplitStepWeight = 0m;
        SessionStatusText = SessionStatusMapper.ToDisplayString(value.SessionStatus);
        OverweightResolutionText = OverweightResolutionStatusMapper.ToDisplayString(value.OverweightResolutionStatus);
        CustomerSummary = string.Join(" / ", lineItems.Select(x => x.CustomerName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        ProductSummary = string.Join(" / ", lineItems.Select(x => x.ProductName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        SessionLines = new ObservableCollection<WeighingSessionLineRow>(lineItems.Select(x => new WeighingSessionLineRow(x)));
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
        SessionLines = new ObservableCollection<WeighingSessionLineRow>();
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
    private bool CanOpenAllocation() => SelectedSession?.SessionStatus is WeighingSessionStatus.ALLOCATION_PENDING or WeighingSessionStatus.READY_TO_COMPLETE;
    private bool CanShowOverweightHandling() =>
        SelectedSession != null
        && SelectedSession.IsOverweight
        && SelectedSession.OverweightResolutionStatus == OverweightResolutionStatus.PENDING;
    private bool CanPrintWeighTicket() => SelectedSession != null && SelectedSession.SessionStatus != WeighingSessionStatus.PENDING_WEIGHT1;
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
        && SelectedSession.SessionStatus != WeighingSessionStatus.COMPLETED
        && SelectedSession.SessionStatus != WeighingSessionStatus.CANCELLED;
    private bool CanCancel() => SelectedSession != null && SelectedSession.SessionStatus != WeighingSessionStatus.COMPLETED && SelectedSession.SessionStatus != WeighingSessionStatus.CANCELLED;
    private bool CanShowRelatedTickets() => SelectedSession != null;
    private bool CanSuggestOverweightSplitAgain() => SelectedSession != null && IsOverweightHandlingVisible;

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
                await uc.ExecuteAsync(
                    new CaptureSessionWeightRequest(
                        SelectedSession.SessionId,
                        _pendingCapturedWeight2!.Value,
                        _pendingWeight2IsStable,
                        _pendingWeight2Mode),
                    CancellationToken.None);

                _toastService.ShowSuccess(UiText.Weighing.Weight2Saved);
            }

            ClearPendingCapturedWeights();
            await FocusSessionAsync(SelectedSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Save captured weight failed");
            _toastService.ShowError(UiText.Weighing.LoadSessionsError);
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

    [RelayCommand]
    private void AllocateByPlan()
    {
        if (!NetWeight.HasValue || AllocationLines.Count == 0)
        {
            return;
        }

        var totalPlanned = AllocationLines.Sum(x => x.PlannedWeight ?? 0m);
        decimal allocated = 0m;
        for (var i = 0; i < AllocationLines.Count; i++)
        {
            var row = AllocationLines[i];
            if (i == AllocationLines.Count - 1)
            {
                row.ActualAllocatedWeight = NetWeight.Value - allocated;
            }
            else if (totalPlanned > 0)
            {
                var proportional = decimal.Round(NetWeight.Value * ((row.PlannedWeight ?? 0m) / totalPlanned), 3, MidpointRounding.AwayFromZero);
                row.ActualAllocatedWeight = proportional;
                allocated += proportional;
            }
            else
            {
                var even = decimal.Round(NetWeight.Value / AllocationLines.Count, 3, MidpointRounding.AwayFromZero);
                row.ActualAllocatedWeight = even;
                allocated += even;
            }
        }
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
                    AllocationLines.Select(x => new AllocateWeighingSessionLineRequest(
                        x.SessionLineId,
                        x.ActualAllocatedWeight,
                        x.ActualAllocatedBagCount)).ToList()),
                CancellationToken.None);

            _toastService.ShowSuccess(UiText.Weighing.AllocationSaved);
            IsAllocationVisible = false;
            await FocusSessionAsync(SelectedSession.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Confirm allocation failed");
            _toastService.ShowError("Không thể lưu phân bổ thực giao. Vui lòng thử lại.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanShowOverweightHandling))]
    private async Task ShowOverweightHandlingAsync()
    {
        if (SelectedSession == null)
        {
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
        _toastService.ShowSuccess("Đã cập nhật xử lý quá tải.");
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
        _toastService.ShowSuccess("Đã cập nhật xử lý quá tải.");
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
            SetOverweightSplitValidation("Khoi luong tach phai la so hop le.");
            return;
        }

        var firstSplitWeight = isFirstTicket
            ? editedWeight
            : decimal.Round(NetWeight.Value - editedWeight, 3, MidpointRounding.AwayFromZero);

        await RefreshOverweightPreviewAsync(isManualOverride: true, firstSplitWeight: firstSplitWeight);
    }

    private async Task RefreshOverweightPreviewAsync(bool isManualOverride, decimal? firstSplitWeight)
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

            ApplyOverweightPreview(preview);
        }
        catch (InvalidOperationException ex)
        {
            if (requestVersion != _overweightPreviewRequestVersion)
            {
                return;
            }

            ApplyOverweightPreviewError(isManualOverride, firstSplitWeight, ex.Message);
        }
    }

    private void ApplyOverweightPreview(OverweightSplitPreviewDto preview)
    {
        _isUpdatingOverweightSplitInputs = true;
        try
        {
            OverweightSplitStepWeight = preview.OverweightSplitStepWeight;
            OverweightSplitTicket1WeightText = FormatOverweightSplitWeight(preview.SplitTicket1NetWeight);
            OverweightSplitTicket2WeightText = FormatOverweightSplitWeight(preview.SplitTicket2NetWeight);
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

    private void ApplyOverweightPreviewError(bool isManualOverride, decimal? firstSplitWeight, string message)
    {
        IsManualSplitOverride = isManualOverride;
        OverweightSplitModeText = SplitModeDisplayMapper.ToDisplayString(isManualOverride);
        OverweightSplitRandomFactorText = isManualOverride ? "Tùy chỉnh tay" : "--";

        if (firstSplitWeight.HasValue)
        {
            _isUpdatingOverweightSplitInputs = true;
            try
            {
                OverweightSplitTicket1WeightText = FormatOverweightSplitWeight(firstSplitWeight.Value);
                if (NetWeight.HasValue)
                {
                    OverweightSplitTicket2WeightText = FormatOverweightSplitWeight(
                        decimal.Round(NetWeight.Value - firstSplitWeight.Value, 3, MidpointRounding.AwayFromZero));
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

        if (decimal.TryParse(value, out parsedWeight))
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

        await ExecutePrintFlowAsync(PrintDocumentKind.DeliveryTicket, SelectedSession.SessionId, "phiếu giao nhận");
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
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<CompleteWeighingSessionUseCase>();
            await uc.ExecuteAsync(SelectedSession.SessionId, CancellationToken.None);
            _toastService.ShowSuccess(UiText.Weighing.MoveOutSuccess);
            await LoadSessionsAsync();
            NavigateToOutgoingRequested?.Invoke();
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
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<MarkWeighingSessionNoLoadUseCase>();
            await uc.ExecuteAsync(new MarkWeighingSessionNoLoadRequest(SelectedSession.SessionId), CancellationToken.None);
            _toastService.ShowSuccess("Đã chuyển xe sang danh sách xe ra theo luồng không lấy hàng.");
            await LoadSessionsAsync();
            NavigateToOutgoingRequested?.Invoke();
        }
        catch (InvalidOperationException ex)
        {
            _toastService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MarkNoLoad failed");
            _toastService.ShowError("Không thể chuyển xe ra theo luồng không lấy hàng.");
        }
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
        _toastService.ShowSuccess(UiText.Weighing.CancelSuccess);
        await LoadSessionsAsync();
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
                .ThenBy(x => x.SplitSequence ?? byte.MaxValue)
                .ThenBy(x => x.CreatedAt));

        IsRelatedTicketsVisible = true;
    }

    [RelayCommand]
    private void CloseRelatedTickets()
    {
        IsRelatedTicketsVisible = false;
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
            var template = await templateProvider.GetTemplateAsync(kind, CancellationToken.None);
            var preview = BuildPrintBatchPreview(scope, context, kind);
            if (preview.Pages.Count == 0)
            {
                _toastService.ShowWarning(string.Format(UiText.Weighing.NoPrintableDocumentFormat, displayName));
                return;
            }

            var dialogVm = new PrintOptionsDialogViewModel(
                kind == PrintDocumentKind.WeighTicket ? UiText.Weighing.PrintDialogWeighTicket : UiText.Weighing.PrintDialogDeliveryTicket,
                template,
                preview,
                printerDiscovery.GetInstalledPrinters(),
                renderer);

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
        var regRepo = scope.ServiceProvider.GetRequiredService<IVehicleRegistrationRepository>();
        var weighRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        var session = await sessionRepo.GetByIdAsync(sessionId, CancellationToken.None);
        if (session == null)
        {
            return null;
        }

        var lines = await sessionRepo.GetLinesBySessionIdAsync(sessionId, CancellationToken.None);
        var registrations = await regRepo.GetByWeighingSessionIdAsync(sessionId, CancellationToken.None);
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
            var primaryRegistration = context.RegistrationsById.Values.OrderBy(x => x.CreatedAt).FirstOrDefault();
            if (primaryRegistration == null)
            {
                return new PrintBatchPreviewModel { Kind = kind, Title = UiText.Weighing.PrintPreviewWeigh, Pages = [] };
            }

            var ticketsToPrint = splitConfirmed
                ? context.WeighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived && !x.IsDeleted).OrderBy(x => x.SplitSequence).ToList()
                : context.WeighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.MasterSession && !x.IsDeleted).Take(1).ToList();

            return new PrintBatchPreviewModel
            {
                Kind = kind,
                Title = splitConfirmed ? "In phiếu cân tách tải" : UiText.Weighing.PrintPreviewWeighMaster,
                Pages = ticketsToPrint.Select(ticket => composer.Compose(primaryRegistration, ticket, context.Vehicle, printedAtLocal)).Cast<PrintPreviewPageModel>().ToList()
            };
        }

        var deliveryComposer = scope.ServiceProvider.GetRequiredService<IDeliveryTicketPrintComposer>();
        var pages = new List<PrintPreviewPageModel>();
        var deliveryTicketsToPrint = splitConfirmed
            ? context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived && !x.IsDeleted)
                .OrderBy(x => x.SplitSequence).ThenBy(x => x.CreatedAt)
            : context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.Normal && !x.IsDeleted)
                .OrderBy(x => x.WeighingSessionLineId).ThenBy(x => x.CreatedAt);

        foreach (var ticket in deliveryTicketsToPrint)
        {
            if (!ticket.WeighingSessionLineId.HasValue)
            {
                continue;
            }

            var line = context.Lines.FirstOrDefault(x => x.Id == ticket.WeighingSessionLineId.Value);
            if (line == null || !context.RegistrationsById.TryGetValue(line.VehicleRegistrationId, out var registration))
            {
                continue;
            }

            var relatedWeighTicket = splitConfirmed
                ? context.WeighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived && x.SplitGroupId == ticket.SplitGroupId && !x.IsDeleted)
                : context.WeighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession && !x.IsDeleted);

            pages.Add(deliveryComposer.Compose(registration, ticket, relatedWeighTicket, line, context.Vehicle, printedAtLocal));
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
                    : context.WeighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.MasterSession && !x.IsDeleted);

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

                if (!splitConfirmed && !result.HasFailures)
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

    private void OnWeightReceived(object? sender, ScaleReading reading)
    {
        if (IsAutoMode)
        {
            CurrentWeight = reading.Weight;
            IsStable = reading.IsStable;
            StabilityText = reading.IsStable ? "ỔN ĐỊNH" : "CHƯA ỔN ĐỊNH";
            StabilityBrush = reading.IsStable ? StableBrush : UnstableBrush;
        }
        else
        {
            IsStable = true;
            StabilityText = "CÂN TAY";
            StabilityBrush = StableBrush;
        }

        IsDeviceConnected = _scaleDevice.IsConnected;
        DeviceStatusText = _scaleDevice.IsConnected ? UiText.Weighing.ActiveConnection : UiText.Weighing.LostConnection;
    }

    public void Dispose()
    {
        _scaleDevice.WeightReceived -= OnWeightReceived;
    }

    private sealed record SessionPrintContext(
        WeighingSession MasterSession,
        IReadOnlyList<WeighingSessionLine> Lines,
        IReadOnlyDictionary<Guid, VehicleRegistration> RegistrationsById,
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
        VehicleRegistrationId = item.VehicleRegistrationId;
        SequenceNo = item.SequenceNo;
        ErpVehicleRegistrationId = item.ErpVehicleRegistrationId;
        CustomerName = item.CustomerName;
        DistributorName = item.DistributorName;
        ProductCode = item.ProductCode;
        ProductName = item.ProductName;
        PlannedWeight = item.PlannedWeight;
        PlannedBagCount = item.PlannedBagCount;
        ActualAllocatedWeight = item.ActualAllocatedWeight;
        ActualAllocatedBagCount = item.ActualAllocatedBagCount;
        LineStatus = item.LineStatus;
        HasPrintedDeliveryTicket = item.HasPrintedDeliveryTicket;
    }

    [ObservableProperty] private decimal? _actualAllocatedWeight;
    [ObservableProperty] private int? _actualAllocatedBagCount;

    public Guid SessionLineId { get; }
    public Guid VehicleRegistrationId { get; }
    public int SequenceNo { get; }
    public string? ErpVehicleRegistrationId { get; }
    public string? CustomerName { get; }
    public string? DistributorName { get; }
    public string? ProductCode { get; }
    public string? ProductName { get; }
    public decimal? PlannedWeight { get; }
    public int? PlannedBagCount { get; }
    public WeighingSessionLineStatus LineStatus { get; }
    public bool HasPrintedDeliveryTicket { get; set; }

    partial void OnActualAllocatedWeightChanged(decimal? value)
    {
        if (!value.HasValue || value <= 0)
        {
            ActualAllocatedBagCount = null;
            return;
        }

        ActualAllocatedBagCount = (int)decimal.Round(value.Value / DefaultBagWeightKg, 0, MidpointRounding.AwayFromZero);
    }

    public WeighingSessionLineRow Clone()
    {
        return new WeighingSessionLineRow(new WeighingSessionLineItem(
            SessionLineId,
            VehicleRegistrationId,
            SequenceNo,
            ErpVehicleRegistrationId,
            CustomerName,
            DistributorName,
            ProductCode,
            ProductName,
            PlannedWeight,
            PlannedBagCount,
            ActualAllocatedWeight,
            ActualAllocatedBagCount,
            LineStatus,
            HasPrintedDeliveryTicket));
    }
}
