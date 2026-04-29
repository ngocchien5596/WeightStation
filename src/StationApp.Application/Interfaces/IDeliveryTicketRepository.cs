using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IDeliveryTicketRepository
{
    Task AddAsync(DeliveryTicket ticket, CancellationToken ct);
    Task UpdateAsync(DeliveryTicket ticket, CancellationToken ct);
    Task<DeliveryTicket?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetByErpVehicleRegistrationIdAsync(string erpVehicleRegistrationId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetBySplitGroupIdAsync(Guid splitGroupId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetByVehicleRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetAllByVehicleRegistrationIdAsync(Guid registrationId, CancellationToken ct);
    Task<DeliveryTicket?> GetPrimaryByVehicleRegistrationIdAsync(Guid registrationId, CancellationToken ct);
}
