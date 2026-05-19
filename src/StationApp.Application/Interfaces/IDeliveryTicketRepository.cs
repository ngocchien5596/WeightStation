using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.Interfaces;

public interface IDeliveryTicketRepository
{
    Task AddAsync(DeliveryTicket ticket, CancellationToken ct);
    Task UpdateAsync(DeliveryTicket ticket, CancellationToken ct);
    Task<DeliveryTicket?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetByErpCutOrderIdAsync(string erpCutOrderId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetBySplitGroupIdAsync(Guid splitGroupId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetAllByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryTicket>> GetBySyncStatusAsync(SyncStatus syncStatus, int take, CancellationToken ct);
    Task<DeliveryTicket?> GetPrimaryByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct);
}


