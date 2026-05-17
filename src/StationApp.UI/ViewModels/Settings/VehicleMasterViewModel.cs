using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.UI.Services;
using StationApp.Domain.Enums;
using StationApp.Domain.Constants;

namespace StationApp.UI.ViewModels.Settings
{
    public partial class VehicleMasterViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public VehicleMasterViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        [ObservableProperty] private string _searchVehiclePlate = string.Empty;
        [ObservableProperty] private string _searchMoocNumber = string.Empty;
        [ObservableProperty] private string _searchDriverName = string.Empty;

        [ObservableProperty] private string _editVehiclePlate = string.Empty;
        [ObservableProperty] private string _editMoocNumber = string.Empty;
        [ObservableProperty] private string _editDriverName = string.Empty;
        [ObservableProperty] private TransportMethod? _editTransportMethod = TransportMethod.ROAD;
        [ObservableProperty] private decimal? _editTtcpWeight;
        [ObservableProperty] private string _editVehicleRegistrationNo = string.Empty;
        [ObservableProperty] private DateTime? _editVehicleRegistrationExpiryDate;
        [ObservableProperty] private string _editMoocRegistrationNo = string.Empty;
        [ObservableProperty] private DateTime? _editMoocRegistrationExpiryDate;
        [ObservableProperty] private bool _editIsActive = true;

        [ObservableProperty] private ObservableCollection<Vehicle> _vehicles = new();
        [ObservableProperty] private Vehicle? _selectedVehicle;

        partial void OnSelectedVehicleChanged(Vehicle? value)
        {
            if (value != null)
            {
                EditVehiclePlate = value.VehiclePlate;
                EditMoocNumber = value.MoocNumber;
                EditDriverName = value.DriverName ?? string.Empty;
                EditTransportMethod = Enum.TryParse<TransportMethod>(value.TransportMethod, out var tm) ? tm : null;
                EditTtcpWeight = value.TtcpWeight;
                EditVehicleRegistrationNo = value.VehicleRegistrationNo ?? string.Empty;
                EditVehicleRegistrationExpiryDate = value.VehicleRegistrationExpiryDate;
                EditMoocRegistrationNo = value.MoocRegistrationNo ?? string.Empty;
                EditMoocRegistrationExpiryDate = value.MoocRegistrationExpiryDate;
                EditIsActive = value.IsActive;
            }
        }

