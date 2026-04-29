using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken ct);
    Task UpdateAsync(Product product, CancellationToken ct);
    Task<Product?> GetByCodeAsync(string productCode, CancellationToken ct);
    Task<IReadOnlyList<Product>> SearchAsync(string? keyword, CancellationToken ct);
}
