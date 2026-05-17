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
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings
{
    public partial class ProductMasterViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ProductMasterViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;

        [ObservableProperty] private string _editCode = string.Empty;
        [ObservableProperty] private string _editName = string.Empty;
        [ObservableProperty] private bool _editIsActive = true;

        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private Product? _selectedProduct;

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                EditCode = value.ProductCode;
                EditName = value.ProductName;
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
            var repo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            var list = await repo.SearchAsync(null, CancellationToken.None);
            var filtered = list.Where(x =>
                MatchesSearch(x.ProductCode, SearchCode)
                && MatchesSearch(x.ProductName, SearchName));

            Products.Clear();
            foreach (var item in filtered)
            {
                Products.Add(item);
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
            SelectedProduct = null;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();
            var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
            var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

            if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
            {
                await dialogService.ShowErrorAsync("Lỗi", "Mã và Tên sản phẩm không được rỗng!");
                return;
            }

            try
            {
                var existing = await repo.GetByCodeAsync(EditCode.Trim(), CancellationToken.None);
                if (existing != null && (SelectedProduct == null || existing.Id != SelectedProduct.Id))
                {
                    await dialogService.ShowWarningAsync("Lỗi", "Mã sản phẩm đã tồn tại trên hệ thống!");
                    return;
                }

                if (SelectedProduct == null)
                {
                    var newProduct = new Product
                    {
                        Id = Guid.NewGuid(),
                        ProductCode = EditCode.Trim(),
                        ProductName = EditName.Trim(),
                        IsActive = EditIsActive,
                        CreatedAt = clock.NowLocal,
                        CreatedBy = "Operator"
                    };
                    await repo.AddAsync(newProduct, CancellationToken.None);
                    await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, newProduct, clock.NowLocal);
                }
                else
                {
                    SelectedProduct.ProductCode = EditCode.Trim();
                    SelectedProduct.ProductName = EditName.Trim();
                    SelectedProduct.IsActive = EditIsActive;
                    SelectedProduct.UpdatedAt = clock.NowLocal;
                    SelectedProduct.UpdatedBy = "Operator";

                    if (existing != null)
                    {
                        existing.ProductCode = EditCode.Trim();
                        existing.ProductName = EditName.Trim();
                        existing.IsActive = EditIsActive;
                        existing.UpdatedAt = clock.NowLocal;
                        existing.UpdatedBy = "Operator";
                        await repo.UpdateAsync(existing, CancellationToken.None);
                        await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, existing, clock.NowLocal);
                    }
                    else
                    {
                        await repo.UpdateAsync(SelectedProduct, CancellationToken.None);
                        await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, SelectedProduct, clock.NowLocal);
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
            if (SelectedProduct == null) return;

            using var scope = _scopeFactory.CreateScope();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

            var result = await dialogService.ShowConfirmAsync(
                "Xác nhận", 
                $"Bạn có chắc muốn ngừng sử dụng sản phẩm {SelectedProduct.ProductName}?", 
                "Đồng ý", 
                "Bỏ qua"
            );

            if (result)
            {
                var repo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
                var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

                SelectedProduct.IsActive = false;
                await repo.UpdateAsync(SelectedProduct, CancellationToken.None);
                await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, SelectedProduct, clock.NowLocal);
                await uow.SaveChangesAsync(CancellationToken.None);

                await dialogService.ShowInfoAsync("Thông báo", "Đã chuyển đổi trạng thái ngừng sử dụng.");
                ResetForm();
                await SearchAsync();
            }
        }

        private static async Task EnqueueMasterSyncAsync(
            ISyncOutboxRepository outboxRepo,
            ISyncPayloadFactory payloadFactory,
            Product product,
            DateTime now)
        {
            await outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = product.Id,
                AggregateType = SyncAggregateTypes.Product,
                PayloadJson = payloadFactory.CreatePayload(product),
                IdempotencyKey = product.Id,
                Status = OutboxStatus.PENDING,
                RetryCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            }, CancellationToken.None);
        }
    }
}
