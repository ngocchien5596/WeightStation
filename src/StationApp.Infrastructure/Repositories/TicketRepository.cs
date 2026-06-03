using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository, IWeighTicketRepository
{
    private readonly StationDbContext _db;
    public TicketRepository(StationDbContext db) => _db = db;

    public async Task AddAsync(WeighTicket ticket, CancellationToken ct)
    {
        SyncTrackedEntityUpdateHelper.PrepareForAdd(ticket);
        await _db.WeighTickets.AddAsync(ticket, ct);
    }

    public Task UpdateAsync(WeighTicket ticket, CancellationToken ct)
    {
        SyncTrackedEntityUpdateHelper.PrepareForUpdate(_db, ticket);
        _db.WeighTickets.Update(ticket);
        return Task.CompletedTask;
    }

    public async Task<WeighTicket?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.WeighTickets.FindAsync(new object[] { id }, ct);

    public async Task<WeighTicket?> GetByTicketNoAsync(string ticketNo, CancellationToken ct)
        => await _db.WeighTickets.FirstOrDefaultAsync(t => t.TicketNo == ticketNo && !t.IsDeleted, ct);

    public async Task<IReadOnlyList<WeighTicket>> GetByStatusAsync(TicketStatus status, CancellationToken ct)
        => await _db.WeighTickets
            .Where(t => t.Status == status && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WeighTicket>> SearchAsync(string? keyword, TicketStatus? status, CancellationToken ct)
    {
        var query = _db.WeighTickets.Where(t => !t.IsDeleted);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(t => t.TicketNo.Contains(keyword) || t.VehiclePlate.Contains(keyword));
        return await query.OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync(ct);
    }

    public async Task<bool> ExistsByTicketNoAsync(string ticketNo, CancellationToken ct)
        => await _db.WeighTickets.AnyAsync(t => t.TicketNo == ticketNo && !t.IsDeleted, ct);

    public async Task<IReadOnlyList<WeighTicket>> GetPrimaryDisplayTicketsAsync(string? keyword, CancellationToken ct)
    {
        var query = _db.WeighTickets.Where(t => t.RecordRole == WeighTicketRecordRoles.MasterSession && t.IsPrimaryDisplay && !t.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(t => t.TicketNo.Contains(keyword) || t.VehiclePlate.Contains(keyword));
        
        return await query.OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighTicket>> GetRelatedTicketsAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await _db.WeighTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null) return Array.Empty<WeighTicket>();

        // Find by split group if exists, or just the ticket itself + any source it came from
        if (ticket.SplitGroupId.HasValue)
        {
            return await _db.WeighTickets
                .Where(t => t.SplitGroupId == ticket.SplitGroupId.Value && !t.IsDeleted)
                .OrderBy(t => t.SplitSequence)
                .ToListAsync(ct);
        }

        if (ticket.SourceTicketId.HasValue)
        {
            return await _db.WeighTickets
                .Where(t => !t.IsDeleted && (t.Id == ticket.SourceTicketId.Value || t.Id == ticket.Id))
                .ToListAsync(ct);
        }

        return ticket.IsDeleted ? Array.Empty<WeighTicket>() : new List<WeighTicket> { ticket };
    }

    public async Task<IReadOnlyList<WeighTicket>> GetByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        return await _db.WeighTickets
            .Where(t => t.CutOrderId == cutOrderId && !t.IsDeleted)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighTicket>> GetAllByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        return await _db.WeighTickets
            .Where(t => t.CutOrderId == cutOrderId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighTicket>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct)
    {
        return await _db.WeighTickets
            .Where(t => t.WeighingSessionId == weighingSessionId && !t.IsDeleted)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighTicket>> GetBySyncStatusAsync(SyncStatus syncStatus, int take, CancellationToken ct)
    {
        return await _db.WeighTickets
            .Where(t => t.SyncStatus == syncStatus && !t.IsDeleted)
            .OrderBy(t => t.UpdatedAt ?? t.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<WeighTicket?> GetPrimaryByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        return await _db.WeighTickets
            .Where(t => t.CutOrderId == cutOrderId && t.IsPrimaryDisplay && !t.IsDeleted)
            .OrderBy(t => t.SplitSequence ?? 0)
            .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<WeighTicket?> GetPrimaryByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct)
    {
        return await _db.WeighTickets
            .Where(t => t.WeighingSessionId == weighingSessionId
                && t.RecordRole == WeighTicketRecordRoles.MasterSession
                && !t.IsDeleted)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}

