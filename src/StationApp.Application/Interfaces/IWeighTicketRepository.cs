using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IWeighTicketRepository
{
    Task AddAsync(WeighTicket ticket, CancellationToken ct);
    Task UpdateAsync(WeighTicket ticket, CancellationToken ct);
    Task<IReadOnlyList<WeighTicket>> GetByVehicleRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task<IReadOnlyList<WeighTicket>> GetAllByVehicleRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task<WeighTicket?> GetPrimaryByVehicleRegistrationIdAsync(Guid registrationId, CancellationToken ct);
}
