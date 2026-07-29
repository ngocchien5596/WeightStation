using StationApp.Application.DTOs;
using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IIncomingSeedVehicleRepository
{
    Task<IReadOnlyList<IncomingSeedVehicleListItem>> GetQn01Async(CancellationToken ct);
    Task<IncomingSeedVehicle?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IncomingSeedVehicle?> FindActiveDuplicateQn01Async(string customerCode, string productCode, Guid? excludeId, CancellationToken ct);
    Task<int> GetNextSortOrderQn01Async(CancellationToken ct);
    Task AddAsync(IncomingSeedVehicle seedVehicle, CancellationToken ct);
    Task UpdateAsync(IncomingSeedVehicle seedVehicle, CancellationToken ct);
}
