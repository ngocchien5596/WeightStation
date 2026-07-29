using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings;

public sealed record ProductTypeSearchOption(string? Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public partial class ProductMasterViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProductMasterViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [ObservableProperty] private string _searchCode = string.Empty;
    [ObservableProperty] private string _searchName = string.Empty;
    [ObservableProperty] private ProductTypeSearchOption? _selectedSearchProductTypeOption;

    [ObservableProperty] private string _editCode = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string? _editType;
    [ObservableProperty] private bool _editIsActive = true;

    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private Product? _selectedProduct;

    public IReadOnlyList<string> ProductTypeOptions { get; } = ProductTypes.All;
    public IReadOnlyList<ProductTypeSearchOption> SearchProductTypeOptions { get; } =
    [
        new(null, "T\u1ea5t c\u1ea3"),
        .. ProductTypes.All.Select(x => new ProductTypeSearchOption(x, x))
    ];

    partial void OnSelectedProductChanged(Product? value)
    {
        if (value == null)
        {
            return;
        }

        EditCode = value.ProductCode;
        EditName = value.ProductName;
        EditType = value.ProductType;
        EditIsActive = value.IsActive;
    }

    public async Task LoadAsync()
    {
        SelectedSearchProductTypeOption ??= SearchProductTypeOptions.FirstOrDefault();
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
            && MatchesSearch(x.ProductName, SearchName)
            && MatchesSearchProductType(x));

        Products.Clear();
        foreach (var item in filtered)
        {
            Products.Add(item);
        }
    }

    private bool MatchesSearchProductType(Product product)
    {
        var selectedType = ProductTypes.Normalize(SelectedSearchProductTypeOption?.Value);
        if (string.IsNullOrWhiteSpace(selectedType))
        {
            return true;
        }

        return string.Equals(ProductTypes.Normalize(product.ProductType), selectedType, StringComparison.OrdinalIgnoreCase);
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
        EditType = null;
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

        var normalizedType = ProductTypes.Normalize(EditType);
        if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(normalizedType))
        {
            await dialogService.ShowErrorAsync(
                "L\u1ed7i",
                "M\u00e3, T\u00ean v\u00e0 Lo\u1ea1i s\u1ea3n ph\u1ea9m kh\u00f4ng \u0111\u01b0\u1ee3c r\u1ed7ng!");
            return;
        }

        try
        {
            var existing = await repo.GetByCodeAsync(EditCode.Trim(), CancellationToken.None);
            if (existing != null && (SelectedProduct == null || existing.Id != SelectedProduct.Id))
            {
                await dialogService.ShowWarningAsync(
                    "L\u1ed7i",
                    "M\u00e3 s\u1ea3n ph\u1ea9m \u0111\u00e3 t\u1ed3n t\u1ea1i tr\u00ean h\u1ec7 th\u1ed1ng!");
                return;
            }

            if (SelectedProduct == null)
            {
                var newProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    ProductCode = EditCode.Trim(),
                    ProductName = EditName.Trim(),
                    ProductType = normalizedType,
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
                SelectedProduct.ProductType = normalizedType;
                SelectedProduct.IsActive = EditIsActive;
                SelectedProduct.UpdatedAt = clock.NowLocal;
                SelectedProduct.UpdatedBy = "Operator";

                if (existing != null)
                {
                    existing.ProductCode = EditCode.Trim();
                    existing.ProductName = EditName.Trim();
                    existing.ProductType = normalizedType;
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
            await dialogService.ShowInfoAsync(
                "Th\u00f4ng b\u00e1o",
                "L\u01b0u d\u1eef li\u1ec7u th\u00e0nh c\u00f4ng!");

            ResetForm();
            await SearchAsync();
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync(
                "L\u1ed7i h\u1ec7 th\u1ed1ng",
                $"L\u1ed7i khi l\u01b0u d\u1eef li\u1ec7u: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        if (SelectedProduct == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

        var result = await dialogService.ShowConfirmAsync(
            "X\u00e1c nh\u1eadn",
            $"B\u1ea1n c\u00f3 ch\u1eafc mu\u1ed1n ng\u1eebng s\u1eed d\u1ee5ng s\u1ea3n ph\u1ea9m {SelectedProduct.ProductName}?",
            "\u0110\u1ed3ng \u00fd",
            "B\u1ecf qua");

        if (!result)
        {
            return;
        }

        var repo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
        var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();

        SelectedProduct.IsActive = false;
        SelectedProduct.UpdatedAt = clock.NowLocal;
        SelectedProduct.UpdatedBy = "Operator";
        await repo.UpdateAsync(SelectedProduct, CancellationToken.None);
        await EnqueueMasterSyncAsync(outboxRepo, payloadFactory, SelectedProduct, clock.NowLocal);
        await uow.SaveChangesAsync(CancellationToken.None);

        await dialogService.ShowInfoAsync(
            "Th\u00f4ng b\u00e1o",
            "\u0110\u00e3 chuy\u1ec3n \u0111\u1ed5i tr\u1ea1ng th\u00e1i ng\u1eebng s\u1eed d\u1ee5ng.");
        ResetForm();
        await SearchAsync();
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
