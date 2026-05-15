using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    public partial class CustomerMasterViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public CustomerMasterViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;

        [ObservableProperty] private string _editCode = string.Empty;
        [ObservableProperty] private string _editName = string.Empty;
        [ObservableProperty] private bool _editIsActive = true;

        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private Customer? _selectedCustomer;

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (value != null)
            {
                EditCode = value.CustomerCode;
                EditName = value.CustomerName;
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
            var repo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

            var list = await repo.SearchAsync(null, CancellationToken.None);
            var filtered = list.Where(x =>
                MatchesSearch(x.CustomerCode, SearchCode)
                && MatchesSearch(x.CustomerName, SearchName));

            Customers.Clear();
            foreach (var item in filtered)
            {
                Customers.Add(item);
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
            EditCode = string.Empty;
            EditName = string.Empty;
            EditIsActive = true;
            SelectedCustomer = null;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

            if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
            {
                await dialogService.ShowErrorAsync("Lỗi", "Mã và Tên khách hàng không được rỗng!");
                return;
            }

            try
            {
                var existing = await repo.GetByCodeAsync(EditCode.Trim(), CancellationToken.None);
                if (existing != null && (SelectedCustomer == null || existing.Id != SelectedCustomer.Id))
                {
                    await dialogService.ShowWarningAsync("Lỗi", "Mã khách hàng đã tồn tại trên hệ thống!");
                    return;
                }

                if (SelectedCustomer == null)
                {
                    var newCustomer = new Customer
                    {
                        Id = Guid.NewGuid(),
                        CustomerCode = EditCode.Trim(),
                        CustomerName = EditName.Trim(),
                        IsActive = EditIsActive,
                        CreatedAt = clock.NowLocal,
                        CreatedBy = "Operator"
                    };
                    await repo.AddAsync(newCustomer, CancellationToken.None);
                }
                else
                {
                    SelectedCustomer.CustomerCode = EditCode.Trim();
                    SelectedCustomer.CustomerName = EditName.Trim();
                    SelectedCustomer.IsActive = EditIsActive;
                    SelectedCustomer.UpdatedAt = clock.NowLocal;
                    SelectedCustomer.UpdatedBy = "Operator";

                    if (existing != null)
                    {
                        existing.CustomerCode = EditCode.Trim();
                        existing.CustomerName = EditName.Trim();
                        existing.IsActive = EditIsActive;
                        existing.UpdatedAt = clock.NowLocal;
                        existing.UpdatedBy = "Operator";
                        await repo.UpdateAsync(existing, CancellationToken.None);
                    }
                    else
                    {
                        await repo.UpdateAsync(SelectedCustomer, CancellationToken.None);
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
            if (SelectedCustomer == null) return;

            using var scope = _scopeFactory.CreateScope();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

            var result = await dialogService.ShowConfirmAsync(
                "Xác nhận", 
                $"Bạn có chắc muốn ngừng sử dụng khách hàng {SelectedCustomer.CustomerName}?", 
                "Đồng ý", 
                "Bỏ qua"
            );

            if (result)
            {
                var repo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                SelectedCustomer.IsActive = false;
                await repo.UpdateAsync(SelectedCustomer, CancellationToken.None);
                await uow.SaveChangesAsync(CancellationToken.None);

                await dialogService.ShowInfoAsync("Thông báo", "Đã chuyển đổi trạng thái ngừng sử dụng.");
                ResetForm();
                await SearchAsync();
            }
        }
    }
}
