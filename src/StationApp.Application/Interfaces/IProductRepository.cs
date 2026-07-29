using StationApp.Application.DTOs;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.Interfaces;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken ct);
    Task UpdateAsync(Product product, CancellationToken ct);
    Task<Product?> GetByCodeAsync(string productCode, CancellationToken ct);
    Task<IReadOnlyList<Product>> SearchAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<ProductAutocompleteSource>> SearchAutocompleteAsync(string keyword, int limit, CancellationToken ct, TransactionType? transactionType = null);
}
