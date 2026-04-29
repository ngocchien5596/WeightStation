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
        [ObservableProperty] private string _editTransportMethod = string.Empty;
        [ObservableProperty] private string _editTtcpWeight = string.Empty;
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
                EditTransportMethod = value.TransportMethod ?? string.Empty;
                EditTtcpWeight = value.TtcpWeight?.ToString() ?? string.Empty;
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
            
            // Note: Custom multi-parameter searches can fall back to standard keyword filters appropriately
            var list = await repo.SearchAsync(SearchVehiclePlate, CancellationToken.None);
            Vehicles.Clear();
            foreach (var item in list)
            {
                Vehicles.Add(item);
            }
        }

        [RelayCommand]
        private void ResetForm()
        {
            EditVehiclePlate = string.Empty;
            EditMoocNumber = string.Empty;
            EditDriverName = string.Empty;
            EditTransportMethod = string.Empty;
            EditTtcpWeight = string.Empty;
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

            if (string.IsNullOrWhiteSpace(EditVehiclePlate))
            {
                await dialogService.ShowErrorAsync("Lỗi", "Biển số xe không được rỗng!");
                return;
            }

            decimal? ttcpParsed = null;
            if (!string.IsNullOrWhiteSpace(EditTtcpWeight) && decimal.TryParse(EditTtcpWeight, out var val))
            {
                ttcpParsed = val;
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
                        TransportMethod = EditTransportMethod.Trim(),
                        TtcpWeight = ttcpParsed,
                        VehicleRegistrationNo = EditVehicleRegistrationNo.Trim(),
                        VehicleRegistrationExpiryDate = EditVehicleRegistrationExpiryDate,
                        MoocRegistrationNo = EditMoocRegistrationNo.Trim(),
                        MoocRegistrationExpiryDate = EditMoocRegistrationExpiryDate,
                        IsActive = EditIsActive,
                        CreatedAt = clock.NowLocal,
                        CreatedBy = "Operator"
                    };
                    await repo.AddAsync(newVehicle, CancellationToken.None);
                }
                else
                {
                    SelectedVehicle.VehiclePlate = EditVehiclePlate.Trim();
                    SelectedVehicle.MoocNumber = EditMoocNumber.Trim();
                    SelectedVehicle.DriverName = EditDriverName.Trim();
                    SelectedVehicle.TransportMethod = EditTransportMethod.Trim();
                    SelectedVehicle.TtcpWeight = ttcpParsed;
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
                        existing.TransportMethod = EditTransportMethod.Trim();
                        existing.TtcpWeight = ttcpParsed;
                        existing.VehicleRegistrationNo = EditVehicleRegistrationNo.Trim();
                        existing.VehicleRegistrationExpiryDate = EditVehicleRegistrationExpiryDate;
                        existing.MoocRegistrationNo = EditMoocRegistrationNo.Trim();
                        existing.MoocRegistrationExpiryDate = EditMoocRegistrationExpiryDate;
                        existing.IsActive = EditIsActive;
                        existing.UpdatedAt = clock.NowLocal;
                        existing.UpdatedBy = "Operator";
                        await repo.UpdateAsync(existing, CancellationToken.None);
                    }
                    else
                    {
                        await repo.UpdateAsync(SelectedVehicle, CancellationToken.None);
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

                SelectedVehicle.IsActive = false;
                await repo.UpdateAsync(SelectedVehicle, CancellationToken.None);
                await uow.SaveChangesAsync(CancellationToken.None);

                await dialogService.ShowInfoAsync("Thông báo", "Đã chuyển đổi trạng thái ngừng sử dụng.");
                ResetForm();
                await SearchAsync();
            }
        }
    }
}
