using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
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
        if (string.IsNullOrWhiteSpace(session.StationCode))
        {
            session.StationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        }

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

        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var fullSessionNo = BusinessNumberFormatter.PrefixWithStation(stationCode, sessionNo);

        return await _db.WeighingSessions
            .FirstOrDefaultAsync(
                x => !x.IsDeleted && (x.SessionNo == sessionNo || x.SessionNo == fullSessionNo),
                ct);
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
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        return await _db.WeighingSessions
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && x.SyncStatus == syncStatus)
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
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var sessionsQuery = _db.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && !x.IsCancelled && x.SessionStatus != WeighingSessionStatus.COMPLETED && x.SessionStatus != WeighingSessionStatus.CANCELLED);

        if (transactionType is null)
        {
            var exportSessionIdsByLine = await (
                from line in _db.WeighingSessionLines.AsNoTracking()
                join cutOrder in _db.CutOrders.AsNoTracking()
                    on line.CutOrderId equals cutOrder.Id
                where !line.IsDeleted
                    && line.StationCode == stationCode
                    && !cutOrder.IsDeleted
                    && cutOrder.StationCode == stationCode
                    && cutOrder.IsExportScale
                select line.WeighingSessionId)
                .Distinct()
                .ToListAsync(ct);

            var exportSessionIdsByCutOrder = await _db.CutOrders.AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.StationCode == stationCode
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

        var keywordText = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keywordText))
        {
            var sessionIdsByTicketNo = await _db.WeighTickets.AsNoTracking()
                .Where(x => x.StationCode == stationCode
                    && !x.IsDeleted
                    && x.WeighingSessionId.HasValue
                    && x.TicketNo.Contains(keywordText))
                .Select(x => x.WeighingSessionId!.Value)
                .Distinct()
                .ToListAsync(ct);

            sessionsQuery = sessionsQuery.Where(x =>
                x.SessionNo.Contains(keywordText) ||
                x.VehiclePlate.Contains(keywordText) ||
                (x.MoocNumber != null && x.MoocNumber.Contains(keywordText)) ||
                sessionIdsByTicketNo.Contains(x.Id));
        }

        var sessions = await sessionsQuery
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var sessionIds = sessions.Select(x => x.Id).ToList();
        var ticketBySessionId = await LoadPrimaryTicketBySessionIdAsync(sessionIds, ct);
        var lines = await _db.WeighingSessionLines.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && sessionIds.Contains(x.WeighingSessionId))
            .ToListAsync(ct);
        var cutOrderIds = lines.Select(x => x.CutOrderId).Distinct().ToList();
        var portTransferByCutOrderId = cutOrderIds.Count == 0
            ? new Dictionary<Guid, bool>()
            : await _db.CutOrders.AsNoTracking()
                .Where(x => x.StationCode == stationCode && cutOrderIds.Contains(x.Id) && !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id, x => x.IsPortTransfer, ct);
        var userDisplayByUsername = await LoadUserDisplayNameByUsernameAsync(
            sessions.SelectMany(x =>
            {
                ticketBySessionId.TryGetValue(x.Id, out var ticket);
                return new[]
                {
                    x.CreatedBy,
                    x.UpdatedBy,
                    ticket?.Weight1User,
                    ticket?.Weight2User
                };
            }),
            ct);

        return sessions.Select(session =>
        {
            var sessionLines = lines.Where(x => x.WeighingSessionId == session.Id).ToList();
            var lineCount = sessionLines.Count;
            var allPrinted = lineCount > 0 && sessionLines.All(x => x.HasPrintedDeliveryTicket);
            var isPortTransfer = sessionLines.Count > 0
                && sessionLines.All(x => portTransferByCutOrderId.GetValueOrDefault(x.CutOrderId));

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

            var primaryTicket = ticketBySessionId.GetValueOrDefault(session.Id);

            return new WeighingSessionListItem(
                session.Id,
                BusinessNumberFormatter.ToDisplay(session.SessionNo),
                session.TransactionType,
                session.VehiclePlate,
                session.MoocNumber,
                  session.DriverName,
                  session.Weight1,
                  session.Weight1Time,
                  session.Weight2,
                  session.Weight2Time,
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
                isPortTransfer,
                allPrinted,
                session.CreatedAt,
                session.UpdatedAt,
                customerSummary,
                productSummary,
                primaryTicket is null ? null : BusinessNumberFormatter.ToDisplay(primaryTicket.TicketNo),
                ResolveUserDisplayName(
                    userDisplayByUsername,
                    primaryTicket?.Weight1User ?? session.CreatedBy),
                ResolveUserDisplayName(
                    userDisplayByUsername,
                    primaryTicket?.Weight2User ?? (session.Weight2Time.HasValue ? session.UpdatedBy ?? session.CreatedBy : null)));
        }).ToList();
    }

    public async Task<IReadOnlyList<CrusherWeighingSessionListItem>> SearchCrusherSessionsAsync(string? keyword, DateTime? selectedDate, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var query = _db.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode
                && !x.IsDeleted
                && !x.IsCancelled
                && x.TransactionType == TransactionType.INBOUND
                && x.InternalVehicleNo != null);

        if (selectedDate.HasValue)
        {
            var date = selectedDate.Value.Date;
            var nextDate = date.AddDays(1);
            query = query.Where(x =>
                (x.Weight2Time != null && x.Weight2Time >= date && x.Weight2Time < nextDate)
                || (x.Weight2Time == null && x.CreatedAt >= date && x.CreatedAt < nextDate));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.VehiclePlate.Contains(normalized));
        }

        var sessions = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        var ticketBySessionId = await LoadPrimaryTicketBySessionIdAsync(sessions.Select(x => x.Id).ToList(), ct);
        var userDisplayByUsername = await LoadUserDisplayNameByUsernameAsync(
            sessions.SelectMany(x =>
            {
                ticketBySessionId.TryGetValue(x.Id, out var ticket);
                return new[]
                {
                    x.CreatedBy,
                    x.UpdatedBy,
                    ticket?.Weight1User,
                    ticket?.Weight2User
                };
            }),
            ct);

        return sessions
            .Select(x => new CrusherWeighingSessionListItem(
                x.Id,
                BusinessNumberFormatter.ToDisplay(x.SessionNo),
                x.VehiclePlate,
                x.DriverName,
                x.Weight1,
                x.Weight1Time,
                x.Weight2,
                x.Weight2Time,
                x.NetWeight,
                x.WeighingMode,
                x.StandardTareWeightSnapshot,
                x.StandardTareSourceSnapshot,
                x.SessionStatus,
                x.CreatedAt,
                x.UpdatedAt,
                x.ProductCode,
                x.ProductName,
                x.CustomerCode,
                x.CustomerName,
                x.IsReturnedBrokenTrip,
                ResolveUserDisplayName(
                    userDisplayByUsername,
                    ticketBySessionId.GetValueOrDefault(x.Id)?.Weight1User ?? x.CreatedBy),
                ResolveUserDisplayName(
                    userDisplayByUsername,
                    ticketBySessionId.GetValueOrDefault(x.Id)?.Weight2User ?? (x.Weight2Time.HasValue ? x.UpdatedBy ?? x.CreatedBy : null))))
            .ToList();
    }

    public async Task<IReadOnlyList<CrusherWeighingSessionListItem>> SearchClaySessionsAsync(string? keyword, DateTime? selectedDate, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var query = _db.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode
                && !x.IsDeleted
                && !x.IsCancelled
                && x.TransactionType == TransactionType.INBOUND
                && x.InternalVehicleNo != null);

        if (selectedDate.HasValue)
        {
            var date = selectedDate.Value.Date;
            var nextDate = date.AddDays(1);
            query = query.Where(x =>
                (x.Weight2Time != null && x.Weight2Time >= date && x.Weight2Time < nextDate)
                || (x.Weight2Time == null && x.CreatedAt >= date && x.CreatedAt < nextDate));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.VehiclePlate.Contains(normalized));
        }

        var sessions = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        var ticketBySessionId = await LoadPrimaryTicketBySessionIdAsync(sessions.Select(x => x.Id).ToList(), ct);
        var userDisplayByUsername = await LoadUserDisplayNameByUsernameAsync(
            sessions.SelectMany(x =>
            {
                ticketBySessionId.TryGetValue(x.Id, out var ticket);
                return new[]
                {
                    x.CreatedBy,
                    x.UpdatedBy,
                    ticket?.Weight1User,
                    ticket?.Weight2User
                };
            }),
            ct);

        return sessions
            .Select(x => new CrusherWeighingSessionListItem(
                x.Id,
                BusinessNumberFormatter.ToDisplay(x.SessionNo),
                x.VehiclePlate,
                x.DriverName,
                x.Weight1,
                x.Weight1Time,
                x.Weight2,
                x.Weight2Time,
                x.NetWeight,
                x.WeighingMode,
                x.StandardTareWeightSnapshot,
                x.StandardTareSourceSnapshot,
                x.SessionStatus,
                x.CreatedAt,
                x.UpdatedAt,
                x.ProductCode,
                x.ProductName,
                x.CustomerCode,
                x.CustomerName,
                x.IsReturnedBrokenTrip,
                ResolveUserDisplayName(
                    userDisplayByUsername,
                    ticketBySessionId.GetValueOrDefault(x.Id)?.Weight1User ?? x.CreatedBy),
                ResolveUserDisplayName(
                    userDisplayByUsername,
                    ticketBySessionId.GetValueOrDefault(x.Id)?.Weight2User ?? (x.Weight2Time.HasValue ? x.UpdatedBy ?? x.CreatedBy : null))))
            .ToList();
    }

    public async Task<ReturnedBrokenTripPreviousTripInfo?> GetPreviousCrusherTripForReturnedAsync(Guid sessionId, CancellationToken ct)
    {
        var current = await _db.WeighingSessions.AsNoTracking()
            .Where(x => x.Id == sessionId && !x.IsDeleted && !x.IsCancelled)
            .Select(x => new
            {
                x.Id,
                x.StationCode,
                x.VehiclePlate,
                x.InternalVehicleNo,
                CompletedAt = x.Weight2Time ?? x.UpdatedAt ?? x.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (current == null)
        {
            return null;
        }

        var vehicleNo = string.IsNullOrWhiteSpace(current.InternalVehicleNo)
            ? current.VehiclePlate
            : current.InternalVehicleNo;
        if (string.IsNullOrWhiteSpace(vehicleNo))
        {
            return null;
        }

        var previous = await _db.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == current.StationCode
                && x.Id != current.Id
                && !x.IsDeleted
                && !x.IsCancelled
                && !x.IsNoLoad
                && !x.IsReturnedBrokenTrip
                && x.TransactionType == TransactionType.INBOUND
                && x.SessionStatus == WeighingSessionStatus.COMPLETED
                && x.Weight2Time.HasValue
                && x.Weight2Time.Value < current.CompletedAt
                && (x.NetWeight ?? 0m) > 0m
                && ((x.InternalVehicleNo ?? x.VehiclePlate) == vehicleNo))
            .OrderByDescending(x => x.Weight2Time)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.SessionNo,
                x.InternalVehicleNo,
                x.VehiclePlate,
                x.Weight2Time,
                x.NetWeight
            })
            .FirstOrDefaultAsync(ct);

        if (previous == null || !previous.NetWeight.HasValue)
        {
            return null;
        }

        return new ReturnedBrokenTripPreviousTripInfo(
            previous.Id,
            null,
            BusinessNumberFormatter.ToDisplay(previous.SessionNo),
            previous.InternalVehicleNo ?? previous.VehiclePlate,
            previous.Weight2Time,
            previous.NetWeight.Value);
    }

    public async Task<ReturnedBrokenTripPreviousTripInfo?> GetPreviousClayTripForReturnedAsync(Guid sessionLineId, CancellationToken ct)
    {
        var current = await (
            from line in _db.WeighingSessionLines.AsNoTracking()
            join session in _db.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            where line.Id == sessionLineId
                && !line.IsDeleted
                && !session.IsDeleted
                && !session.IsCancelled
            select new
            {
                LineId = line.Id,
                line.CutOrderId,
                session.StationCode,
                session.VehiclePlate,
                session.InternalVehicleNo,
                CompletedAt = session.Weight2Time ?? session.UpdatedAt ?? session.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (current == null)
        {
            return null;
        }

        var vehicleNo = string.IsNullOrWhiteSpace(current.InternalVehicleNo)
            ? current.VehiclePlate
            : current.InternalVehicleNo;
        if (string.IsNullOrWhiteSpace(vehicleNo))
        {
            return null;
        }

        var previous = await (
            from line in _db.WeighingSessionLines.AsNoTracking()
            join session in _db.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            where line.CutOrderId == current.CutOrderId
                && line.Id != current.LineId
                && !line.IsDeleted
                && !line.IsReturnedBrokenTrip
                && (line.ActualAllocatedWeight ?? 0m) > 0m
                && session.StationCode == current.StationCode
                && !session.IsDeleted
                && !session.IsCancelled
                && !session.IsNoLoad
                && session.SessionStatus == WeighingSessionStatus.COMPLETED
                && session.Weight2Time.HasValue
                && session.Weight2Time.Value < current.CompletedAt
                && ((session.InternalVehicleNo ?? session.VehiclePlate) == vehicleNo)
            orderby session.Weight2Time descending, session.UpdatedAt ?? session.CreatedAt descending
            select new
            {
                LineId = line.Id,
                SessionId = session.Id,
                session.SessionNo,
                session.InternalVehicleNo,
                session.VehiclePlate,
                session.Weight2Time,
                line.ActualAllocatedWeight
            })
            .FirstOrDefaultAsync(ct);

        if (previous == null || !previous.ActualAllocatedWeight.HasValue)
        {
            return null;
        }

        return new ReturnedBrokenTripPreviousTripInfo(
            previous.SessionId,
            previous.LineId,
            BusinessNumberFormatter.ToDisplay(previous.SessionNo),
            previous.InternalVehicleNo ?? previous.VehiclePlate,
            previous.Weight2Time,
            previous.ActualAllocatedWeight.Value);
    }

    private async Task<Dictionary<Guid, WeighTicket>> LoadPrimaryTicketBySessionIdAsync(
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken ct)
    {
        if (sessionIds.Count == 0)
        {
            return new Dictionary<Guid, WeighTicket>();
        }

        var tickets = await _db.WeighTickets.AsNoTracking()
            .Where(x => x.WeighingSessionId.HasValue
                && sessionIds.Contains(x.WeighingSessionId.Value)
                && !x.IsDeleted)
            .OrderBy(x => x.RecordRole == WeighTicketRecordRoles.MasterSession ? 0 : 1)
            .ThenByDescending(x => x.IsPrimaryDisplay)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(ct);

        return tickets
            .GroupBy(x => x.WeighingSessionId!.Value)
            .ToDictionary(x => x.Key, x => x.First());
    }

    private async Task<Dictionary<string, string>> LoadUserDisplayNameByUsernameAsync(
        IEnumerable<string?> usernames,
        CancellationToken ct)
    {
        var normalized = usernames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var users = await _db.Users.AsNoTracking()
            .Where(x => normalized.Contains(x.Username))
            .Select(x => new { x.Username, x.DisplayName })
            .ToListAsync(ct);

        return users
            .GroupBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => string.IsNullOrWhiteSpace(x.First().DisplayName) ? x.Key : x.First().DisplayName,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveUserDisplayName(IReadOnlyDictionary<string, string> userDisplayByUsername, string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalized = username.Trim();
        return userDisplayByUsername.TryGetValue(normalized, out var displayName)
            ? displayName
            : normalized;
    }

    public async Task<IReadOnlyList<OutgoingSessionListItem>> SearchCompletedSessionsAsync(string? keyword, DateTime? completedDate, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var query = _db.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && !x.IsCancelled && x.SessionStatus == WeighingSessionStatus.COMPLETED);

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
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && sessionIds.Contains(x.WeighingSessionId))
            .ToListAsync(ct);
        var registrations = await _db.CutOrders.AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.WeighingSessionId.HasValue && sessionIds.Contains(x.WeighingSessionId.Value))
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
                BusinessNumberFormatter.ToDisplay(session.SessionNo),
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

    public async Task<int> CountCompletedStandardTareSessionsForVehicleOnDateAsync(Guid vehicleId, DateTime date, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var start = date.Date;
        var end = start.AddDays(1);

        return await _db.WeighingSessions.AsNoTracking()
            .CountAsync(x => x.StationCode == stationCode
                && !x.IsDeleted
                && !x.IsCancelled
                && x.StandardTareVehicleId == vehicleId
                && x.SessionStatus == WeighingSessionStatus.COMPLETED
                && x.Weight2.HasValue
                && (x.Weight2Time ?? x.UpdatedAt ?? x.CreatedAt) >= start
                && (x.Weight2Time ?? x.UpdatedAt ?? x.CreatedAt) < end,
                ct);
    }

    public async Task AddLineAsync(WeighingSessionLine line, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(line.StationCode))
        {
            var session = await _db.WeighingSessions.AsNoTracking()
                .Where(x => x.Id == line.WeighingSessionId)
                .Select(x => x.StationCode)
                .FirstOrDefaultAsync(ct);
            line.StationCode = string.IsNullOrWhiteSpace(session)
                ? await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct)
                : session;
        }

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
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        return await _db.WeighingSessionLines
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && x.SyncStatus == syncStatus)
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
                line.BagCountDisplay,
                line.LineStatus,
                line.HasPrintedDeliveryTicket,
                reg.ProductType,
                reg.Notes,
                reg.IsPortTransfer
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
