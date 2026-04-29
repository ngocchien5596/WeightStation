using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.Interfaces;

public interface ITicketRepository
{
    Task AddAsync(WeighTicket ticket, CancellationToken ct);
    Task UpdateAsync(WeighTicket ticket, CancellationToken ct);
    Task<WeighTicket?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<WeighTicket?> GetByTicketNoAsync(string ticketNo, CancellationToken ct);
    Task<IReadOnlyList<WeighTicket>> GetByStatusAsync(TicketStatus status, CancellationToken ct);
    Task<IReadOnlyList<WeighTicket>> SearchAsync(string? keyword, TicketStatus? status, CancellationToken ct);
    Task<bool> ExistsByTicketNoAsync(string ticketNo, CancellationToken ct);
    Task<IReadOnlyList<WeighTicket>> GetPrimaryDisplayTicketsAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<WeighTicket>> GetRelatedTicketsAsync(Guid ticketId, CancellationToken ct);
}
