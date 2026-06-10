using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Application.UseCases.MasterData;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Constants;
using StationApp.UI.Resources;
using StationApp.UI.Services;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels;

public partial class IncomingVehicleListViewModel : ObservableObject
{
    private static readonly TimeSpan ReuseWeight1Window = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<IncomingVehicleListViewModel>? _logger;
    private CancellationTokenSource? _customerCodeLookupCts;
    private IAsyncRelayCommand? _markNoLoadCommand;

    public event Action<Guid>? NavigateToWeighingRequested;
    public event Action? NavigateToOutgoingRequested;
    public event Action<Guid>? NavigateToExportWeighingRequested;

    [ObservableProperty] private ObservableCollection<IncomingVehicleSelectionItem> _vehicles = new();
    [ObservableProperty] private IncomingVehicleSelectionItem? _selectedVehicle;
    [ObservableProperty] private string? _searchErpCutOrderId;
    [ObservableProperty] private string? _searchVehiclePlate;
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _isCreateMode;
    [ObservableProperty] private Guid? _editingCutOrderId;
    [ObservableProperty] private string? _formErpCutOrderId;
    [ObservableProperty] private string? _formVehiclePlate;
    [ObservableProperty] private string? _formMoocNumber;
    [ObservableProperty] private string? _formDriverName;
    [ObservableProperty] private string? _formCustomerCode;
    [ObservableProperty] private string? _formCustomerName;
    [ObservableProperty] private string? _formProductCode;
    [ObservableProperty] private string? _formProductName;
    [ObservableProperty] private decimal? _formPlannedWeight;
    [ObservableProperty] private int? _formBagCount;
    [ObservableProperty] private bool _isFormProductBagged = true;
    [ObservableProperty] private string? _formNotes;
    [ObservableProperty] private bool _formIsCancelled;
    [ObservableProperty] private TransportMethod? _formTransportMethod = TransportMethod.ROAD;
    [ObservableProperty] private TransactionType _formTransactionType = TransactionType.INBOUND;

    [ObservableProperty] private decimal? _ttcpWeight;
    [ObservableProperty] private string? _vehicleRegistrationNo;
    [ObservableProperty] private DateTime? _vehicleRegistrationExpiry;
    [ObservableProperty] private string? _moocRegistrationNo;
    [ObservableProperty] private DateTime? _moocRegistrationExpiry;
    [ObservableProperty] private string? _formAttachSessionNo;
    [ObservableProperty] private string? _formConsumptionPlace;
    [ObservableProperty] private string? _formMarket;

    public AutocompleteInputViewModel SearchVehiclePlateInput { get; }
    public AutocompleteInputViewModel FormVehiclePlateInput { get; }
    public AutocompleteInputViewModel FormMoocInput { get; }
    public AutocompleteInputViewModel FormDriverInput { get; }
    public AutocompleteInputViewModel FormCustomerCodeInput { get; }
    public AutocompleteInputViewModel FormCustomerInput { get; }
    public AutocompleteInputViewModel FormProductCodeInput { get; }
    public AutocompleteInputViewModel FormProductNameInput { get; }

    public string SaveButtonText => "LƯU THAY ĐỔI";
    public bool IsDetailSelectionMode => !IsCreateMode && SelectedVehicle != null;
    public bool CanConfirmEnterWeighing => Vehicles.Any(x => x.IsSelected) || (!IsCreateMode && SelectedVehicle != null) || (IsCreateMode && !string.IsNullOrWhiteSpace(FormVehiclePlate));
    public bool CanMarkNoLoad => Vehicles.Any(x => x.IsSelected) || (!IsCreateMode && SelectedVehicle != null);
    public bool CanTransitionToExportScale
    {
        get
        {
            var selected = ResolveSingleExportScaleCandidate();
            return selected != null
                && selected.TransactionType == TransactionType.OUTBOUND
                && selected.CutOrderStatus == CutOrderStatus.REGISTERED
                && !selected.IsExportScale;
        }
    }

    public IAsyncRelayCommand MarkNoLoadCommand => _markNoLoadCommand ??= new AsyncRelayCommand(MarkNoLoadAsync, () => CanMarkNoLoad);
    public decimal DisplayTtcp10PercentKg => ((TtcpWeight ?? 0m) * 1.10m);
    public bool IsOutboundDetailLockMode => !IsCreateMode && FormTransactionType == TransactionType.OUTBOUND;
    public bool CanEditNonRegistrationFields => !IsOutboundDetailLockMode;
    public bool IsVehicleRegistrationExpired => IsExpiredDate(VehicleRegistrationExpiry);
    public bool IsMoocRegistrationExpired => IsExpiredDate(MoocRegistrationExpiry);

    public IncomingVehicleListViewModel(
        IServiceScopeFactory scopeFactory,
        IToastService toastService,
        IDialogService dialogService,
        ILogger<IncomingVehicleListViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _toastService = toastService;
        _dialogService = dialogService;
        _logger = logger;

        SearchVehiclePlateInput = CreateAutocompleteField(AutocompleteFieldType.Vehicle, 1, item => SearchVehiclePlate = item.Value);

        FormVehiclePlateInput = CreateAutocompleteField(AutocompleteFieldType.Vehicle, 1, ApplyVehicleSelection);
        FormMoocInput = CreateAutocompleteField(AutocompleteFieldType.Mooc, 1, ApplyMoocSelection);
        FormDriverInput = CreateAutocompleteField(AutocompleteFieldType.Driver, 2, item => FormDriverName = item.Value);
        FormCustomerCodeInput = CreateAutocompleteField(AutocompleteFieldType.CustomerCode, 1, ApplyCustomerSelection);
        FormCustomerInput = CreateAutocompleteField(AutocompleteFieldType.Customer, 2, ApplyCustomerSelection);
        FormProductCodeInput = CreateAutocompleteField(AutocompleteFieldType.ProductCode, 1, ApplyProductSelection);
        FormProductNameInput = CreateAutocompleteField(AutocompleteFieldType.ProductName, 2, ApplyProductSelection);

        WireTextState(SearchVehiclePlateInput, value => SearchVehiclePlate = value);
        WireTextState(FormVehiclePlateInput, value => FormVehiclePlate = value);
        WireTextState(FormMoocInput, value => FormMoocNumber = value);
        WireTextState(FormDriverInput, value => FormDriverName = value);
        WireTextState(FormCustomerCodeInput, value => FormCustomerCode = value);
        WireTextState(FormCustomerInput, value => FormCustomerName = value);
        WireTextState(FormProductCodeInput, value => FormProductCode = value);
        WireTextState(FormProductNameInput, value => FormProductName = value);

        BeginCreateMode();
    }

