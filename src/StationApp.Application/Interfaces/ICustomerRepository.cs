using StationApp.Application.DTOs;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.Interfaces;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken ct);
    Task UpdateAsync(Customer customer, CancellationToken ct);
    Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken ct);
    Task<IReadOnlyList<Customer>> SearchAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<CustomerAutocompleteSource>> SearchAutocompleteAsync(string keyword, int limit, CancellationToken ct, TransactionType? transactionType = null);
}
