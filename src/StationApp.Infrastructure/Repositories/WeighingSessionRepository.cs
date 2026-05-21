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
        return await _db.WeighingSessions
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
    }

    public async Task<WeighingSession?> GetBySessionNoAsync(string sessionNo, CancellationToken ct)
    {
        sessionNo = sessionNo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sessionNo))
        {
            return null;
        }

        return await _db.WeighingSessions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.SessionNo == sessionNo, ct);
    }

    public async Task<IReadOnlyList<WeighingSession>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<WeighingSession>();
        }

        return await _db.WeighingSessions
            .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighingSessionListItem>> SearchActiveSessionsAsync(string? keyword, CancellationToken ct)
    {
        var sessionsQuery = _db.WeighingSessions.AsNoTracking()
            .Where(x => !x.IsDeleted && !x.IsCancelled && x.SessionStatus != WeighingSessionStatus.COMPLETED && x.SessionStatus != WeighingSessionStatus.CANCELLED);

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
        var lineSummaries = await _db.WeighingSessionLines.AsNoTracking()
            .Where(x => !x.IsDeleted && sessionIds.Contains(x.WeighingSessionId))
            .GroupBy(x => x.WeighingSessionId)
            .Select(group => new
            {
                SessionId = group.Key,
                LineCount = group.Count(),
                AllPrinted = group.All(line => line.HasPrintedDeliveryTicket)
            })
            .ToListAsync(ct);

        var summaryBySessionId = lineSummaries.ToDictionary(x => x.SessionId);

        return sessions.Select(session =>
        {
            summaryBySessionId.TryGetValue(session.Id, out var summary);
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
                summary?.LineCount ?? 0,
                session.HasPrintedMasterWeighTicket,
                session.UseActualWeightForBaggedCutOrders,
                summary?.LineCount > 0 && summary.AllPrinted,
                session.CreatedAt,
                session.UpdatedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<OutgoingSessionListItem>> SearchCompletedSessionsAsync(string? keyword, DateTime? completedDate, CancellationToken ct)
    {
        var query = _db.WeighingSessions.AsNoTracking()
            .Where(x => !x.IsDeleted && !x.IsCancelled && x.SessionStatus == WeighingSessionStatus.COMPLETED);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.SessionNo.Contains(keyword) ||
                x.VehiclePlate.Contains(keyword) ||
                (x.MoocNumber != null && x.MoocNumber.Contains(keyword)));
        }

        if (completedDate.HasValue)
        {
            var start = completedDate.Value.Date;
            var end = start.AddDays(1);
            query = query.Where(x =>
                (x.UpdatedAt ?? x.CreatedAt) >= start &&
                (x.UpdatedAt ?? x.CreatedAt) < end);
        }

        var sessions = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var sessionIds = sessions.Select(x => x.Id).ToList();
        var lines = await _db.WeighingSessionLines.AsNoTracking()
            .Where(x => !x.IsDeleted && sessionIds.Contains(x.WeighingSessionId))
            .ToListAsync(ct);
        var registrations = await _db.CutOrders.AsNoTracking()
            .Where(x => x.WeighingSessionId.HasValue && sessionIds.Contains(x.WeighingSessionId.Value))
            .Select(x => new
            {
                SessionId = x.WeighingSessionId!.Value,
                x.ErpCutOrderId
            })
            .ToListAsync(ct);

        return sessions.Select(session =>
        {
            var sessionLines = lines.Where(x => x.WeighingSessionId == session.Id).ToList();
            var registrationSummary = string.Join(" / ",
                registrations
                    .Where(x => x.SessionId == session.Id)
                    .Select(x => x.ErpCutOrderId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .OrderBy(x => x)
                    .Cast<string>());

            return new OutgoingSessionListItem(
                session.Id,
                session.SessionNo,
                session.TransactionType,
                session.VehiclePlate,
                session.MoocNumber,
                session.DriverName,
                registrationSummary,
                sessionLines.Sum(x => x.PlannedWeight ?? 0m),
                session.Weight1,
                session.Weight2,
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
            .Where(x => !x.IsDeleted && x.WeighingSessionId == sessionId)
            .OrderBy(x => x.SequenceNo)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighingSessionLineItem>> GetLineItemsBySessionIdAsync(Guid sessionId, CancellationToken ct)
    {
        var items = await (
            from line in _db.WeighingSessionLines.AsNoTracking()
            join reg in _db.CutOrders.AsNoTracking()
                on line.CutOrderId equals reg.Id
            where !line.IsDeleted && line.WeighingSessionId == sessionId
            orderby line.SequenceNo
            select new WeighingSessionLineItem(
                line.Id,
                line.CutOrderId,
                line.SequenceNo,
                reg.ErpCutOrderId,
                line.CustomerName,
                line.DistributorName,
                line.ProductCode,
                line.ProductName,
                line.PlannedWeight,
                line.PlannedBagCount,
                line.ActualAllocatedWeight,
                line.ActualAllocatedBagCount,
                line.LineStatus,
                line.HasPrintedDeliveryTicket,
                reg.ProductType
            ))
            .ToListAsync(ct);

        var missingProductCodes = items
            .Where(x => string.IsNullOrWhiteSpace(x.ProductType) && !string.IsNullOrWhiteSpace(x.ProductCode))
            .Select(x => x.ProductCode!)
            .Distinct()
            .ToList();

        if (missingProductCodes.Count > 0)
        {
            var products = await _db.Products.AsNoTracking()
                .Where(x => missingProductCodes.Contains(x.ProductCode))
                .ToDictionaryAsync(x => x.ProductCode.Trim(), x => x.ProductType, ct);

            return items.Select(item =>
            {
                if (string.IsNullOrWhiteSpace(item.ProductType) && !string.IsNullOrWhiteSpace(item.ProductCode) && products.TryGetValue(item.ProductCode.Trim(), out var productType))
                {
                    return item with { ProductType = productType };
                }
                return item;
            }).ToList().AsReadOnly();
        }

        return items;
    }

    public async Task<WeighingSession?> GetReusablePendingWeight2SessionAsync(string vehiclePlate, string? moocNumber, TransactionType transactionType, CancellationToken ct)
    {
        vehiclePlate = vehiclePlate?.Trim() ?? string.Empty;
        moocNumber = moocNumber?.Trim();
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            return null;
        }

        var query = _db.WeighingSessions
            .Where(ws => !ws.IsDeleted
                && !ws.IsCancelled
                && ws.TransactionType == transactionType
                && ws.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
                && ws.Weight1.HasValue
                && ws.VehiclePlate == vehiclePlate);

        query = string.IsNullOrWhiteSpace(moocNumber)
            ? query.Where(ws => ws.MoocNumber == null || ws.MoocNumber == string.Empty)
            : query.Where(ws => ws.MoocNumber == moocNumber);

        return await query
            .Where(ws => !_db.CutOrders.Any(co =>
                !co.IsDeleted
                && !co.IsCancelled
                && co.WeighingSessionId == ws.Id))
            .OrderByDescending(ws => ws.Weight1Time ?? ws.UpdatedAt ?? ws.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}


