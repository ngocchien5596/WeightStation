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
    Task<VehicleRegistration?> GetByErpIdAsync(string erpVehicleRegistrationId, CancellationToken ct);
    Task<IReadOnlyList<VehicleRegistration>> SearchAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<VehicleRegistration>> GetUnprocessedInboundAsync(CancellationToken ct);
    Task<IReadOnlyList<WeightViewListItem>> GetWeightViewListAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<IncomingVehicleListItem>> GetIncomingListAsync(IncomingVehicleListFilter filter, CancellationToken ct);
    Task<IReadOnlyList<OutgoingVehicleListItem>> GetOutgoingListAsync(OutgoingVehicleListFilter filter, CancellationToken ct);
}
