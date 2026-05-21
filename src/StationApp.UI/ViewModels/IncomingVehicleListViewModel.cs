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
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Constants;
using StationApp.UI.Resources;
using StationApp.UI.Services;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels;

public partial class IncomingVehicleListViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<IncomingVehicleListViewModel>? _logger;
    private CancellationTokenSource? _customerCodeLookupCts;

    public event Action<Guid>? NavigateToWeighingRequested;
    public event Action? NavigateToOutgoingRequested;

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

    public AutocompleteInputViewModel SearchVehiclePlateInput { get; }
    public AutocompleteInputViewModel FormVehiclePlateInput { get; }
    public AutocompleteInputViewModel FormMoocInput { get; }
    public AutocompleteInputViewModel FormDriverInput { get; }
    public AutocompleteInputViewModel FormCustomerInput { get; }
    public AutocompleteInputViewModel FormProductCodeInput { get; }
    public AutocompleteInputViewModel FormProductNameInput { get; }

    public string SaveButtonText => IsCreateMode ? "TẠO XE NHẬP" : "LƯU THAY ĐỔI";
    public bool IsDetailSelectionMode => !IsCreateMode && SelectedVehicle != null;
    public bool CanConfirmEnterWeighing => Vehicles.Any(x => x.IsSelected) || (!IsCreateMode && SelectedVehicle != null);
    public bool CanMarkNoLoad => Vehicles.Any(x => x.IsSelected) || (!IsCreateMode && SelectedVehicle != null);
    public decimal DisplayTtcp10PercentKg => ((TtcpWeight ?? 0m) * 1.10m);
    public bool IsOutboundDetailLockMode => !IsCreateMode && FormTransactionType == TransactionType.OUTBOUND;
    public bool CanEditNonRegistrationFields => !IsOutboundDetailLockMode;

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
        FormCustomerInput = CreateAutocompleteField(AutocompleteFieldType.Customer, 2, ApplyCustomerSelection);
        FormProductCodeInput = CreateAutocompleteField(AutocompleteFieldType.ProductCode, 1, ApplyProductSelection);
        FormProductNameInput = CreateAutocompleteField(AutocompleteFieldType.ProductName, 2, ApplyProductSelection);

        WireTextState(SearchVehiclePlateInput, value => SearchVehiclePlate = value);
        WireTextState(FormVehiclePlateInput, value => FormVehiclePlate = value);
        WireTextState(FormMoocInput, value => FormMoocNumber = value);
        WireTextState(FormDriverInput, value => FormDriverName = value);
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
    private void StartCreateInbound()
    {
        BeginCreateMode();
    }

    [RelayCommand]
    private async Task SaveDetailAsync()
    {
        if (string.IsNullOrWhiteSpace(FormVehiclePlate))
        {
            _toastService.ShowWarning(UiText.Common.RequiredVehiclePlate);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();

            if (IsCreateMode)
            {
                var uc = scope.ServiceProvider.GetRequiredService<CreateInboundRegistrationUseCase>();
                var result = await uc.ExecuteAsync(new CreateInboundRegistrationRequest(
                    FormVehiclePlate!,
                    FormTransactionType,
                    FormTransportMethod,
                    FormMoocNumber,
                    FormDriverName,
                    FormCustomerCode,
                    FormCustomerName,
                    FormProductCode,
                    FormProductName,
                    null,
                    FormPlannedWeight,
                    FormBagCount,
                    FormNotes,
                    TtcpWeight,
                    VehicleRegistrationNo,
                    VehicleRegistrationExpiry,
                    MoocRegistrationNo,
                    MoocRegistrationExpiry
                ), CancellationToken.None);

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

            var updateUseCase = scope.ServiceProvider.GetRequiredService<UpdateIncomingRegistrationUseCase>();
            var updateResult = await updateUseCase.ExecuteAsync(new UpdateIncomingRegistrationRequest(
                EditingCutOrderId.Value,
                FormVehiclePlate!,
                FormTransactionType,
                FormTransportMethod,
                FormMoocNumber,
                FormDriverName,
                FormCustomerCode,
                FormCustomerName,
                FormProductCode,
                FormProductName,
                null,
                FormPlannedWeight,
                FormBagCount,
                FormNotes,
                FormIsCancelled,
                TtcpWeight,
                VehicleRegistrationNo,
                VehicleRegistrationExpiry,
                MoocRegistrationNo,
                MoocRegistrationExpiry
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
        var selectedVehicles = Vehicles.Where(x => x.IsSelected).ToList();
        if (selectedVehicles.Count == 0 && SelectedVehicle != null)
        {
            selectedVehicles.Add(SelectedVehicle);
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

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var applyCarryForwardWeight1 = true;
            var carryForwardCandidates = selectedVehicles
                .Where(x => x.CarryForwardWeight1.HasValue)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.ErpCutOrderId)
                .ToList();
            var carryForwardWeight1 = carryForwardCandidates
                .Select(x => x.CarryForwardWeight1)
                .Distinct()
                .SingleOrDefault();
            var suggestedSessionCandidates = selectedVehicles
                .Select(x => string.IsNullOrWhiteSpace(x.SuggestedSessionNo) ? null : x.SuggestedSessionNo.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var attachSessionNo = string.IsNullOrWhiteSpace(FormAttachSessionNo) ? null : FormAttachSessionNo.Trim();
            var isAutoSuggestedAttach =
                !string.IsNullOrWhiteSpace(attachSessionNo)
                && suggestedSessionCandidates.Count == 1
                && string.Equals(attachSessionNo, suggestedSessionCandidates[0], StringComparison.OrdinalIgnoreCase);

            if (carryForwardWeight1.HasValue)
            {
                var carryForwardReference = carryForwardCandidates.First();
                var carryForwardTimeText = carryForwardReference.CarryForwardWeight1Time.HasValue
                    ? carryForwardReference.CarryForwardWeight1Time.Value.ToString("dd/MM/yyyy HH:mm:ss")
                    : "không xác định";
                applyCarryForwardWeight1 = await _dialogService.ShowConfirmAsync(
                    "Xác nhận dùng lại cân lần 1",
                    $"Cắt lệnh {carryForwardReference.ErpCutOrderId ?? carryForwardReference.VehiclePlate} đã có số cân lần 1 là {carryForwardWeight1.Value:N0} kg vào lúc {carryForwardTimeText}. Bạn có đồng ý lấy số cân lần 1 này cho lượt cân mới không?",
                    "Đồng ý",
                    "Không");
            }

            if (!string.IsNullOrWhiteSpace(attachSessionNo) && (!isAutoSuggestedAttach || applyCarryForwardWeight1))
            {
                var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
                var session = await sessionRepo.GetBySessionNoAsync(attachSessionNo, CancellationToken.None);
                if (session == null)
                {
                    _toastService.ShowError($"Không tìm thấy lượt cân {attachSessionNo} trong hệ thống.");
                    return;
                }

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
                                ? (int?)decimal.Round(session.NetWeight.Value / 50m, 0, MidpointRounding.AwayFromZero)
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

                _toastService.ShowSuccess($"Đã gắn cắt lệnh vào lượt cân {session.SessionNo}.");
                await LoadVehiclesAsync();
                NavigateToWeighingRequested?.Invoke(session.Id);
                return;
            }

            var uc = scope.ServiceProvider.GetRequiredService<CreateWeighingSessionUseCase>();
            var result = await uc.ExecuteAsync(
                new CreateWeighingSessionRequest(selectedIds, primaryVehicle.CutOrderId, applyCarryForwardWeight1),
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

    [RelayCommand(CanExecute = nameof(CanMarkNoLoad))]
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
            SetFormVehiclePlate(registration.VehiclePlate);
            SetFormMoocNumber(registration.MoocNumber);
            SetFormDriverName(registration.ReceiverName);
            FormCustomerCode = registration.CustomerCode;
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
        SetFormVehiclePlate(null);
        SetFormMoocNumber(null);
        SetFormDriverName(null);
        FormCustomerCode = null;
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
        ConfirmEnterWeighingCommand.NotifyCanExecuteChanged();
        MarkNoLoadCommand.NotifyCanExecuteChanged();
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
        FormCustomerCode = item.Payload?.CustomerCode;
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
                    FormCustomerCode = normalizedCode;
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
}