        public async Task LoadAsync()
        {
            await SearchAsync();
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            var list = await repo.SearchAsync(null, CancellationToken.None);
            var filtered = list.Where(x =>
                MatchesSearch(x.VehiclePlate, SearchVehiclePlate)
                && MatchesSearch(x.MoocNumber, SearchMoocNumber)
                && MatchesSearch(x.DriverName, SearchDriverName));
            Vehicles.Clear();
            foreach (var item in filtered)
            {
                Vehicles.Add(item);
            }
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

        [RelayCommand]
        private void ResetForm()
        {
            EditVehiclePlate = string.Empty;
            EditMoocNumber = string.Empty;
            EditDriverName = string.Empty;
            EditTransportMethod = TransportMethod.ROAD;
            EditTtcpWeight = null;
            EditVehicleRegistrationNo = string.Empty;
            EditVehicleRegistrationExpiryDate = null;
            EditMoocRegistrationNo = string.Empty;
            EditMoocRegistrationExpiryDate = null;
            EditIsActive = true;
            SelectedVehicle = null;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
            var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
            var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

            if (string.IsNullOrWhiteSpace(EditVehiclePlate))
            {
                await dialogService.ShowErrorAsync("Lỗi", "Biển số xe không được rỗng!");
                return;
            }


            try
            {
                var existing = await repo.GetByPlateAndMoocAsync(EditVehiclePlate, EditMoocNumber, CancellationToken.None);
                if (existing != null && (SelectedVehicle == null || existing.Id != SelectedVehicle.Id))
                {
                    await dialogService.ShowWarningAsync("Lỗi", "Cặp (Biển số, Số Mooc) đã tồn tại trên hệ thống!");
                    return;
                }

                if (SelectedVehicle == null)
                {
                    var newVehicle = new Vehicle
                    {
                        Id = Guid.NewGuid(),
                        VehiclePlate = EditVehiclePlate.Trim(),
                        MoocNumber = EditMoocNumber.Trim(),
                        DriverName = EditDriverName.Trim(),
                        TransportMethod = EditTransportMethod?.ToString(),
                        TtcpWeight = EditTtcpWeight,
                        VehicleRegistrationNo = EditVehicleRegistrationNo.Trim(),
                        VehicleRegistrationExpiryDate = EditVehicleRegistrationExpiryDate,
                        MoocRegistrationNo = EditMoocRegistrationNo.Trim(),
                        MoocRegistrationExpiryDate = EditMoocRegistrationExpiryDate,
                        IsActive = EditIsActive,
                        CreatedAt = clock.NowLocal,
                        CreatedBy = "Operator"
                    };
                    await repo.AddAsync(newVehicle, CancellationToken.None);
                    await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, newVehicle, clock.NowLocal);
                }
                else
                {
                    SelectedVehicle.VehiclePlate = EditVehiclePlate.Trim();
                    SelectedVehicle.MoocNumber = EditMoocNumber.Trim();
                    SelectedVehicle.DriverName = EditDriverName.Trim();
                    SelectedVehicle.TransportMethod = EditTransportMethod?.ToString();
                    SelectedVehicle.TtcpWeight = EditTtcpWeight;
                    SelectedVehicle.VehicleRegistrationNo = EditVehicleRegistrationNo.Trim();
                    SelectedVehicle.VehicleRegistrationExpiryDate = EditVehicleRegistrationExpiryDate;
                    SelectedVehicle.MoocRegistrationNo = EditMoocRegistrationNo.Trim();
                    SelectedVehicle.MoocRegistrationExpiryDate = EditMoocRegistrationExpiryDate;
                    SelectedVehicle.IsActive = EditIsActive;
                    SelectedVehicle.UpdatedAt = clock.NowLocal;
                    SelectedVehicle.UpdatedBy = "Operator";

                    if (existing != null)
                    {
                        existing.VehiclePlate = EditVehiclePlate.Trim();
                        existing.MoocNumber = EditMoocNumber.Trim();
                        existing.DriverName = EditDriverName.Trim();
                        existing.TransportMethod = EditTransportMethod?.ToString();
                        existing.TtcpWeight = EditTtcpWeight;
                        existing.VehicleRegistrationNo = EditVehicleRegistrationNo.Trim();
                        existing.VehicleRegistrationExpiryDate = EditVehicleRegistrationExpiryDate;
                        existing.MoocRegistrationNo = EditMoocRegistrationNo.Trim();
                        existing.MoocRegistrationExpiryDate = EditMoocRegistrationExpiryDate;
                        existing.IsActive = EditIsActive;
                        existing.UpdatedAt = clock.NowLocal;
                        existing.UpdatedBy = "Operator";
                        await repo.UpdateAsync(existing, CancellationToken.None);
                        await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, existing, clock.NowLocal);
                    }
                    else
                    {
                        await repo.UpdateAsync(SelectedVehicle, CancellationToken.None);
                        await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, SelectedVehicle, clock.NowLocal);
                    }
                }

                await uow.SaveChangesAsync(CancellationToken.None);
                await dialogService.ShowInfoAsync("Thông báo", "Lưu dữ liệu thành công!");
                
                ResetForm();
                await SearchAsync();
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi lưu dữ liệu: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeactivateAsync()
        {
            if (SelectedVehicle == null) return;

            using var scope = _scopeFactory.CreateScope();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

            var result = await dialogService.ShowConfirmAsync(
                "Xác nhận", 
                $"Bạn có chắc muốn ngừng sử dụng phương tiện {SelectedVehicle.VehiclePlate}?", 
                "Đồng ý", 
                "Bỏ qua"
            );

            if (result)
            {
                var repo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
                var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

                SelectedVehicle.IsActive = false;
                await repo.UpdateAsync(SelectedVehicle, CancellationToken.None);
                await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, SelectedVehicle, clock.NowLocal);
                await uow.SaveChangesAsync(CancellationToken.None);

                await dialogService.ShowInfoAsync("Thông báo", "Đã chuyển đổi trạng thái ngừng sử dụng.");
                ResetForm();
                await SearchAsync();
            }
        }

        private static async Task EnqueueMasterSyncAsync(
            ISyncOutboxRepository outboxRepo,
            ISyncPayloadFactory payloadFactory,
            Vehicle vehicle,
            DateTime now)
        {
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
    }
}
