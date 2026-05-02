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

    [ObservableProperty] private ObservableCollection<IncomingVehicleSelectionItem> _vehicles = new();
    [ObservableProperty] private IncomingVehicleSelectionItem? _selectedVehicle;
    [ObservableProperty] private string? _searchErpVehicleRegistrationId;
    [ObservableProperty] private string? _searchVehiclePlate;
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _isCreateMode;
    [ObservableProperty] private Guid? _editingRegistrationId;
    [ObservableProperty] private string? _formErpVehicleRegistrationId;
    [ObservableProperty] private string? _formVehiclePlate;
    [ObservableProperty] private string? _formMoocNumber;
    [ObservableProperty] private string? _formDriverName;
    [ObservableProperty] private string? _formCustomerCode;
    [ObservableProperty] private string? _formCustomerName;
    [ObservableProperty] private string? _formProductCode;
    [ObservableProperty] private string? _formProductName;
    [ObservableProperty] private decimal? _formPlannedWeight;
    [ObservableProperty] private int? _formBagCount;
    [ObservableProperty] private string? _formNotes;
    [ObservableProperty] private bool _formIsCancelled;
    [ObservableProperty] private TransportMethod? _formTransportMethod = TransportMethod.ROAD;
    [ObservableProperty] private TransactionType _formTransactionType = TransactionType.INBOUND;

    [ObservableProperty] private decimal? _ttcpWeight;
    [ObservableProperty] private string? _vehicleRegistrationNo;
    [ObservableProperty] private DateTime? _vehicleRegistrationExpiry;
    [ObservableProperty] private string? _moocRegistrationNo;
    [ObservableProperty] private DateTime? _moocRegistrationExpiry;

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
    public decimal DisplayTtcp10PercentKg => ((TtcpWeight ?? 0m) * 1.10m);

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
        RefreshCreateSessionState();
    }

    partial void OnFormCustomerCodeChanged(string? value)
    {
        _ = SyncCustomerByCodeAsync(value);
    }

    [RelayCommand]
    private async Task LoadVehiclesAsync()
    {
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IVehicleRegistrationRepository>();
            var list = await repo.GetIncomingListAsync(
                new IncomingVehicleListFilter(
                    SearchErpVehicleRegistrationId,
                    SearchVehiclePlate,
                    null,
                    null,
                    null,
                    null,
                    null),
                CancellationToken.None);

            var selectedIds = Vehicles.Where(x => x.IsSelected).Select(x => x.RegistrationId).ToHashSet();
            Vehicles = new ObservableCollection<IncomingVehicleSelectionItem>(
                list.Select(x =>
                {
                    var item = new IncomingVehicleSelectionItem(x);
                    item.IsSelected = selectedIds.Contains(x.RegistrationId);
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
                SelectedVehicle = Vehicles.FirstOrDefault(x => x.RegistrationId == SelectedVehicle.RegistrationId);
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
                    SelectedVehicle = Vehicles.FirstOrDefault(x => x.RegistrationId == result.Data.Id);
                }

                IsCreateMode = false;
                return;
            }

            if (!EditingRegistrationId.HasValue)
            {
                _toastService.ShowWarning(UiText.Incoming.UpdateSelectionRequired);
                return;
            }

            var updateUseCase = scope.ServiceProvider.GetRequiredService<UpdateIncomingRegistrationUseCase>();
            var updateResult = await updateUseCase.ExecuteAsync(new UpdateIncomingRegistrationRequest(
                EditingRegistrationId.Value,
                FormVehiclePlate!,
                FormTransactionType,
                FormTransportMethod,
                FormMoocNumber,
                FormDriverName,
                FormCustomerCode,
                FormCustomerName,
                FormProductCode,
                FormProductName,
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

        var selectedIds = selectedVehicles.Select(x => x.RegistrationId).ToList();
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
                    .ThenBy(x => x.ErpVehicleRegistrationId)
                    .Select(x => new VehicleRepresentativeOption(
                        x.RegistrationId,
                        x.VehiclePlate,
                        x.MoocNumber,
                        x.ReceiverName,
                        x.ErpVehicleRegistrationId,
                        x.CreatedAt))
                    .ToList(),
                primaryVehicle.RegistrationId);

            var selectionResult = await _dialogService.ShowCustomDialogAsync<VehicleRepresentativeSelectionDialogViewModel, VehicleRepresentativeSelectionResult>(selectionVm);
            if (selectionResult == null)
            {
                return;
            }

            primaryVehicle = selectedVehicles.FirstOrDefault(x => x.RegistrationId == selectionResult.RegistrationId);
            if (primaryVehicle == null)
            {
                return;
            }
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<CreateWeighingSessionUseCase>();
            var result = await uc.ExecuteAsync(
                new CreateWeighingSessionRequest(selectedIds, primaryVehicle.RegistrationId),
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

    private IncomingVehicleSelectionItem? ResolvePrimaryVehicleSelection(IReadOnlyCollection<IncomingVehicleSelectionItem> selectedVehicles)
    {
        if (selectedVehicles.Count == 0)
        {
            return null;
        }

        if (SelectedVehicle != null && selectedVehicles.Any(x => x.RegistrationId == SelectedVehicle.RegistrationId))
        {
            return SelectedVehicle;
        }

        return selectedVehicles
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ErpVehicleRegistrationId)
            .FirstOrDefault();
    }

    public async Task InitializeAsync()
    {
        await LoadVehiclesAsync();
    }

    private async Task LoadSelectedVehicleDetailsAsync(IncomingVehicleSelectionItem selected)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var regRepo = scope.ServiceProvider.GetRequiredService<IVehicleRegistrationRepository>();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            var registration = await regRepo.GetByIdAsync(selected.RegistrationId, CancellationToken.None);
            if (registration == null)
            {
                return;
            }

            IsCreateMode = false;
            EditingRegistrationId = registration.Id;
            FormErpVehicleRegistrationId = registration.ErpVehicleRegistrationId;
            SetFormVehiclePlate(registration.VehiclePlate);
            SetFormMoocNumber(registration.MoocNumber);
            SetFormDriverName(registration.ReceiverName);
            FormCustomerCode = registration.CustomerCode;
            SetFormCustomerName(registration.CustomerName);
            SetFormProductCode(registration.ProductCode);
            SetFormProductName(registration.ProductName);
            FormPlannedWeight = registration.PlannedWeight;
            FormBagCount = registration.BagCount;
            FormNotes = registration.Notes;
            FormIsCancelled = registration.IsCancelled;
            FormTransportMethod = registration.TransportMethod;
            FormTransactionType = registration.TransactionType;

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
        EditingRegistrationId = null;
        FormErpVehicleRegistrationId = null;
        SetFormVehiclePlate(null);
        SetFormMoocNumber(null);
        SetFormDriverName(null);
        FormCustomerCode = null;
        SetFormCustomerName(null);
        SetFormProductCode(null);
        SetFormProductName(null);
        FormPlannedWeight = null;
        FormBagCount = null;
        FormNotes = null;
        FormIsCancelled = false;
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
        ConfirmEnterWeighingCommand.NotifyCanExecuteChanged();
    }

    private bool HasSearchFilters()
    {
        return !string.IsNullOrWhiteSpace(SearchErpVehicleRegistrationId)
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
}

public partial class IncomingVehicleSelectionItem : ObservableObject
{
    public IncomingVehicleSelectionItem(IncomingVehicleListItem item)
    {
        RegistrationId = item.RegistrationId;
        ErpVehicleRegistrationId = item.ErpVehicleRegistrationId;
        TransactionType = item.TransactionType;
        VehiclePlate = item.VehiclePlate;
        MoocNumber = item.MoocNumber;
        ReceiverName = item.ReceiverName;
        CustomerName = item.CustomerName;
        ProductCode = item.ProductCode;
        ProductName = item.ProductName;
        PlannedWeight = item.PlannedWeight;
        BagCount = item.BagCount;
        RegistrationStatus = item.RegistrationStatus;
        TransportMethod = item.TransportMethod;
        CreatedAt = item.CreatedAt;
    }

    [ObservableProperty] private bool _isSelected;

    public Guid RegistrationId { get; }
    public string? ErpVehicleRegistrationId { get; }
    public TransactionType TransactionType { get; }
    public string VehiclePlate { get; }
    public string? MoocNumber { get; }
    public string? ReceiverName { get; }
    public string? CustomerName { get; }
    public string? ProductCode { get; }
    public string? ProductName { get; }
    public decimal? PlannedWeight { get; }
    public int? BagCount { get; }
    public RegistrationStatus RegistrationStatus { get; }
    public TransportMethod? TransportMethod { get; }
    public DateTime CreatedAt { get; }
}
