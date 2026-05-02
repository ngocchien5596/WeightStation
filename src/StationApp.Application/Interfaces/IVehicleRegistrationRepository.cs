using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IVehicleRegistrationRepository
{
    Task AddAsync(VehicleRegistration registration, CancellationToken ct);
    Task UpdateAsync(VehicleRegistration registration, CancellationToken ct);
    Task<VehicleRegistration?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<VehicleRegistration>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<VehicleRegistration?> GetByErpIdAsync(string erpVehicleRegistrationId, CancellationToken ct);
    Task<IReadOnlyList<VehicleRegistration>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct);
    Task<IReadOnlyList<VehicleRegistration>> SearchAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<VehicleRegistration>> GetUnprocessedInboundAsync(CancellationToken ct);
    Task<IReadOnlyList<WeightViewListItem>> GetWeightViewListAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<IncomingVehicleListItem>> GetIncomingListAsync(IncomingVehicleListFilter filter, CancellationToken ct);
    Task<IReadOnlyList<OutgoingVehicleListItem>> GetOutgoingListAsync(OutgoingVehicleListFilter filter, CancellationToken ct);
    Task<IReadOnlyList<VehicleAutocompleteSource>> SearchVehicleHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<VehicleAutocompleteSource>> SearchMoocHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<DriverAutocompleteSource>> SearchDriverHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<CustomerAutocompleteSource>> SearchCustomerHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<ProductAutocompleteSource>> SearchProductCodeHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<ProductAutocompleteSource>> SearchProductNameHistorySourcesAsync(string keyword, int limit, CancellationToken ct);
}
