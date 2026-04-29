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
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels;

public partial class IncomingVehicleListViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IToastService _toastService;
    private readonly ILogger<IncomingVehicleListViewModel>? _logger;

    public event Action<Guid>? NavigateToWeighingRequested;

    [ObservableProperty] private ObservableCollection<IncomingVehicleListItem> _vehicles = new();
    [ObservableProperty] private IncomingVehicleListItem? _selectedVehicle;
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
    [ObservableProperty] private DateTime? _vehicleRegistrationExpiry;
    [ObservableProperty] private DateTime? _moocRegistrationExpiry;

    public string DetailPanelTitle => IsCreateMode ? "TẠO XE NHẬP HÀNG" : "THÔNG TIN XE ĐƯỢC CHỌN";
    public string SaveButtonText => IsCreateMode ? "TẠO XE NHẬP" : "LƯU THAY ĐỔI";
    public bool IsDetailSelectionMode => !IsCreateMode && SelectedVehicle != null;
    public bool CanConfirmEnterWeighing => SelectedVehicle != null && !IsCreateMode;
    public decimal DisplayTtcp10PercentKg => ((TtcpWeight ?? 0m) * 1.10m);

    public IncomingVehicleListViewModel(IServiceScopeFactory scopeFactory, IToastService toastService, ILogger<IncomingVehicleListViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _toastService = toastService;
        _logger = logger;
        BeginCreateMode();
    }

    partial void OnSelectedVehicleChanged(IncomingVehicleListItem? value)
    {
        if (value == null)
        {
            return;
        }

        _ = LoadSelectedVehicleDetailsAsync(value);
        OnPropertyChanged(nameof(CanConfirmEnterWeighing));
        ConfirmEnterWeighingCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCreateModeChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailPanelTitle));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(IsDetailSelectionMode));
        OnPropertyChanged(nameof(CanConfirmEnterWeighing));
        ConfirmEnterWeighingCommand.NotifyCanExecuteChanged();
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
                    null),
                CancellationToken.None);

            Vehicles = new ObservableCollection<IncomingVehicleListItem>(list);

            if (list.Count == 0 && HasSearchFilters())
            {
                _toastService.ShowInfo("Không tìm thấy dữ liệu phù hợp.");
            }

            if (SelectedVehicle != null)
            {
                SelectedVehicle = Vehicles.FirstOrDefault(x => x.RegistrationId == SelectedVehicle.RegistrationId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadIncomingVehicles failed");
            _toastService.ShowError("Không thể tải danh sách xe vào. Vui lòng thử lại.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
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
            _toastService.ShowWarning("Vui lòng nhập Số PTVC.");
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
                    FormNotes
                ), CancellationToken.None);

                if (!result.Success)
                {
                    _toastService.ShowError(result.ErrorMessage ?? "Không thể tạo xe nhập hàng.");
                    return;
                }

                _toastService.ShowSuccess("Đã tạo xe nhập hàng thành công.");
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
                _toastService.ShowWarning("Vui lòng chọn xe cần cập nhật.");
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
                FormIsCancelled
            ), CancellationToken.None);

            if (!updateResult.Success)
            {
                _toastService.ShowError(updateResult.ErrorMessage ?? "Không thể cập nhật thông tin xe vào.");
                return;
            }

            _toastService.ShowSuccess("Đã lưu thay đổi thông tin xe vào.");
            await LoadVehiclesAsync();

            if (SelectedVehicle != null)
            {
                await LoadSelectedVehicleDetailsAsync(SelectedVehicle);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Save incoming detail failed");
            _toastService.ShowError("Không thể lưu thông tin xe vào. Vui lòng thử lại.");
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
        if (SelectedVehicle == null)
        {
            _toastService.ShowWarning("Vui lòng chọn một xe để xác nhận vào cân.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uc = scope.ServiceProvider.GetRequiredService<ConfirmEnterWeighingUseCase>();
            await uc.ExecuteAsync(new ConfirmEnterWeighingRequest(SelectedVehicle.RegistrationId), CancellationToken.None);

            _toastService.ShowSuccess("Đã chuyển xe vào màn Lập phiếu cân.");

            var registrationId = SelectedVehicle.RegistrationId;
            await LoadVehiclesAsync();
            NavigateToWeighingRequested?.Invoke(registrationId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ConfirmEnterWeighing failed");
            _toastService.ShowError("Không thể chuyển xe vào cân. Vui lòng thử lại.");
        }
    }

    public async Task InitializeAsync()
    {
        await LoadVehiclesAsync();
    }

    private async Task LoadSelectedVehicleDetailsAsync(IncomingVehicleListItem selected)
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
            FormVehiclePlate = registration.VehiclePlate;
            FormMoocNumber = registration.MoocNumber;
            FormDriverName = registration.ReceiverName;
            FormCustomerCode = registration.CustomerCode;
            FormCustomerName = registration.CustomerName;
            FormProductCode = registration.ProductCode;
            FormProductName = registration.ProductName;
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
            _toastService.ShowError("Không thể tải thông tin chi tiết xe vào.");
        }
    }

    private async Task LoadVehicleMasterInfoAsync(IVehicleRepository vehicleRepo, string? vehiclePlate, string? moocNumber)
    {
        TtcpWeight = null;
        VehicleRegistrationExpiry = null;
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
        VehicleRegistrationExpiry = vehicle.VehicleRegistrationExpiryDate;
        MoocRegistrationExpiry = vehicle.MoocRegistrationExpiryDate;
        OnPropertyChanged(nameof(DisplayTtcp10PercentKg));
    }

    private void BeginCreateMode()
    {
        IsCreateMode = true;
        SelectedVehicle = null;
        ClearForm();
        FormTransactionType = TransactionType.INBOUND;
        FormTransportMethod = TransportMethod.ROAD;
    }

    private void ClearForm()
    {
        EditingRegistrationId = null;
        FormErpVehicleRegistrationId = null;
        FormVehiclePlate = null;
        FormMoocNumber = null;
        FormDriverName = null;
        FormCustomerCode = null;
        FormCustomerName = null;
        FormProductCode = null;
        FormProductName = null;
        FormPlannedWeight = null;
        FormBagCount = null;
        FormNotes = null;
        FormIsCancelled = false;
        TtcpWeight = null;
        VehicleRegistrationExpiry = null;
        MoocRegistrationExpiry = null;
        OnPropertyChanged(nameof(DisplayTtcp10PercentKg));
    }

    private bool HasSearchFilters()
    {
        return !string.IsNullOrWhiteSpace(SearchErpVehicleRegistrationId)
            || !string.IsNullOrWhiteSpace(SearchVehiclePlate);
    }
}
