using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public class DeliveryTicketRepository : IDeliveryTicketRepository
{
    private readonly StationDbContext _context;

    public DeliveryTicketRepository(StationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeliveryTicket ticket, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ticket.StationCode))
        {
            ticket.StationCode = await ResolveDeliveryTicketStationCodeAsync(ticket, ct)
                ?? await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        }

        SyncTrackedEntityUpdateHelper.PrepareForAdd(ticket);
        await _context.DeliveryTickets.AddAsync(ticket, ct);
    }

    public async Task UpdateAsync(DeliveryTicket ticket, CancellationToken ct)
    {
        SyncTrackedEntityUpdateHelper.PrepareForUpdate(_context, ticket);
        if (_context.Entry(ticket).State == EntityState.Detached)
        {
            _context.DeliveryTickets.Update(ticket);
        }
        await Task.CompletedTask;
    }

    public async Task<DeliveryTicket?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.DeliveryTickets.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetByErpCutOrderIdAsync(string erpCutOrderId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.ErpCutOrderId == erpCutOrderId && !d.IsDeleted)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetBySplitGroupIdAsync(Guid splitGroupId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.SplitGroupId == splitGroupId && !d.IsDeleted)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.CutOrderId == cutOrderId && !d.IsDeleted)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetAllByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.CutOrderId == cutOrderId)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.WeighingSessionId == weighingSessionId && !d.IsDeleted)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetBySyncStatusAsync(SyncStatus syncStatus, int take, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        var list = await _context.DeliveryTickets
            .Where(d => d.StationCode == stationCode && d.SyncStatus == syncStatus && !d.IsDeleted)
            .OrderBy(d => d.UpdatedAt ?? d.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<DeliveryTicket?> GetPrimaryByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        return await _context.DeliveryTickets
            .Where(d => d.CutOrderId == cutOrderId && d.RecordRole == DeliveryTicketRecordRoles.Normal && !d.IsDeleted)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string?> ResolveDeliveryTicketStationCodeAsync(DeliveryTicket ticket, CancellationToken ct)
    {
        if (ticket.WeighingSessionId.HasValue)
        {
            var sessionStationCode = await _context.WeighingSessions.AsNoTracking()
                .Where(x => x.Id == ticket.WeighingSessionId.Value)
                .Select(x => x.StationCode)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(sessionStationCode))
            {
                return sessionStationCode;
            }
        }

        return await _context.CutOrders.AsNoTracking()
            .Where(x => x.Id == ticket.CutOrderId)
            .Select(x => x.StationCode)
            .FirstOrDefaultAsync(ct);
    }
}
