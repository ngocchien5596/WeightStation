using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public sealed class WeighingSessionRepository : IWeighingSessionRepository
{
    private readonly StationDbContext _db;

    public WeighingSessionRepository(StationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(WeighingSession session, CancellationToken ct)
    {
        await _db.WeighingSessions.AddAsync(session, ct);
    }

    public Task UpdateAsync(WeighingSession session, CancellationToken ct)
    {
        _db.WeighingSessions.Update(session);
        return Task.CompletedTask;
    }

    public async Task<WeighingSession?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.WeighingSessions.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<WeighingSession>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<WeighingSession>();
        }

        return await _db.WeighingSessions
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighingSessionListItem>> SearchActiveSessionsAsync(string? keyword, CancellationToken ct)
    {
        var sessionsQuery = _db.WeighingSessions.AsNoTracking()
            .Where(x => !x.IsCancelled && x.SessionStatus != WeighingSessionStatus.COMPLETED && x.SessionStatus != WeighingSessionStatus.CANCELLED);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sessionsQuery = sessionsQuery.Where(x =>
                x.SessionNo.Contains(keyword) ||
                x.VehiclePlate.Contains(keyword) ||
                (x.MoocNumber != null && x.MoocNumber.Contains(keyword)));
        }

        var sessions = await sessionsQuery
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var sessionIds = sessions.Select(x => x.Id).ToList();
        var lines = await _db.WeighingSessionLines.AsNoTracking()
            .Where(x => sessionIds.Contains(x.WeighingSessionId))
            .ToListAsync(ct);

        return sessions.Select(session =>
        {
            var sessionLines = lines.Where(x => x.WeighingSessionId == session.Id).ToList();
            return new WeighingSessionListItem(
                session.Id,
                session.SessionNo,
                session.TransactionType,
                session.VehiclePlate,
                session.MoocNumber,
                session.DriverName,
                session.Weight1,
                session.Weight2,
                session.NetWeight,
                session.Ttcp10WeightSnapshot,
                session.IsOverweight,
                session.OverweightAmount,
                session.OverweightResolutionStatus,
                session.SessionStatus,
                sessionLines.Count,
                session.HasPrintedMasterWeighTicket,
                sessionLines.Count > 0 && sessionLines.All(x => x.HasPrintedDeliveryTicket),
                session.CreatedAt,
                session.UpdatedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<OutgoingSessionListItem>> SearchCompletedSessionsAsync(string? keyword, CancellationToken ct)
    {
        var query = _db.WeighingSessions.AsNoTracking()
            .Where(x => !x.IsCancelled && x.SessionStatus == WeighingSessionStatus.COMPLETED);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.SessionNo.Contains(keyword) ||
                x.VehiclePlate.Contains(keyword) ||
                (x.MoocNumber != null && x.MoocNumber.Contains(keyword)));
        }

        var sessions = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var sessionIds = sessions.Select(x => x.Id).ToList();
        var lines = await _db.WeighingSessionLines.AsNoTracking()
            .Where(x => sessionIds.Contains(x.WeighingSessionId))
            .ToListAsync(ct);

        return sessions.Select(session =>
        {
            var sessionLines = lines.Where(x => x.WeighingSessionId == session.Id).ToList();
            return new OutgoingSessionListItem(
                session.Id,
                session.SessionNo,
                session.TransactionType,
                session.VehiclePlate,
                session.MoocNumber,
                session.DriverName,
                session.NetWeight,
                sessionLines.Count,
                session.HasPrintedMasterWeighTicket,
                sessionLines.Count > 0 && sessionLines.All(x => x.HasPrintedDeliveryTicket),
                session.UpdatedAt ?? session.CreatedAt);
        }).ToList();
    }

    public async Task AddLineAsync(WeighingSessionLine line, CancellationToken ct)
    {
        await _db.WeighingSessionLines.AddAsync(line, ct);
    }

    public Task UpdateLineAsync(WeighingSessionLine line, CancellationToken ct)
    {
        _db.WeighingSessionLines.Update(line);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<WeighingSessionLine>> GetLinesBySessionIdAsync(Guid sessionId, CancellationToken ct)
    {
        return await _db.WeighingSessionLines
            .Where(x => x.WeighingSessionId == sessionId)
            .OrderBy(x => x.SequenceNo)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighingSessionLineItem>> GetLineItemsBySessionIdAsync(Guid sessionId, CancellationToken ct)
    {
        return await (
            from line in _db.WeighingSessionLines.AsNoTracking()
            join reg in _db.VehicleRegistrations.AsNoTracking()
                on line.VehicleRegistrationId equals reg.Id
            where line.WeighingSessionId == sessionId
            orderby line.SequenceNo
            select new WeighingSessionLineItem(
                line.Id,
                line.VehicleRegistrationId,
                line.SequenceNo,
                reg.ErpVehicleRegistrationId,
                line.CustomerName,
                line.DistributorName,
                line.ProductCode,
                line.ProductName,
                line.PlannedWeight,
                line.PlannedBagCount,
                line.ActualAllocatedWeight,
                line.ActualAllocatedBagCount,
                line.LineStatus,
                line.HasPrintedDeliveryTicket))
            .ToListAsync(ct);
    }
}
