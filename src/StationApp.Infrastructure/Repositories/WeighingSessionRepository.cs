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
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.LastSyncError = null;
        await _db.WeighingSessions.AddAsync(session, ct);
    }

    public Task UpdateAsync(WeighingSession session, CancellationToken ct)
    {
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.LastSyncError = null;
        if (_db.Entry(session).State == EntityState.Detached)
        {
            _db.WeighingSessions.Update(session);
        }
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

    public async Task<IReadOnlyList<WeighingSession>> GetBySyncStatusAsync(SyncStatus syncStatus, int batchSize, CancellationToken ct)
    {
        return await _db.WeighingSessions
            .Where(x => !x.IsDeleted && x.SyncStatus == syncStatus)
            .OrderBy(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task ApplySyncResultAsync(Guid sessionId, SyncStatus syncStatus, DateTime attemptedAt, string? error, CancellationToken ct)
    {
        var session = await _db.WeighingSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session == null)
        {
            return;
        }

        session.SyncStatus = syncStatus;
        session.LastSyncAttemptAt = attemptedAt;
        session.LastSyncError = error;
        session.UpdatedAt ??= attemptedAt;
    }

    public async Task<IReadOnlyList<WeighingSessionListItem>> SearchActiveSessionsAsync(string? keyword, TransactionType? transactionType, CancellationToken ct)
    {
        var sessionsQuery = _db.WeighingSessions.AsNoTracking()
            .Where(x => !x.IsDeleted && !x.IsCancelled && x.SessionStatus != WeighingSessionStatus.COMPLETED && x.SessionStatus != WeighingSessionStatus.CANCELLED);

        if (transactionType is null)
        {
            var exportSessionIdsByLine = await (
                from line in _db.WeighingSessionLines.AsNoTracking()
                join cutOrder in _db.CutOrders.AsNoTracking()
                    on line.CutOrderId equals cutOrder.Id
                where !line.IsDeleted
                    && !cutOrder.IsDeleted
                    && cutOrder.IsExportScale
                select line.WeighingSessionId)
                .Distinct()
                .ToListAsync(ct);

            var exportSessionIdsByCutOrder = await _db.CutOrders.AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.IsExportScale
                    && x.WeighingSessionId.HasValue)
                .Select(x => x.WeighingSessionId!.Value)
                .Distinct()
                .ToListAsync(ct);

            var exportSessionIds = exportSessionIdsByLine
                .Concat(exportSessionIdsByCutOrder)
                .Distinct()
                .ToList();

            if (exportSessionIds.Count > 0)
            {
                sessionsQuery = sessionsQuery.Where(x => !exportSessionIds.Contains(x.Id));
            }
        }

        if (transactionType.HasValue)
        {
            sessionsQuery = sessionsQuery.Where(x => x.TransactionType == transactionType.Value);
        }

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
            .Where(x => !x.IsDeleted && sessionIds.Contains(x.WeighingSessionId))
            .ToListAsync(ct);

        return sessions.Select(session =>
        {
            var sessionLines = lines.Where(x => x.WeighingSessionId == session.Id).ToList();
            var lineCount = sessionLines.Count;
            var allPrinted = lineCount > 0 && sessionLines.All(x => x.HasPrintedDeliveryTicket);

            var customerSummary = string.Join(" / ", sessionLines
                .Select(x => x.CustomerName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct());

            var productGroups = sessionLines
                .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
                .GroupBy(x => new
                {
                    ProductCode = (x.ProductCode ?? string.Empty).Trim(),
                    ProductName = (x.ProductName ?? string.Empty).Trim()
                })
                .Select(group => new
                {
                    group.Key.ProductName,
                    PlannedWeight = group.Sum(x => x.PlannedWeight ?? 0m)
                })
                .ToList();

            var productSummary = productGroups.Count == 0
                ? null
                : string.Join(" / ", productGroups.Select(x => $"{x.ProductName} ({x.PlannedWeight:N0})"));

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
                lineCount,
                session.HasPrintedMasterWeighTicket,
                session.UseActualWeightForBaggedCutOrders,
                session.IsNoLoad,
                allPrinted,
                session.CreatedAt,
                session.UpdatedAt,
                customerSummary,
                productSummary);
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
                (x.Weight2Time ?? x.Weight1Time ?? x.CreatedAt) >= start &&
                (x.Weight2Time ?? x.Weight1Time ?? x.CreatedAt) < end);
        }

        var sessions = await query
            .OrderByDescending(x => x.Weight2Time ?? x.Weight1Time ?? x.CreatedAt)
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
                session.Weight2Time ?? session.Weight1Time ?? session.CreatedAt);
        }).ToList();
    }

    public async Task AddLineAsync(WeighingSessionLine line, CancellationToken ct)
    {
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncError = null;
        await _db.WeighingSessionLines.AddAsync(line, ct);
    }

    public Task UpdateLineAsync(WeighingSessionLine line, CancellationToken ct)
    {
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncError = null;
        if (_db.Entry(line).State == EntityState.Detached)
        {
            _db.WeighingSessionLines.Update(line);
        }
        return Task.CompletedTask;
    }

    public async Task<WeighingSessionLine?> GetLineByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.WeighingSessionLines
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<WeighingSessionLine>> GetLinesBySyncStatusAsync(SyncStatus syncStatus, int batchSize, CancellationToken ct)
    {
        return await _db.WeighingSessionLines
            .Where(x => !x.IsDeleted && x.SyncStatus == syncStatus)
            .OrderBy(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
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
                reg.ProductType,
                reg.Notes
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

    public async Task ApplyLineSyncResultAsync(Guid lineId, SyncStatus syncStatus, DateTime attemptedAt, string? error, CancellationToken ct)
    {
        var line = await _db.WeighingSessionLines.FirstOrDefaultAsync(x => x.Id == lineId, ct);
        if (line == null)
        {
            return;
        }

        line.SyncStatus = syncStatus;
        line.LastSyncAttemptAt = attemptedAt;
        line.LastSyncError = error;
        line.UpdatedAt ??= attemptedAt;
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