    partial void OnSelectedVehicleChanged(IncomingVehicleSelectionItem? value)
    {
        if (value == null)
        {
            return;
        }

        _ = LoadSelectedVehicleDetailsAsync(value);
        RefreshCreateSessionState();
    }

    partial void OnIsCreateModeChanged(bool value)
    {
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(IsDetailSelectionMode));
        OnPropertyChanged(nameof(IsOutboundDetailLockMode));
        OnPropertyChanged(nameof(CanEditNonRegistrationFields));
        RefreshCreateSessionState();
    }

    partial void OnFormTransactionTypeChanged(TransactionType value)
    {
        OnPropertyChanged(nameof(IsOutboundDetailLockMode));
        OnPropertyChanged(nameof(CanEditNonRegistrationFields));
    }

    partial void OnFormVehiclePlateChanged(string? value)
    {
        RefreshCreateSessionState();
    }

    partial void OnVehicleRegistrationExpiryChanged(DateTime? value)
    {
        RefreshRegistrationExpiryState();
    }

    partial void OnMoocRegistrationExpiryChanged(DateTime? value)
    {
        RefreshRegistrationExpiryState();
    }

    partial void OnFormCustomerCodeChanged(string? value)
    {
        _ = SyncCustomerByCodeAsync(value);
    }

    [RelayCommand]
    private async Task LoadVehiclesAsync()
    {
        using var perfScope = Helpers.PerformanceLogger.Track("IncomingVehicles.LoadVehicles");
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
            var list = await repo.GetIncomingListAsync(
                new IncomingVehicleListFilter(
                    SearchErpCutOrderId,
                    SearchVehiclePlate,
                    null,
                    null,
                    null,
                    null,
                    null),
                CancellationToken.None);

            var selectedIds = Vehicles.Where(x => x.IsSelected).Select(x => x.CutOrderId).ToHashSet();
            Vehicles = new ObservableCollection<IncomingVehicleSelectionItem>(
                list.Select(x =>
                {
                    var item = new IncomingVehicleSelectionItem(x);
                    item.IsSelected = selectedIds.Contains(x.CutOrderId);
                    item.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(IncomingVehicleSelectionItem.IsSelected))
                        {
                            RefreshCreateSessionState();
                        }
                    };
                    return item;
                }));

            if (list.Count == 0 && HasSearchFilters())
            {
                _toastService.ShowInfo(UiText.Common.NoMatchingData);
            }

            if (SelectedVehicle != null)
            {
                SelectedVehicle = Vehicles.FirstOrDefault(x => x.CutOrderId == SelectedVehicle.CutOrderId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadIncomingVehicles failed");
            _toastService.ShowError(UiText.Common.SearchIncomingLoadError);
        }
        finally
        {
            IsLoading = false;
            RefreshCreateSessionState();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        BeginCreateMode();
        await LoadVehiclesAsync();
    }

    [RelayCommand]
    private async Task SaveDetailAsync()
    {
        if (!ValidateIncomingDetailForm())
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();

            if (IsCreateMode)
            {
                var result = await CreateInboundRegistrationFromFormAsync(scope.ServiceProvider, CancellationToken.None);

                if (!result.Success)
                {
                    _toastService.ShowError(result.ErrorMessage ?? UiText.Incoming.CreateInboundError);
                    return;
                }

                _toastService.ShowSuccess(UiText.Incoming.CreateInboundSuccess);
                await LoadVehiclesAsync();

                if (result.Data != null)
                {
                    SelectedVehicle = Vehicles.FirstOrDefault(x => x.CutOrderId == result.Data.Id);
                }

                IsCreateMode = false;
                return;
            }

            if (!EditingCutOrderId.HasValue)
            {
                _toastService.ShowWarning(UiText.Incoming.UpdateSelectionRequired);
                return;
            }

            var notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim();
            var updateUseCase = scope.ServiceProvider.GetRequiredService<UpdateIncomingRegistrationUseCase>();
            var updateResult = await updateUseCase.ExecuteAsync(new UpdateIncomingRegistrationRequest(
                CutOrderId: EditingCutOrderId.Value,
                VehiclePlate: FormVehiclePlate!,
                TransactionType: FormTransactionType,
                TransportMethod: FormTransportMethod,
                MoocNumber: FormMoocNumber,
                ReceiverName: FormDriverName,
                CustomerCode: FormCustomerCode,
                CustomerName: FormCustomerName,
                ProductCode: FormProductCode,
                ProductName: FormProductName,
                ProductType: null,
                PlannedWeight: FormPlannedWeight,
                BagCount: FormBagCount,
                Notes: notes,
                IsCancelled: FormIsCancelled,
                TtcpWeight: TtcpWeight,
                VehicleRegistrationNo: VehicleRegistrationNo,
                VehicleRegistrationExpiryDate: VehicleRegistrationExpiry,
                MoocRegistrationNo: MoocRegistrationNo,
                MoocRegistrationExpiryDate: MoocRegistrationExpiry
            ), CancellationToken.None);

            if (!updateResult.Success)
            {
                _toastService.ShowError(updateResult.ErrorMessage ?? UiText.Incoming.UpdateInboundError);
                return;
            }

            _toastService.ShowSuccess(UiText.Incoming.UpdateInboundSuccess);
            await LoadVehiclesAsync();

            if (SelectedVehicle != null)
            {
                await LoadSelectedVehicleDetailsAsync(SelectedVehicle);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Save incoming detail failed");
            _toastService.ShowError(UiText.Incoming.SaveInboundError);
        }
    }

    [RelayCommand]
    private void ResetDetail()
    {
        if (IsCreateMode)
        {
            ClearForm();
            FormTransactionType = TransactionType.INBOUND;
            FormTransportMethod = TransportMethod.ROAD;
            return;
        }

        if (SelectedVehicle != null)
        {
            _ = LoadSelectedVehicleDetailsAsync(SelectedVehicle);
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmEnterWeighing))]
    private async Task ConfirmEnterWeighingAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            List<IncomingVehicleSelectionItem> selectedVehicles;

            if (IsCreateMode)
            {
                if (!ValidateIncomingDetailForm())
                {
                    return;
                }

                var currentFormValidationMessage = ValidateCurrentFormRegistrationExpiryForCreateSession();
                if (currentFormValidationMessage != null)
                {
                    _toastService.ShowError(currentFormValidationMessage);
                    return;
                }

                var createResult = await CreateInboundRegistrationFromFormAsync(scope.ServiceProvider, CancellationToken.None);
                if (!createResult.Success || createResult.Data == null)
                {
                    _toastService.ShowError(createResult.ErrorMessage ?? UiText.Incoming.CreateInboundError);
                    return;
                }

                selectedVehicles = [BuildSelectionItem(createResult.Data)];
            }
            else
            {
                selectedVehicles = Vehicles.Where(x => x.IsSelected).ToList();
                if (selectedVehicles.Count == 0 && SelectedVehicle != null)
                {
                    selectedVehicles.Add(SelectedVehicle);
                }
            }

            var selectedIds = selectedVehicles.Select(x => x.CutOrderId).ToList();
            if (selectedIds.Count == 0)
            {
                _toastService.ShowWarning(UiText.Incoming.CreateSessionSelectionRequired);
                return;
            }

            var primaryVehicle = ResolvePrimaryVehicleSelection(selectedVehicles);
            if (primaryVehicle == null)
            {
                _toastService.ShowWarning(UiText.Incoming.CreateSessionSelectionRequired);
                return;
            }

            var distinctVehiclePlates = selectedVehicles
                .Select(x => x.VehiclePlate?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctVehiclePlates.Count > 1)
            {
                var selectionVm = new VehicleRepresentativeSelectionDialogViewModel(
                    selectedVehicles
                        .OrderBy(x => x.CreatedAt)
                        .ThenBy(x => x.ErpCutOrderId)
                        .Select(x => new VehicleRepresentativeOption(
                            x.CutOrderId,
                            x.VehiclePlate,
                            x.MoocNumber,
                            x.ReceiverName,
                            x.ErpCutOrderId,
                            x.CreatedAt))
                        .ToList(),
                    primaryVehicle.CutOrderId);

                var selectionResult = await _dialogService.ShowCustomDialogAsync<VehicleRepresentativeSelectionDialogViewModel, VehicleRepresentativeSelectionResult>(selectionVm);
                if (selectionResult == null)
                {
                    return;
                }

                primaryVehicle = selectedVehicles.FirstOrDefault(x => x.CutOrderId == selectionResult.CutOrderId);
                if (primaryVehicle == null)
                {
                    return;
                }
            }

            var formValidationMessage = ValidateCurrentFormRegistrationExpiryForCreateSession(selectedIds);
            if (formValidationMessage != null)
            {
                _toastService.ShowError(formValidationMessage);
                return;
            }

            var expiryValidationMessage = await ValidateRegistrationExpiryForCreateSessionAsync(
                scope.ServiceProvider.GetRequiredService<IVehicleRepository>(),
                selectedVehicles,
                CancellationToken.None);
            if (expiryValidationMessage != null)
            {
                _toastService.ShowError(expiryValidationMessage);
                return;
            }

            await PersistCurrentFormVehicleMasterDataForCreateSessionAsync(scope.ServiceProvider, selectedIds, CancellationToken.None);

            var attachSessionNo = string.IsNullOrWhiteSpace(FormAttachSessionNo) ? null : FormAttachSessionNo.Trim();
            if (!string.IsNullOrWhiteSpace(attachSessionNo))
            {
                var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
                var session = await sessionRepo.GetBySessionNoAsync(attachSessionNo, CancellationToken.None);
                if (session == null)
                {
                    _toastService.ShowError($"Kh\u00f4ng t\u00ecm th\u1ea5y l\u01b0\u1ee3t c\u00e2n {attachSessionNo} trong h\u1ec7 th\u1ed1ng.");
                    return;
                }

                var reuseCutoff = DateTime.Now.Subtract(ReuseWeight1Window);
                if (session.Weight1.HasValue
                    && (!session.Weight1Time.HasValue || session.Weight1Time.Value < reuseCutoff))
                {
                    _toastService.ShowError($"L\u01b0\u1ee3t c\u00e2n {session.SessionNo} \u0111\u00e3 qu\u00e1 24 gi\u1edd k\u1ec3 t\u1eeb th\u1eddi \u0111i\u1ec3m c\u00e2n l\u1ea7n 1, kh\u00f4ng \u0111\u01b0\u1ee3c ph\u00e9p d\u00f9ng l\u1ea1i.");
                    return;
                }

                var applyCarryForwardWeight1 = true;
                if (session.Weight1.HasValue)
                {
                    var carryForwardTimeText = session.Weight1Time.HasValue
                        ? session.Weight1Time.Value.ToString("dd/MM/yyyy HH:mm:ss")
                        : "kh\u00f4ng x\u00e1c \u0111\u1ecbnh";
                    var carryForwardVehiclePlate = string.IsNullOrWhiteSpace(primaryVehicle.VehiclePlate)
                        ? "kh\u00f4ng x\u00e1c \u0111\u1ecbnh"
                        : primaryVehicle.VehiclePlate.Trim();
                    var carryForwardConfirmResult = await _dialogService.ShowConfirmOrCloseAsync(
                        "X\u00e1c nh\u1eadn d\u00f9ng l\u1ea1i c\u00e2n l\u1ea7n 1",
                        $"Xe bi\u1ec3n s\u1ed1 {carryForwardVehiclePlate} v\u1eeba th\u1ef1c hi\u1ec7n c\u00e2n l\u1ea7n 1 v\u1edbi s\u1ed1 l\u01b0\u1ee3t c\u00e2n {session.SessionNo}, s\u1ed1 c\u00e2n {session.Weight1.Value:N0} kg v\u00e0o l\u00fac {carryForwardTimeText}. B\u1ea1n c\u00f3 \u0111\u1ed3ng \u00fd d\u00f9ng l\u1ea1i s\u1ed1 c\u00e2n l\u1ea7n 1 n\u00e0y kh\u00f4ng?",
                        "\u0110\u1ed3ng \u00fd",
                        "Kh\u00f4ng");
                    if (!carryForwardConfirmResult.HasValue)
                    {
                        return;
                    }

                    applyCarryForwardWeight1 = carryForwardConfirmResult.Value;
                }

                if (applyCarryForwardWeight1)
                {
                    var appendUc = scope.ServiceProvider.GetRequiredService<AppendCutOrdersToWeighingSessionUseCase>();
                    await appendUc.ExecuteAsync(
                        new AppendCutOrdersToWeighingSessionRequest(session.Id, selectedIds),
                        CancellationToken.None);

                    if (session.SessionStatus == WeighingSessionStatus.ALLOCATION_PENDING && session.NetWeight.HasValue)
                    {
                        var refreshedLines = await sessionRepo.GetLineItemsBySessionIdAsync(session.Id, CancellationToken.None);
                        if (refreshedLines.Count == 1 && !refreshedLines[0].ActualAllocatedWeight.HasValue)
                        {
                            var singleLine = refreshedLines[0];
                            var actualBagCount =
                                string.Equals(ProductTypes.Normalize(singleLine.ProductType), ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase)
                                && singleLine.PlannedBagCount.HasValue
                                    ? (int?)decimal.Floor(session.NetWeight.Value / 50m)
                                    : null;

                            var allocateUc = scope.ServiceProvider.GetRequiredService<AllocateWeighingSessionUseCase>();
                            await allocateUc.ExecuteAsync(
                                new AllocateWeighingSessionRequest(
                                    session.Id,
                                    new[]
                                    {
                                        new AllocateWeighingSessionLineRequest(
                                            singleLine.SessionLineId,
                                            session.NetWeight.Value,
                                            actualBagCount)
                                    }),
                                CancellationToken.None);
                        }
                    }

                    _toastService.ShowSuccess($"\u0110\u00e3 g\u1eafn c\u1eaft l\u1ec7nh v\u00e0o l\u01b0\u1ee3t c\u00e2n {session.SessionNo}.");
                    await LoadVehiclesAsync();
                    NavigateToWeighingRequested?.Invoke(session.Id);
                    return;
                }
            }

            var uc = scope.ServiceProvider.GetRequiredService<CreateWeighingSessionUseCase>();
            var result = await uc.ExecuteAsync(
                new CreateWeighingSessionRequest(selectedIds, primaryVehicle.CutOrderId, false),
                CancellationToken.None);

            _toastService.ShowSuccess(UiText.Incoming.CreateSessionSuccess);

            await LoadVehiclesAsync();
            NavigateToWeighingRequested?.Invoke(result.SessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CreateWeighingSession failed");
            _toastService.ShowError(ex.Message);
        }
    }

    private async Task MarkNoLoadAsync()
    {
        var selectedVehicles = Vehicles.Where(x => x.IsSelected).ToList();
        if (selectedVehicles.Count == 0 && SelectedVehicle != null)
        {
            selectedVehicles.Add(SelectedVehicle);
        }

        if (selectedVehicles.Count == 0)
        {
            _toastService.ShowWarning("Vui lòng chọn ít nhất một xe để chuyển ra.");
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            "Xác nhận không lấy hàng",
            "Xe đã chọn sẽ được chuyển thẳng sang danh sách xe ra với trạng thái không lấy hàng. Tiếp tục?",
            "Chuyển xe ra",
            UiText.Common.No);

        if (!confirmed)
        {
            return;
        }

        var primaryVehicle = ResolvePrimaryVehicleSelection(selectedVehicles);
        if (primaryVehicle == null)
        {
            _toastService.ShowWarning("Không xác định được xe đại diện để chuyển ra.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<MarkRegistrationsNoLoadUseCase>();
            await uc.ExecuteAsync(
                new MarkRegistrationsNoLoadRequest(
                    selectedVehicles.Select(x => x.CutOrderId).ToList(),
                    primaryVehicle.CutOrderId),
                CancellationToken.None);

            _toastService.ShowSuccess("Đã chuyển xe sang danh sách xe ra theo luồng không lấy hàng.");
            await LoadVehiclesAsync();
            NavigateToOutgoingRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Mark incoming registrations as no-load failed");
            _toastService.ShowError(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanTransitionToExportScale))]
    private async Task TransitionToExportScaleAsync()
    {
        var selected = ResolveSingleExportScaleCandidate();
        if (selected == null)
        {
            _toastService.ShowWarning("Vui l\u00f2ng ch\u1ecdn 1 c\u1eaft l\u1ec7nh xu\u1ea5t h\u00e0ng \u0111ang ch\u1edd xe v\u00e0o.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
            var temporaryOptions = await repo.GetActiveTemporaryExportCutOrderOptionsAsync(selected.CutOrderId, CancellationToken.None);

            if (temporaryOptions.Count > 0)
            {
                var dialogVm = new TemporaryExportCutOrderMapDialogViewModel(temporaryOptions);
                var mapResult = await _dialogService.ShowCustomDialogAsync<TemporaryExportCutOrderMapDialogViewModel, TemporaryExportCutOrderMapDialogResult>(dialogVm);
                if (mapResult == null)
                {
                    return;
                }

                if (!mapResult.SkipMapping && mapResult.TemporaryCutOrderId.HasValue)
                {
                    var mapUc = scope.ServiceProvider.GetRequiredService<MapTemporaryExportCutOrderUseCase>();
                    await mapUc.ExecuteAsync(
                        new MapTemporaryExportCutOrderRequest(mapResult.TemporaryCutOrderId.Value, selected.CutOrderId),
                        CancellationToken.None);

                    _toastService.ShowSuccess("Đã map cắt lệnh tạm sang cắt lệnh thật.");
                    await LoadVehiclesAsync();
                    NavigateToExportWeighingRequested?.Invoke(selected.CutOrderId);
                    return;
                }
            }

            var uc = scope.ServiceProvider.GetRequiredService<TransitionToExportScaleUseCase>();
            await uc.ExecuteAsync(new TransitionToExportScaleRequest(selected.CutOrderId), CancellationToken.None);

            _toastService.ShowSuccess("\u0110\u00e3 chuy\u1ec3n c\u1eaft l\u1ec7nh sang lu\u1ed3ng c\u00e2n xu\u1ea5t kh\u1ea9u.");
            await LoadVehiclesAsync();
            NavigateToExportWeighingRequested?.Invoke(selected.CutOrderId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Transition cut order to export scale failed");
            _toastService.ShowError(ex.Message);
        }
    }

    private IncomingVehicleSelectionItem? ResolveSingleExportScaleCandidate()
    {
        if (IsCreateMode)
        {
            return null;
        }

        var selectedVehicles = Vehicles.Where(x => x.IsSelected).Take(2).ToList();
        if (selectedVehicles.Count > 1)
        {
            return null;
        }

        return selectedVehicles.Count == 1
            ? selectedVehicles[0]
            : SelectedVehicle;
    }

    private IncomingVehicleSelectionItem? ResolvePrimaryVehicleSelection(IReadOnlyCollection<IncomingVehicleSelectionItem> selectedVehicles)
    {
        if (selectedVehicles.Count == 0)
        {
            return null;
        }

        if (SelectedVehicle != null && selectedVehicles.Any(x => x.CutOrderId == SelectedVehicle.CutOrderId))
        {
            return SelectedVehicle;
        }

        return selectedVehicles
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpCutOrderId)
            .FirstOrDefault();
    }

    public async Task InitializeAsync()
    {
        using (Helpers.PerformanceLogger.Track("IncomingVehicles.Initialize"))
        {
            await LoadVehiclesAsync();
        }
    }

    private async Task LoadSelectedVehicleDetailsAsync(IncomingVehicleSelectionItem selected)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var regRepo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            var registration = await regRepo.GetByIdAsync(selected.CutOrderId, CancellationToken.None);
            if (registration == null)
            {
                return;
            }

            IsCreateMode = false;
            EditingCutOrderId = registration.Id;
            FormErpCutOrderId = registration.ErpCutOrderId;
            FormConsumptionPlace = registration.ConsumptionPlace;
            FormMarket = registration.Market;
            SetFormVehiclePlate(registration.VehiclePlate);
            SetFormMoocNumber(registration.MoocNumber);
            SetFormDriverName(registration.ReceiverName);
            SetFormCustomerCode(registration.CustomerCode);
            SetFormCustomerName(registration.CustomerName);
            SetFormProductCode(registration.ProductCode);
            SetFormProductName(registration.ProductName);
            FormPlannedWeight = registration.PlannedWeight;
            var isBagged = string.Equals(StationApp.Domain.Constants.ProductTypes.Normalize(selected.ProductType), StationApp.Domain.Constants.ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
            IsFormProductBagged = isBagged;
            FormBagCount = isBagged ? registration.BagCount : null;
            FormNotes = registration.Notes;
            FormIsCancelled = registration.IsCancelled;
            FormTransportMethod = registration.TransportMethod;
            FormTransactionType = registration.TransactionType;
            FormAttachSessionNo = selected.SuggestedSessionNo;

            await LoadVehicleMasterInfoAsync(vehicleRepo, registration.VehiclePlate, registration.MoocNumber);
            OnPropertyChanged(nameof(IsDetailSelectionMode));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Load selected incoming vehicle detail failed");
            _toastService.ShowError(UiText.Incoming.DetailLoadError);
        }
    }

    private async Task LoadVehicleMasterInfoAsync(IVehicleRepository vehicleRepo, string? vehiclePlate, string? moocNumber)
    {
        TtcpWeight = null;
        VehicleRegistrationNo = null;
        VehicleRegistrationExpiry = null;
        MoocRegistrationNo = null;
        MoocRegistrationExpiry = null;
        OnPropertyChanged(nameof(DisplayTtcp10PercentKg));

        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            return;
        }

        Vehicle? vehicle = null;
        if (!string.IsNullOrWhiteSpace(moocNumber))
        {
            vehicle = await vehicleRepo.GetByPlateAndMoocAsync(vehiclePlate, moocNumber, CancellationToken.None);
        }

        if (vehicle == null)
        {
            vehicle = (await vehicleRepo.GetByPlateAsync(vehiclePlate, CancellationToken.None)).FirstOrDefault();
        }

        if (vehicle == null)
        {
            return;
        }

        TtcpWeight = vehicle.TtcpWeight;
        VehicleRegistrationNo = vehicle.VehicleRegistrationNo;
        VehicleRegistrationExpiry = vehicle.VehicleRegistrationExpiryDate;
        MoocRegistrationNo = vehicle.MoocRegistrationNo;
        MoocRegistrationExpiry = vehicle.MoocRegistrationExpiryDate;
        OnPropertyChanged(nameof(DisplayTtcp10PercentKg));
    }

    private void BeginCreateMode()
    {
        IsCreateMode = true;
        SelectedVehicle = null;
        foreach (var vehicle in Vehicles)
        {
            vehicle.IsSelected = false;
        }

        ClearForm();
        FormTransactionType = TransactionType.INBOUND;
        FormTransportMethod = TransportMethod.ROAD;
    }

    private void ClearForm()
    {
        EditingCutOrderId = null;
        FormErpCutOrderId = null;
        FormConsumptionPlace = null;
        FormMarket = null;
        SetFormVehiclePlate(null);
        SetFormMoocNumber(null);
        SetFormDriverName(null);
        SetFormCustomerCode(null);
        SetFormCustomerName(null);
        SetFormProductCode(null);
        SetFormProductName(null);
        FormPlannedWeight = null;
        FormBagCount = null;
        IsFormProductBagged = true;
        FormNotes = null;
        FormIsCancelled = false;
        FormAttachSessionNo = null;
        TtcpWeight = null;
        VehicleRegistrationNo = null;
        VehicleRegistrationExpiry = null;
        MoocRegistrationNo = null;
        MoocRegistrationExpiry = null;
        OnPropertyChanged(nameof(DisplayTtcp10PercentKg));
    }

    private void RefreshCreateSessionState()
    {
        OnPropertyChanged(nameof(CanConfirmEnterWeighing));
        OnPropertyChanged(nameof(CanMarkNoLoad));
        OnPropertyChanged(nameof(CanTransitionToExportScale));
        ConfirmEnterWeighingCommand.NotifyCanExecuteChanged();
        TransitionToExportScaleCommand.NotifyCanExecuteChanged();
        _markNoLoadCommand?.NotifyCanExecuteChanged();
    }

    private void RefreshRegistrationExpiryState()
    {
        OnPropertyChanged(nameof(IsVehicleRegistrationExpired));
        OnPropertyChanged(nameof(IsMoocRegistrationExpired));
    }

    private bool HasSearchFilters()
    {
        return !string.IsNullOrWhiteSpace(SearchErpCutOrderId)
            || !string.IsNullOrWhiteSpace(SearchVehiclePlate);
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
        SetFormVehiclePlate(item.Value);
        if (!string.IsNullOrWhiteSpace(item.Payload?.MoocNumber))
        {
            SetFormMoocNumber(item.Payload.MoocNumber);
        }

        if (!string.IsNullOrWhiteSpace(item.Payload?.DriverName))
        {
            SetFormDriverName(item.Payload.DriverName);
        }

        ApplyVehiclePayload(item.Payload);
    }

    private void ApplyMoocSelection(AutocompleteItem item)
    {
        SetFormMoocNumber(item.Value);
        ApplyVehiclePayload(item.Payload);
    }

    private void ApplyCustomerSelection(AutocompleteItem item)
    {
        SetFormCustomerCode(item.Payload?.CustomerCode ?? item.Value);
        SetFormCustomerName(item.Payload?.CustomerName ?? item.Value);
    }

    private void ApplyProductSelection(AutocompleteItem item)
    {
        SetFormProductCode(item.Payload?.ProductCode ?? item.Value);
        SetFormProductName(item.Payload?.ProductName ?? item.Value);
    }

    private void ApplyVehiclePayload(AutocompletePayload? payload)
    {
        if (payload == null)
        {
            _ = LoadVehicleMasterInfoAsyncFromFormAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(payload.VehiclePlate))
        {
            SetFormVehiclePlate(payload.VehiclePlate);
        }

        if (!string.IsNullOrWhiteSpace(payload.MoocNumber))
        {
            SetFormMoocNumber(payload.MoocNumber);
        }

        if (!string.IsNullOrWhiteSpace(payload.DriverName))
        {
            SetFormDriverName(payload.DriverName);
        }

        TtcpWeight = payload.TtcpWeight;
        VehicleRegistrationNo = payload.VehicleRegistrationNo;
        VehicleRegistrationExpiry = payload.VehicleRegistrationExpiryDate;
        MoocRegistrationNo = payload.MoocRegistrationNo;
        MoocRegistrationExpiry = payload.MoocRegistrationExpiryDate;
        OnPropertyChanged(nameof(DisplayTtcp10PercentKg));

        if (payload.TtcpWeight == null && !string.IsNullOrWhiteSpace(FormVehiclePlate))
        {
            _ = LoadVehicleMasterInfoAsyncFromFormAsync();
        }
    }

    private async Task LoadVehicleMasterInfoAsyncFromFormAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        await LoadVehicleMasterInfoAsync(vehicleRepo, FormVehiclePlate, FormMoocNumber);
    }

    private async Task SyncCustomerByCodeAsync(string? value)
    {
        _customerCodeLookupCts?.Cancel();
        _customerCodeLookupCts?.Dispose();

        var normalizedCode = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalizedCode == null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _customerCodeLookupCts = cts;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            var customer = await customerRepo.GetByCodeAsync(normalizedCode, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            if (customer != null)
            {
                if (!string.Equals(FormCustomerCode, normalizedCode, StringComparison.Ordinal))
                {
                    SetFormCustomerCode(normalizedCode);
                }

                SetFormCustomerName(customer.CustomerName);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Customer code lookup failed for {CustomerCode}", normalizedCode);
        }
        finally
        {
            if (ReferenceEquals(_customerCodeLookupCts, cts))
            {
                _customerCodeLookupCts = null;
            }

            cts.Dispose();
        }
    }

    private void SetFormVehiclePlate(string? value)
    {
        FormVehiclePlate = value;
        FormVehiclePlateInput.SetText(value);
    }

    private void SetFormMoocNumber(string? value)
    {
        FormMoocNumber = value;
        FormMoocInput.SetText(value);
    }

    private void SetFormDriverName(string? value)
    {
        FormDriverName = value;
        FormDriverInput.SetText(value);
    }

    private void SetFormCustomerName(string? value)
    {
        FormCustomerName = value;
        FormCustomerInput.SetText(value);
    }

    private void SetFormCustomerCode(string? value)
    {
        FormCustomerCode = value;
        FormCustomerCodeInput.SetText(value);
    }

    private void SetFormProductCode(string? value)
    {
        FormProductCode = value;
        FormProductCodeInput.SetText(value);
    }

    private void SetFormProductName(string? value)
    {
        FormProductName = value;
        FormProductNameInput.SetText(value);
    }

    private async Task<string?> ValidateRegistrationExpiryForCreateSessionAsync(
        IVehicleRepository vehicleRepo,
        IReadOnlyCollection<IncomingVehicleSelectionItem> selectedVehicles,
        CancellationToken ct)
    {
        foreach (var vehicleSelection in selectedVehicles)
        {
            if (ShouldUseCurrentFormRegistrationDataForCreateSession(vehicleSelection.CutOrderId))
            {
                continue;
            }

            var vehicle = await ResolveVehicleForExpiryValidationAsync(
                vehicleRepo,
                vehicleSelection.VehiclePlate,
                vehicleSelection.MoocNumber,
                ct);

            var issues = new Collection<string>();
            if (IsExpiredDate(vehicle?.VehicleRegistrationExpiryDate))
            {
                issues.Add($"hạn đăng kiểm xe ({vehicle!.VehicleRegistrationExpiryDate:dd/MM/yyyy})");
            }

            if (IsExpiredDate(vehicle?.MoocRegistrationExpiryDate))
            {
                issues.Add($"hạn đăng kiểm mooc ({vehicle!.MoocRegistrationExpiryDate:dd/MM/yyyy})");
            }

            if (issues.Count > 0)
            {
                return $"Không thể tạo lượt cân cho cắt lệnh {vehicleSelection.ErpCutOrderId ?? vehicleSelection.VehiclePlate} vì {string.Join(" và ", issues)} đã hết hạn.";
            }
        }

        return null;
    }

    private string? ValidateCurrentFormRegistrationExpiryForCreateSession(IReadOnlyCollection<Guid> selectedIds)
    {
        if (!ShouldUseCurrentFormRegistrationDataForCreateSession(selectedIds))
        {
            return null;
        }

        var issues = new Collection<string>();
        if (IsExpiredDate(VehicleRegistrationExpiry))
        {
            issues.Add($"hạn đăng kiểm xe ({VehicleRegistrationExpiry:dd/MM/yyyy})");
        }

        if (IsExpiredDate(MoocRegistrationExpiry))
        {
            issues.Add($"hạn đăng kiểm mooc ({MoocRegistrationExpiry:dd/MM/yyyy})");
        }

        if (issues.Count == 0)
        {
            return null;
        }

        return $"Không thể tạo lượt cân cho cắt lệnh {FormErpCutOrderId ?? FormVehiclePlate} vì {string.Join(" và ", issues)} đã hết hạn.";
    }

    private async Task PersistCurrentFormVehicleMasterDataForCreateSessionAsync(
        IServiceProvider serviceProvider,
        IReadOnlyCollection<Guid> selectedIds,
        CancellationToken ct)
    {
        if (!ShouldUseCurrentFormRegistrationDataForCreateSession(selectedIds)
            || string.IsNullOrWhiteSpace(FormVehiclePlate))
        {
            return;
        }

        var ensureInboundMasterDataUseCase = serviceProvider.GetRequiredService<EnsureInboundMasterDataUseCase>();
        await ensureInboundMasterDataUseCase.ExecuteAsync(
            FormVehiclePlate!,
            FormMoocNumber,
            FormDriverName,
            FormTransportMethod,
            FormCustomerCode,
            FormCustomerName,
            FormProductCode,
            FormProductName,
            null,
            FormTransactionType,
            ct,
            TtcpWeight,
            VehicleRegistrationNo,
            VehicleRegistrationExpiry,
            MoocRegistrationNo,
            MoocRegistrationExpiry);
    }


    private bool ValidateIncomingDetailForm()
    {
        if (string.IsNullOrWhiteSpace(FormVehiclePlate))
        {
            _toastService.ShowWarning(UiText.Common.RequiredVehiclePlate);
            return false;
        }

        var requiresOutboundPlanningFields = FormTransactionType == TransactionType.OUTBOUND;

        if (requiresOutboundPlanningFields && string.IsNullOrWhiteSpace(FormDriverName))
        {
            _toastService.ShowWarning(UiText.Common.RequiredDriverName);
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormCustomerName))
        {
            _toastService.ShowWarning(UiText.Common.RequiredCustomer);
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormProductCode))
        {
            _toastService.ShowWarning(UiText.Common.RequiredProductCode);
            return false;
        }

        if (string.IsNullOrWhiteSpace(FormProductName))
        {
            _toastService.ShowWarning(UiText.Common.RequiredProductName);
            return false;
        }

        if (requiresOutboundPlanningFields && (!FormPlannedWeight.HasValue || FormPlannedWeight.Value <= 0))
        {
            _toastService.ShowWarning(UiText.Common.RequiredPlannedWeight);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(FormNotes) && FormNotes.Trim().Length > 500)
        {
            _toastService.ShowWarning("Ghi chú không được vượt quá 500 ký tự.");
            return false;
        }

        return true;
    }

    private Task<OperationResult<CutOrder>> CreateInboundRegistrationFromFormAsync(
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        var notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim();
        var uc = serviceProvider.GetRequiredService<CreateInboundRegistrationUseCase>();
        return uc.ExecuteAsync(new CreateInboundRegistrationRequest(
            VehiclePlate: FormVehiclePlate!,
            TransactionType: FormTransactionType,
            TransportMethod: FormTransportMethod,
            MoocNumber: FormMoocNumber,
            ReceiverName: FormDriverName,
            CustomerCode: FormCustomerCode,
            CustomerName: FormCustomerName,
            ProductCode: FormProductCode,
            ProductName: FormProductName,
            ProductType: null,
            PlannedWeight: FormPlannedWeight,
            BagCount: FormBagCount,
            Notes: notes,
            TtcpWeight: TtcpWeight,
            VehicleRegistrationNo: VehicleRegistrationNo,
            VehicleRegistrationExpiryDate: VehicleRegistrationExpiry,
            MoocRegistrationNo: MoocRegistrationNo,
            MoocRegistrationExpiryDate: MoocRegistrationExpiry
        ), ct);
    }

    private string? ValidateCurrentFormRegistrationExpiryForCreateSession()
    {
        var issues = new Collection<string>();
        if (IsExpiredDate(VehicleRegistrationExpiry))
        {
            issues.Add($"h\u1ea1n \u0111\u0103ng ki\u1ec3m xe ({VehicleRegistrationExpiry:dd/MM/yyyy})");
        }

        if (IsExpiredDate(MoocRegistrationExpiry))
        {
            issues.Add($"h\u1ea1n \u0111\u0103ng ki\u1ec3m mooc ({MoocRegistrationExpiry:dd/MM/yyyy})");
        }

        if (issues.Count == 0)
        {
            return null;
        }

        return $"Kh\u00f4ng th\u1ec3 t\u1ea1o l\u01b0\u1ee3t c\u00e2n cho c\u1eaft l\u1ec7nh {FormErpCutOrderId ?? FormVehiclePlate} v\u00ec {string.Join(" v\u00e0 ", issues)} \u0111\u00e3 h\u1ebft h\u1ea1n.";
    }

    private IncomingVehicleSelectionItem BuildSelectionItem(CutOrder registration)
    {
        return new IncomingVehicleSelectionItem(
            new IncomingVehicleListItem(
                registration.Id,
                registration.ErpCutOrderId,
                registration.ErpRegistrationCode,
                registration.TransactionType,
                registration.VehiclePlate,
                registration.MoocNumber,
                registration.ReceiverName,
                registration.CustomerName,
                registration.ProductCode,
                registration.ProductName,
                registration.PlannedWeight,
                registration.BagCount,
                registration.CutOrderStatus,
                registration.TransportMethod,
                registration.CreatedAt,
                registration.ProductType,
                registration.CarryForwardWeight1,
                registration.CarryForwardWeight1Time,
                FormAttachSessionNo,
                registration.ConsumptionPlace,
                registration.Market));
    }
    private bool ShouldUseCurrentFormRegistrationDataForCreateSession(IReadOnlyCollection<Guid> selectedIds)
    {
        return !IsCreateMode
            && SelectedVehicle != null
            && selectedIds.Contains(SelectedVehicle.CutOrderId);
    }

    private bool ShouldUseCurrentFormRegistrationDataForCreateSession(Guid cutOrderId)
    {
        return !IsCreateMode
            && SelectedVehicle != null
            && SelectedVehicle.CutOrderId == cutOrderId;
    }

    private async Task<Vehicle?> ResolveVehicleForExpiryValidationAsync(
        IVehicleRepository vehicleRepo,
        string? vehiclePlate,
        string? moocNumber,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            return null;
        }

        Vehicle? vehicle = null;
        if (!string.IsNullOrWhiteSpace(moocNumber))
        {
            vehicle = await vehicleRepo.GetByPlateAndMoocAsync(vehiclePlate, moocNumber, ct);
        }

        return vehicle ?? (await vehicleRepo.GetByPlateAsync(vehiclePlate, ct)).FirstOrDefault();
    }

    private static bool IsExpiredDate(DateTime? value)
    {
        return value.HasValue && value.Value.Date < DateTime.Today;
    }

    partial void OnFormProductCodeChanged(string? value)
    {
        _ = SyncProductTypeAsync(value);
    }

    private async Task SyncProductTypeAsync(string? productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            IsFormProductBagged = true;
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var product = await productRepo.GetByCodeAsync(productCode.Trim(), CancellationToken.None);
            if (product != null)
            {
                var isBagged = string.Equals(StationApp.Domain.Constants.ProductTypes.Normalize(product.ProductType), StationApp.Domain.Constants.ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
                IsFormProductBagged = isBagged;
                if (!isBagged)
                {
                    FormBagCount = null;
                }
            }
            else
            {
                IsFormProductBagged = true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Product type sync failed for {ProductCode}", productCode);
            IsFormProductBagged = true;
        }
    }
}

