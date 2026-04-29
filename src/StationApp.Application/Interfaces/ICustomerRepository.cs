using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken ct);
    Task UpdateAsync(Customer customer, CancellationToken ct);
    Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken ct);
    Task<IReadOnlyList<Customer>> SearchAsync(string? keyword, CancellationToken ct);
}