public partial class IncomingVehicleSelectionItem : ObservableObject
{
    public IncomingVehicleSelectionItem(IncomingVehicleListItem item)
    {
        CutOrderId = item.CutOrderId;
        ErpCutOrderId = item.ErpCutOrderId;
        ErpRegistrationCode = item.ErpRegistrationCode;
        TransactionType = item.TransactionType;
        VehiclePlate = item.VehiclePlate;
        MoocNumber = item.MoocNumber;
        ReceiverName = item.ReceiverName;
        CustomerName = item.CustomerName;
        ProductCode = item.ProductCode;
        ProductName = item.ProductName;
        ProductType = item.ProductType;
        PlannedWeight = item.PlannedWeight;

        var isBagged = string.Equals(StationApp.Domain.Constants.ProductTypes.Normalize(ProductType), StationApp.Domain.Constants.ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
        BagCount = isBagged ? item.BagCount : null;

        CutOrderStatus = item.CutOrderStatus;
        TransportMethod = item.TransportMethod;
        CreatedAt = item.CreatedAt;
        CarryForwardWeight1 = item.CarryForwardWeight1;
        CarryForwardWeight1Time = item.CarryForwardWeight1Time;
        SuggestedSessionNo = item.SuggestedSessionNo;
        ConsumptionPlace = item.ConsumptionPlace;
        Market = item.Market;
        IsExportScale = item.IsExportScale;
        ExportAccumulatedWeight = item.ExportAccumulatedWeight;
        ExportRemainingWeight = item.ExportRemainingWeight;
        ExportTripCount = item.ExportTripCount;
    }

    [ObservableProperty] private bool _isSelected;

    public Guid CutOrderId { get; }
    public string? ErpCutOrderId { get; }
    public string? ErpRegistrationCode { get; }
    public TransactionType TransactionType { get; }
    public string VehiclePlate { get; }
    public string? MoocNumber { get; }
    public string? ReceiverName { get; }
    public string? CustomerName { get; }
    public string? ProductCode { get; }
    public string? ProductName { get; }
    public string? ProductType { get; }
    public decimal? PlannedWeight { get; }
    public int? BagCount { get; }
    public CutOrderStatus CutOrderStatus { get; }
    public TransportMethod? TransportMethod { get; }
    public DateTime CreatedAt { get; }
    public decimal? CarryForwardWeight1 { get; }
    public DateTime? CarryForwardWeight1Time { get; }
    public string? SuggestedSessionNo { get; }
    public string? ConsumptionPlace { get; }
    public string? Market { get; }
    public bool IsExportScale { get; }
    public decimal? ExportAccumulatedWeight { get; }
    public decimal? ExportRemainingWeight { get; }
    public int ExportTripCount { get; }
}


