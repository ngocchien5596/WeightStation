using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Infrastructure.Persistence;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Infrastructure.Repositories;

public class CutOrderRepository : ICutOrderRepository
{
    private readonly StationDbContext _db;
    private static readonly TimeSpan ReuseWeight1Window = TimeSpan.FromHours(24);

    public CutOrderRepository(StationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(CutOrder registration, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(registration.StationCode))
        {
            registration.StationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        }

        SyncTrackedEntityUpdateHelper.PrepareForAdd(registration);
        await _db.CutOrders.AddAsync(registration, ct);
    }

    public Task UpdateAsync(CutOrder registration, CancellationToken ct)
    {
        SyncTrackedEntityUpdateHelper.PrepareForUpdate(_db, registration);
        if (_db.Entry(registration).State == EntityState.Detached)
        {
            _db.CutOrders.Update(registration);
        }
        return Task.CompletedTask;
    }

    public async Task<CutOrder?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.CutOrders.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<CutOrder>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<CutOrder>();
        }

        return await _db.CutOrders
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
    }

    public async Task<CutOrder?> GetByErpIdAsync(string erpCutOrderId, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        return await _db.CutOrders
            .FirstOrDefaultAsync(v => v.StationCode == stationCode && !v.IsDeleted && v.ErpCutOrderId == erpCutOrderId, ct);
    }

    public async Task<IReadOnlyList<CutOrder>> GetLatestDeletedByErpIdsAsync(IReadOnlyCollection<string> erpCutOrderIds, CancellationToken ct)
    {
        if (erpCutOrderIds.Count == 0)
        {
            return Array.Empty<CutOrder>();
        }

        var normalized = erpCutOrderIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return Array.Empty<CutOrder>();
        }

        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var deletedRows = await _db.CutOrders
            .Where(x => x.IsDeleted
                && x.StationCode == stationCode
                && x.ErpCutOrderId != null
                && normalized.Contains(x.ErpCutOrderId)
                && x.CarryForwardWeight1.HasValue)
            .OrderByDescending(x => x.DeletedAt ?? x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(ct);

        return deletedRows
            .GroupBy(x => x.ErpCutOrderId!, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<CutOrder>> GetLatestDeletedByRegistrationCodesAsync(IReadOnlyCollection<string> erpRegistrationCodes, CancellationToken ct)
    {
        if (erpRegistrationCodes.Count == 0)
        {
            return Array.Empty<CutOrder>();
        }

        var normalized = erpRegistrationCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return Array.Empty<CutOrder>();
        }

        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var deletedRows = await _db.CutOrders
            .Where(x => x.IsDeleted
                && x.StationCode == stationCode
                && x.ErpRegistrationCode != null
                && normalized.Contains(x.ErpRegistrationCode))
            .OrderByDescending(x => x.DeletedAt ?? x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(ct);

        return deletedRows
            .GroupBy(x => x.ErpRegistrationCode!, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<CutOrder>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var lineCutOrderIds = await _db.WeighingSessionLines.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && x.WeighingSessionId == weighingSessionId)
            .OrderBy(x => x.SequenceNo)
            .Select(x => new { x.CutOrderId, x.SequenceNo })
            .ToListAsync(ct);

        var lineOrder = lineCutOrderIds
            .GroupBy(x => x.CutOrderId)
            .ToDictionary(x => x.Key, x => x.Min(item => item.SequenceNo));
        var ids = lineOrder.Keys.ToList();

        var registrations = await _db.CutOrders
            .Where(x => x.StationCode == stationCode
                && !x.IsDeleted
                && (x.WeighingSessionId == weighingSessionId || ids.Contains(x.Id)))
            .ToListAsync(ct);

        return registrations
            .OrderBy(x => lineOrder.TryGetValue(x.Id, out var sequence) ? sequence : int.MaxValue)
            .ThenBy(x => x.CreatedAt)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<CutOrder>> GetBySyncStatusAsync(SyncStatus syncStatus, int take, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        return await _db.CutOrders
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && x.SyncStatus == syncStatus)
            .OrderBy(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CutOrder>> SearchAsync(string? keyword, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var query = _db.CutOrders.Where(v => v.StationCode == stationCode);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(v => !v.IsDeleted && (v.VehiclePlate.Contains(keyword) || 
                                     (v.ErpCutOrderId != null && v.ErpCutOrderId.Contains(keyword))));
        }
        else
        {
            query = query.Where(v => !v.IsDeleted);
        }
        return await query.OrderByDescending(v => v.CreatedAt).Take(100).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CutOrder>> GetUnprocessedInboundAsync(CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        return await _db.CutOrders
            .Where(v => v.StationCode == stationCode && !v.IsDeleted && !v.IsInboundProcessed && v.CutOrderSource == CutOrderSource.ERP)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeightViewListItem>> GetWeightViewListAsync(string? keyword, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var regQuery = _db.CutOrders.AsNoTracking()
            .Where(vr => vr.StationCode == stationCode && !vr.IsDeleted && vr.ProcessingStage == ProcessingStage.WEIGHING && !vr.IsCancelled);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            regQuery = regQuery.Where(vr => vr.VehiclePlate.Contains(keyword) || 
                                           (vr.ErpCutOrderId != null && vr.ErpCutOrderId.Contains(keyword)));
        }

        var registrations = await regQuery.OrderByDescending(vr => vr.CreatedAt).Take(100).ToListAsync(ct);
        var regIds = registrations.Select(x => x.Id).ToList();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var matchedByTicketNo = await _db.WeighTickets.AsNoTracking()
                .Where(wt => wt.StationCode == stationCode && !wt.IsDeleted && wt.TicketNo != null && wt.TicketNo.Contains(keyword))
                .Select(wt => wt.CutOrderId)
                .ToListAsync(ct);

            var extraRegIds = matchedByTicketNo.Where(id => !regIds.Contains(id)).Distinct().ToList();
            if (extraRegIds.Any())
            {
                var extraRegs = await _db.CutOrders.AsNoTracking()
                    .Where(vr => vr.StationCode == stationCode && extraRegIds.Contains(vr.Id))
                    .ToListAsync(ct);

                registrations.AddRange(extraRegs);
                regIds = registrations.Select(x => x.Id).ToList();
            }
        }

        var weighTickets = await _db.WeighTickets.AsNoTracking()
            .Where(wt => wt.StationCode == stationCode && regIds.Contains(wt.CutOrderId) && !wt.IsDeleted)
            .ToListAsync(ct);

        var deliveryTickets = await _db.DeliveryTickets.AsNoTracking()
            .Where(dt => dt.StationCode == stationCode && regIds.Contains(dt.CutOrderId) && !dt.IsDeleted)
            .ToListAsync(ct);

        var sessionIds = registrations
            .Where(vr => vr.WeighingSessionId.HasValue)
            .Select(vr => vr.WeighingSessionId!.Value)
            .Distinct()
            .ToList();
        var sessionById = sessionIds.Count == 0
            ? new Dictionary<Guid, WeighingSession>()
            : await _db.WeighingSessions.AsNoTracking()
                .Where(session => session.StationCode == stationCode && !session.IsDeleted && sessionIds.Contains(session.Id))
                .ToDictionaryAsync(session => session.Id, ct);

        var result = new List<WeightViewListItem>();

        foreach (var vr in registrations)
        {
            var relatedWeighTickets = weighTickets.Where(wt => wt.CutOrderId == vr.Id).ToList();
            var primaryWeighTicket = ResolvePrimaryWeighTicket(vr, relatedWeighTickets);
            var relatedDeliveryTickets = deliveryTickets.Where(dt => dt.CutOrderId == vr.Id).ToList();
            var primaryDeliveryTicket = ResolvePrimaryDeliveryTicket(vr, relatedDeliveryTickets, primaryWeighTicket);
            sessionById.TryGetValue(vr.WeighingSessionId ?? Guid.Empty, out var session);

            result.Add(new WeightViewListItem(
                vr.Id,
                primaryWeighTicket is null ? null : BusinessNumberFormatter.ToDisplay(primaryWeighTicket.TicketNo),
                vr.ErpCutOrderId,
                vr.VehiclePlate,
                vr.CustomerName,
                vr.ProductName,
                vr.CutOrderStatus,
                session?.Weight1 ?? primaryWeighTicket?.Weight1,
                session?.Weight2 ?? primaryWeighTicket?.Weight2,
                vr.BagCount,
                vr.PlannedWeight,
                session?.NetWeight ?? primaryWeighTicket?.NetWeight,
                session?.Weight2Time ?? session?.Weight1Time ?? primaryWeighTicket?.Weight2Time ?? primaryWeighTicket?.Weight1Time ?? NormalizeCreatedAtForDisplay(vr.CutOrderSource, vr.CreatedAt),
                primaryWeighTicket?.Weight2User ?? primaryWeighTicket?.Weight1User,
                primaryDeliveryTicket is null ? null : BusinessNumberFormatter.ToDisplay(primaryDeliveryTicket.DeliveryNo),
                vr.Notes,
                vr.HasOverweightCase,
                vr.TransactionType,
                vr.TransportMethod
            ));
        }

        return result.OrderByDescending(x => x.WeighDate).ToList().AsReadOnly();
    }

    private static WeighTicket? ResolvePrimaryWeighTicket(CutOrder registration, IReadOnlyCollection<WeighTicket> weighTickets)
    {
        var workingTickets = weighTickets
            .Where(wt => string.Equals(wt.RecordRole, "WORKING", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (workingTickets.Count == 0)
        {
            workingTickets = weighTickets.ToList();
        }

        var splitTickets = workingTickets.Where(wt => wt.SplitSequence.HasValue).ToList();
        if (splitTickets.Count > 0)
        {
            return splitTickets
                .OrderBy(wt => wt.SplitSequence ?? byte.MaxValue)
                .ThenBy(wt => wt.IsOverWeight ? 1 : 0)
                .ThenByDescending(wt => wt.UpdatedAt ?? wt.CreatedAt)
                .FirstOrDefault();
        }

        if (registration.CurrentPrimaryWeighTicketId.HasValue)
        {
            var currentPrimary = workingTickets.FirstOrDefault(wt => wt.Id == registration.CurrentPrimaryWeighTicketId.Value);
            if (currentPrimary != null)
            {
                return currentPrimary;
            }
        }

        return workingTickets
            .Where(wt => wt.IsPrimaryDisplay)
            .OrderBy(wt => wt.SplitSequence ?? 0)
            .ThenByDescending(wt => wt.UpdatedAt ?? wt.CreatedAt)
            .FirstOrDefault()
            ?? workingTickets
                .OrderBy(wt => wt.SplitSequence ?? 0)
                .ThenByDescending(wt => wt.UpdatedAt ?? wt.CreatedAt)
                .FirstOrDefault();
    }

    private static DeliveryTicket? ResolvePrimaryDeliveryTicket(
        CutOrder registration,
        IReadOnlyCollection<DeliveryTicket> deliveryTickets,
        WeighTicket? primaryWeighTicket)
    {
        if (registration.CurrentPrimaryDeliveryTicketId.HasValue)
        {
            var currentPrimary = deliveryTickets.FirstOrDefault(dt => dt.Id == registration.CurrentPrimaryDeliveryTicketId.Value);
            if (currentPrimary != null)
            {
                return currentPrimary;
            }
        }

        if (primaryWeighTicket?.SplitSequence != null)
        {
            var matchedBySplit = deliveryTickets.FirstOrDefault(dt => dt.SplitSequence == primaryWeighTicket.SplitSequence);
            if (matchedBySplit != null)
            {
                return matchedBySplit;
            }
        }

        return deliveryTickets
            .OrderBy(dt => dt.SplitSequence ?? 0)
            .ThenByDescending(dt => dt.UpdatedAt ?? dt.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<IncomingVehicleListItem>> GetIncomingListAsync(IncomingVehicleListFilter filter, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var reuseCutoff = DateTime.Now.Subtract(ReuseWeight1Window);
        var query = _db.CutOrders.AsNoTracking()
            .Where(vr => vr.StationCode == stationCode && !vr.IsDeleted && vr.ProcessingStage == ProcessingStage.IN_YARD && !vr.IsCancelled);

        if (!string.IsNullOrWhiteSpace(filter.ErpCutOrderId))
            query = query.Where(vr => vr.ErpCutOrderId != null && vr.ErpCutOrderId.Contains(filter.ErpCutOrderId));

        if (!string.IsNullOrWhiteSpace(filter.VehiclePlate))
            query = query.Where(vr => vr.VehiclePlate.Contains(filter.VehiclePlate));

        if (!string.IsNullOrWhiteSpace(filter.MoocNumber))
            query = query.Where(vr => vr.MoocNumber != null && vr.MoocNumber.Contains(filter.MoocNumber));

        if (!string.IsNullOrWhiteSpace(filter.ReceiverName))
            query = query.Where(vr => vr.ReceiverName != null && vr.ReceiverName.Contains(filter.ReceiverName));

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            query = query.Where(vr => vr.CustomerName != null && vr.CustomerName.Contains(filter.CustomerName));

        if (!string.IsNullOrWhiteSpace(filter.ProductCode))
            query = query.Where(vr => vr.ProductCode != null && vr.ProductCode.Contains(filter.ProductCode));

        if (!string.IsNullOrWhiteSpace(filter.ProductName))
            query = query.Where(vr => vr.ProductName != null && vr.ProductName.Contains(filter.ProductName));

        var registrations = await query.OrderByDescending(vr => vr.CreatedAt).Take(200).ToListAsync(ct);

        var carryForwardHistoryByRegistrationCode = registrations
            .Where(x => !string.IsNullOrWhiteSpace(x.ErpRegistrationCode))
            .Select(x => x.ErpRegistrationCode!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var deletedCarryForwardByRegistrationCode = carryForwardHistoryByRegistrationCode.Count == 0
            ? new Dictionary<string, CutOrder>(StringComparer.OrdinalIgnoreCase)
            : (await _db.CutOrders.AsNoTracking()
                    .Where(x => x.IsDeleted
                        && x.StationCode == stationCode
                        && x.ErpRegistrationCode != null
                        && carryForwardHistoryByRegistrationCode.Contains(x.ErpRegistrationCode)
                        && x.CarryForwardWeight1.HasValue
                        && x.CarryForwardWeight1Time.HasValue
                        && x.CarryForwardWeight1Time.Value >= reuseCutoff)
                    .OrderByDescending(x => x.DeletedAt ?? x.UpdatedAt ?? x.CreatedAt)
                    .ToListAsync(ct))
                .GroupBy(x => x.ErpRegistrationCode!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var carryForwardHistoryByErpId = registrations
            .Where(x => !string.IsNullOrWhiteSpace(x.ErpCutOrderId))
            .Select(x => x.ErpCutOrderId!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var deletedCarryForwardLookup = carryForwardHistoryByErpId.Count == 0
            ? new Dictionary<string, CutOrder>(StringComparer.OrdinalIgnoreCase)
            : (await _db.CutOrders.AsNoTracking()
                    .Where(x => x.IsDeleted
                        && x.StationCode == stationCode
                        && x.ErpCutOrderId != null
                        && carryForwardHistoryByErpId.Contains(x.ErpCutOrderId)
                        && x.CarryForwardWeight1.HasValue
                        && x.CarryForwardWeight1Time.HasValue
                        && x.CarryForwardWeight1Time.Value >= reuseCutoff)
                    .OrderByDescending(x => x.DeletedAt ?? x.UpdatedAt ?? x.CreatedAt)
                    .ToListAsync(ct))
                .GroupBy(x => x.ErpCutOrderId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var deletedSessionIds = deletedCarryForwardByRegistrationCode.Values
            .Where(x => x.WeighingSessionId.HasValue)
            .Select(x => x.WeighingSessionId!.Value)
            .Distinct()
            .ToList();
        var deletedSessionLookup = deletedSessionIds.Count == 0
            ? new Dictionary<Guid, WeighingSession>()
            : await _db.WeighingSessions.AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.StationCode == stationCode
                    && !x.IsCancelled
                    && x.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2
                    && x.Weight1.HasValue
                    && !x.Weight2.HasValue
                    && x.Weight1Time.HasValue
                    && x.Weight1Time.Value >= reuseCutoff
                    && deletedSessionIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        var list = registrations.Select(vr =>
        {
            string? suggestedSessionNo = null;
            if (!string.IsNullOrWhiteSpace(vr.ErpRegistrationCode)
                && deletedCarryForwardByRegistrationCode.TryGetValue(vr.ErpRegistrationCode.Trim(), out var deletedByRegCode)
                && deletedByRegCode.WeighingSessionId.HasValue
                && deletedSessionLookup.TryGetValue(deletedByRegCode.WeighingSessionId.Value, out var suggestedSession)
                && suggestedSession.TransactionType == vr.TransactionType)
            {
                suggestedSessionNo = suggestedSession.SessionNo;
            }

            return new IncomingVehicleListItem(
                vr.Id,
                vr.ErpCutOrderId,
                vr.ErpRegistrationCode,
                vr.TransactionType,
                vr.VehiclePlate ?? string.Empty,
                vr.MoocNumber,
                vr.ReceiverName,
                vr.CustomerName,
                vr.ProductCode,
                vr.ProductName,
                vr.PlannedWeight,
                vr.BagCount,
                vr.CutOrderStatus,
                vr.TransportMethod,
                NormalizeCreatedAtForDisplay(vr.CutOrderSource, vr.CreatedAt),
                vr.ProductType,
                vr.CarryForwardWeight1
                    ?? (!string.IsNullOrWhiteSpace(vr.ErpRegistrationCode) && deletedCarryForwardByRegistrationCode.TryGetValue(vr.ErpRegistrationCode.Trim(), out var deletedCarryForwardByRegCode)
                        ? deletedCarryForwardByRegCode.CarryForwardWeight1
                        : !string.IsNullOrWhiteSpace(vr.ErpCutOrderId) && deletedCarryForwardLookup.TryGetValue(vr.ErpCutOrderId.Trim(), out var deletedCarryForward)
                        ? deletedCarryForward.CarryForwardWeight1
                        : null),
                vr.CarryForwardWeight1Time
                    ?? (!string.IsNullOrWhiteSpace(vr.ErpRegistrationCode) && deletedCarryForwardByRegistrationCode.TryGetValue(vr.ErpRegistrationCode.Trim(), out var deletedCarryForwardTimeByRegCode)
                        ? deletedCarryForwardTimeByRegCode.CarryForwardWeight1Time
                        : !string.IsNullOrWhiteSpace(vr.ErpCutOrderId) && deletedCarryForwardLookup.TryGetValue(vr.ErpCutOrderId.Trim(), out var deletedCarryForwardTime)
                        ? deletedCarryForwardTime.CarryForwardWeight1Time
                        : null),
                string.IsNullOrWhiteSpace(suggestedSessionNo) ? suggestedSessionNo : BusinessNumberFormatter.ToDisplay(suggestedSessionNo),
                vr.ConsumptionPlace,
                vr.Market,
                vr.IsExportScale);
        }).ToList();

        var missingProductCodes = list
            .Where(x => string.IsNullOrWhiteSpace(x.ProductType) && !string.IsNullOrWhiteSpace(x.ProductCode))
            .Select(x => x.ProductCode!)
            .Distinct()
            .ToList();

        if (missingProductCodes.Count > 0)
        {
            var products = await _db.Products.AsNoTracking()
                .Where(x => missingProductCodes.Contains(x.ProductCode))
                .ToDictionaryAsync(x => x.ProductCode.Trim(), x => x.ProductType, ct);

            return list.Select(item =>
            {
                if (string.IsNullOrWhiteSpace(item.ProductType) && !string.IsNullOrWhiteSpace(item.ProductCode) && products.TryGetValue(item.ProductCode.Trim(), out var productType))
                {
                    return item with { ProductType = productType };
                }
                return item;
            }).ToList().AsReadOnly();
        }

        return list.AsReadOnly();
    }

    private static DateTime NormalizeCreatedAtForDisplay(CutOrderSource source, DateTime createdAt)
        => createdAt;

    public async Task<IReadOnlyList<OutgoingVehicleListItem>> GetOutgoingListAsync(OutgoingVehicleListFilter filter, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var query =
            from vr in _db.CutOrders.AsNoTracking()
            join ws in _db.WeighingSessions.AsNoTracking()
                on vr.WeighingSessionId equals ws.Id into sessionGroup
            from session in sessionGroup.Where(x => !x.IsDeleted).DefaultIfEmpty()
            where vr.StationCode == stationCode
                && vr.ProcessingStage == ProcessingStage.OUT_YARD
                && vr.CutOrderStatus == CutOrderStatus.COMPLETED
                && !vr.IsExportScale
                && !vr.IsCancelled
            select new
            {
                Registration = vr,
                Session = session
            };
        query = query.Where(x => !x.Registration.IsDeleted);

        if (filter.FlowType == OutgoingFlowType.Domestic)
        {
            query = query.Where(x => !x.Registration.IsExportScale);
        }
        else if (filter.FlowType == OutgoingFlowType.Export)
        {
            query = query.Where(x => false);
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionNo))
        {
            query = query.Where(x => x.Session != null && x.Session.SessionNo.Contains(filter.SessionNo));
        }

        if (!string.IsNullOrWhiteSpace(filter.ErpCutOrderId))
            query = query.Where(x => x.Registration.ErpCutOrderId != null && x.Registration.ErpCutOrderId.Contains(filter.ErpCutOrderId));

        if (!string.IsNullOrWhiteSpace(filter.VehiclePlate))
            query = query.Where(x => x.Registration.VehiclePlate.Contains(filter.VehiclePlate));

        if (!string.IsNullOrWhiteSpace(filter.MoocNumber))
            query = query.Where(x => x.Registration.MoocNumber != null && x.Registration.MoocNumber.Contains(filter.MoocNumber));

        if (!string.IsNullOrWhiteSpace(filter.ReceiverName))
            query = query.Where(x => x.Registration.ReceiverName != null && x.Registration.ReceiverName.Contains(filter.ReceiverName));

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            query = query.Where(x => x.Registration.CustomerName != null && x.Registration.CustomerName.Contains(filter.CustomerName));

        if (filter.CompletedDate.HasValue)
        {
            var start = filter.CompletedDate.Value.Date;
            var end = start.AddDays(1);
            query = query.Where(x =>
                x.Session != null
                    ? ((x.Session.Weight2Time ?? x.Session.Weight1Time ?? x.Session.CreatedAt) >= start
                        && (x.Session.Weight2Time ?? x.Session.Weight1Time ?? x.Session.CreatedAt) < end)
                    : ((x.Registration.UpdatedAt ?? x.Registration.CreatedAt) >= start
                        && (x.Registration.UpdatedAt ?? x.Registration.CreatedAt) < end));
        }

        var registrations = await query
            .OrderByDescending(x => x.Session != null
                ? (x.Session.Weight2Time ?? x.Session.Weight1Time ?? x.Session.CreatedAt)
                : (x.Registration.UpdatedAt ?? x.Registration.CreatedAt))
            .Take(200)
            .ToListAsync(ct);

        var regIds = registrations.Select(x => x.Registration.Id).ToList();
        var sessionIds = registrations
            .Where(x => x.Session != null)
            .Select(x => x.Session!.Id)
            .Distinct()
            .ToList();
        var missingProductCodes = registrations
            .Where(x => string.IsNullOrWhiteSpace(x.Registration.ProductType) && !string.IsNullOrWhiteSpace(x.Registration.ProductCode))
            .Select(x => x.Registration.ProductCode!.Trim())
            .Distinct()
            .ToList();
        var productTypeLookup = missingProductCodes.Count == 0
            ? new Dictionary<string, string?>()
            : await _db.Products.AsNoTracking()
                .Where(x => missingProductCodes.Contains(x.ProductCode))
                .ToDictionaryAsync(x => x.ProductCode.Trim(), x => x.ProductType, ct);

        var sessionLines = await _db.WeighingSessionLines.AsNoTracking()
            .Where(line => line.StationCode == stationCode && !line.IsDeleted && sessionIds.Contains(line.WeighingSessionId))
            .ToListAsync(ct);

        var sessionMasterWeighTickets = await _db.WeighTickets.AsNoTracking()
            .Where(wt => wt.WeighingSessionId.HasValue
                && wt.StationCode == stationCode
                && sessionIds.Contains(wt.WeighingSessionId.Value)
                && !wt.IsDeleted
                && wt.RecordRole == WeighTicketRecordRoles.MasterSession)
            .ToListAsync(ct);

        var splitWeighTickets = await _db.WeighTickets.AsNoTracking()
            .Where(wt => wt.WeighingSessionId.HasValue
                && wt.StationCode == stationCode
                && sessionIds.Contains(wt.WeighingSessionId.Value)
                && !wt.IsDeleted
                && wt.RecordRole == WeighTicketRecordRoles.SplitDerived)
            .ToListAsync(ct);

        var normalDeliveryTickets = await _db.DeliveryTickets.AsNoTracking()
            .Where(dt => dt.StationCode == stationCode
                && regIds.Contains(dt.CutOrderId)
                && !dt.IsDeleted
                && dt.RecordRole == DeliveryTicketRecordRoles.Normal)
            .ToListAsync(ct);

        var splitDeliveryTickets = await _db.DeliveryTickets.AsNoTracking()
            .Where(dt => dt.StationCode == stationCode
                && regIds.Contains(dt.CutOrderId)
                && !dt.IsDeleted
                && dt.RecordRole == DeliveryTicketRecordRoles.SplitDerived)
            .ToListAsync(ct);

        var normalItems = registrations.Select(item =>
        {
            var vr = item.Registration;
            var session = item.Session;
            var resolvedProductType = ProductTypes.Normalize(vr.ProductType);
            if (resolvedProductType == null
                && !string.IsNullOrWhiteSpace(vr.ProductCode)
                && productTypeLookup.TryGetValue(vr.ProductCode.Trim(), out var fallbackProductType))
            {
                resolvedProductType = ProductTypes.Normalize(fallbackProductType);
            }

            var isBagged = string.Equals(resolvedProductType, ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
            var sessionLine = session == null
                ? null
                : sessionLines.FirstOrDefault(line => line.WeighingSessionId == session.Id && line.CutOrderId == vr.Id);
            var sessionMasterWeigh = session == null
                ? null
                : sessionMasterWeighTickets
                    .Where(wt => wt.WeighingSessionId == session.Id)
                    .OrderByDescending(wt => wt.UpdatedAt ?? wt.CreatedAt)
                    .FirstOrDefault();
            var relatedNormalDelivery = normalDeliveryTickets
                .Where(dt => dt.CutOrderId == vr.Id)
                .OrderByDescending(dt => dt.UpdatedAt ?? dt.CreatedAt)
                .ToList();
            var relatedSplitDelivery = splitDeliveryTickets
                .Where(dt => dt.CutOrderId == vr.Id)
                .OrderByDescending(dt => dt.AllocatedWeight ?? 0m)
                .ThenBy(dt => dt.SplitSequence ?? byte.MaxValue)
                .ThenByDescending(dt => dt.UpdatedAt ?? dt.CreatedAt)
                .ToList();
            var displayDelivery = relatedSplitDelivery.FirstOrDefault() ?? relatedNormalDelivery.FirstOrDefault();
            var displayWeigh = displayDelivery?.RecordRole == DeliveryTicketRecordRoles.SplitDerived
                ? splitWeighTickets.FirstOrDefault(wt => wt.SplitGroupId.HasValue
                    && wt.SplitGroupId == displayDelivery.SplitGroupId
                    && wt.WeighingSessionId == displayDelivery.WeighingSessionId)
                : sessionMasterWeigh;
            var totalBagCount = isBagged
                ? (session == null
                    ? sessionLine?.ActualAllocatedBagCount
                    : sessionLines
                        .Where(line => line.WeighingSessionId == session.Id)
                        .Sum(line => line.ActualAllocatedBagCount ?? 0))
                : null;
            var hasSplitOverweight = relatedSplitDelivery.Count > 0;
            var isNoLoad = session != null
                && (session.IsNoLoad
                    || (session.SessionStatus == WeighingSessionStatus.COMPLETED
                        && (session.NetWeight ?? 0m) <= 0m
                        && !relatedNormalDelivery.Any()
                        && !relatedSplitDelivery.Any()
                        && sessionLines
                            .Where(line => line.WeighingSessionId == session.Id)
                            .All(line => (line.ActualAllocatedWeight ?? 0m) <= 0m && (line.ActualAllocatedBagCount ?? 0) <= 0)));
            var displayWeightKg = displayDelivery?.AllocatedWeight
                ?? sessionLine?.ActualAllocatedWeight;
            var displayBagCount = isBagged
                ? (displayDelivery?.AllocatedBagCount ?? sessionLine?.ActualAllocatedBagCount)
                : null;
            var totalWeightKg = session?.NetWeight
                ?? sessionLine?.ActualAllocatedWeight;
            var weighDate = displayWeigh?.Weight2Time
                ?? displayWeigh?.Weight1Time
                ?? ResolveOutgoingCompletedAt(vr, session);
            var weighUser = displayWeigh?.Weight2User
                ?? displayWeigh?.Weight1User;

            return new OutgoingVehicleListItem(
                vr.Id,
                vr.WeighingSessionId,
                session is null ? null : BusinessNumberFormatter.ToDisplay(session.SessionNo),
                vr.ErpCutOrderId,
                vr.TransactionType,
                vr.VehiclePlate,
                vr.MoocNumber,
                vr.TransportMethod,
                vr.ReceiverName,
                vr.CustomerName,
                vr.ProductCode,
                vr.ProductName,
                resolvedProductType,
                vr.ReceiverName ?? session?.DriverName,
                vr.PlannedWeight,
                isBagged ? vr.BagCount : null,
                displayWeigh?.Weight1 ?? session?.Weight1,
                displayWeightKg,
                displayBagCount,
                totalWeightKg,
                totalBagCount,
                weighDate,
                weighUser,
                displayWeigh is null ? null : BusinessNumberFormatter.ToDisplay(displayWeigh.TicketNo),
                displayDelivery is null ? null : BusinessNumberFormatter.ToDisplay(displayDelivery.DeliveryNo),
                ResolveOutgoingCompletedAt(vr, session),
                displayWeigh?.IsPrinted ?? session?.HasPrintedMasterWeighTicket == true,
                displayDelivery?.IsPrinted == true,
                session?.UseActualWeightForBaggedCutOrders == true,
                vr.ErpExportCompleted,
                isNoLoad,
                hasSplitOverweight
            );
        }).ToList();

        var exportItems = filter.FlowType == OutgoingFlowType.Domestic
            ? []
            : await GetExportScaleOutgoingItemsAsync(filter, ct);

        return normalItems
            .Concat(exportItems)
            .OrderByDescending(x => x.CompletedAt ?? x.WeighDate ?? DateTime.MinValue)
            .Take(200)
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyList<OutgoingVehicleListItem>> GetExportScaleOutgoingItemsAsync(OutgoingVehicleListFilter filter, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var query =
            from line in _db.WeighingSessionLines.AsNoTracking()
            join vr in _db.CutOrders.AsNoTracking()
                on line.CutOrderId equals vr.Id
            join session in _db.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            where vr.StationCode == stationCode
                && line.StationCode == stationCode
                && session.StationCode == stationCode
                && vr.IsExportScale
                && !vr.IsDeleted
                && !vr.IsCancelled
                && !line.IsDeleted
                && !session.IsDeleted
                && !session.IsCancelled
                && session.Weight2.HasValue
                && session.NetWeight.HasValue
                && (session.SessionStatus == WeighingSessionStatus.READY_TO_COMPLETE
                    || session.SessionStatus == WeighingSessionStatus.COMPLETED)
            select new
            {
                Registration = vr,
                Session = session,
                Line = line
            };

        if (filter.FlowType == OutgoingFlowType.Export)
        {
            // Keep export query active.
        }
        else if (filter.FlowType == OutgoingFlowType.Domestic)
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionNo))
        {
            query = query.Where(x => x.Session.SessionNo.Contains(filter.SessionNo));
        }

        if (!string.IsNullOrWhiteSpace(filter.ErpCutOrderId))
        {
            query = query.Where(x => x.Registration.ErpCutOrderId != null && x.Registration.ErpCutOrderId.Contains(filter.ErpCutOrderId));
        }

        if (!string.IsNullOrWhiteSpace(filter.VehiclePlate))
        {
            query = query.Where(x => x.Session.VehiclePlate.Contains(filter.VehiclePlate));
        }

        if (!string.IsNullOrWhiteSpace(filter.MoocNumber))
        {
            query = query.Where(x => x.Session.MoocNumber != null && x.Session.MoocNumber.Contains(filter.MoocNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.ReceiverName))
        {
            query = query.Where(x =>
                (x.Session.DriverName != null && x.Session.DriverName.Contains(filter.ReceiverName)) ||
                (x.Registration.ReceiverName != null && x.Registration.ReceiverName.Contains(filter.ReceiverName)));
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            query = query.Where(x => x.Registration.CustomerName != null && x.Registration.CustomerName.Contains(filter.CustomerName));
        }

        var rows = await query
            .OrderByDescending(x => x.Session.Weight2Time ?? x.Session.UpdatedAt ?? x.Session.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        if (filter.CompletedDate.HasValue)
        {
            var start = filter.CompletedDate.Value.Date;
            var end = start.AddDays(1);
            rows = rows
                .Where(x =>
                {
                    var completedAt = x.Session.Weight2Time ?? x.Session.UpdatedAt ?? x.Session.CreatedAt;
                    return completedAt >= start && completedAt < end;
                })
                .ToList();
        }

        var sessionIds = rows.Select(x => x.Session.Id).Distinct().ToList();
        var missingProductCodes = rows
            .Where(x => string.IsNullOrWhiteSpace(x.Registration.ProductType) && !string.IsNullOrWhiteSpace(x.Registration.ProductCode))
            .Select(x => x.Registration.ProductCode!.Trim())
            .Distinct()
            .ToList();
        var productTypeLookup = missingProductCodes.Count == 0
            ? new Dictionary<string, string?>()
            : await _db.Products.AsNoTracking()
                .Where(x => missingProductCodes.Contains(x.ProductCode))
                .ToDictionaryAsync(x => x.ProductCode.Trim(), x => x.ProductType, ct);
        var weighTickets = await _db.WeighTickets.AsNoTracking()
            .Where(wt => wt.WeighingSessionId.HasValue
                && wt.StationCode == stationCode
                && sessionIds.Contains(wt.WeighingSessionId.Value)
                && !wt.IsDeleted)
            .ToListAsync(ct);
        var deliveryTickets = await _db.DeliveryTickets.AsNoTracking()
            .Where(dt => dt.WeighingSessionId.HasValue
                && dt.StationCode == stationCode
                && sessionIds.Contains(dt.WeighingSessionId.Value)
                && !dt.IsDeleted)
            .ToListAsync(ct);

        return rows.Select(row =>
        {
            var vr = row.Registration;
            var session = row.Session;
            var line = row.Line;
            var resolvedProductType = ProductTypes.Normalize(vr.ProductType);
            if (resolvedProductType == null
                && !string.IsNullOrWhiteSpace(vr.ProductCode)
                && productTypeLookup.TryGetValue(vr.ProductCode.Trim(), out var fallbackProductType))
            {
                resolvedProductType = ProductTypes.Normalize(fallbackProductType);
            }

            var isBagged = string.Equals(resolvedProductType, ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);
            var weighTicket = weighTickets
                .Where(wt => wt.WeighingSessionId == session.Id)
                .OrderBy(wt => wt.RecordRole == WeighTicketRecordRoles.MasterSession ? 0 : 1)
                .ThenByDescending(wt => wt.UpdatedAt ?? wt.CreatedAt)
                .FirstOrDefault();
            var deliveryTicket = deliveryTickets
                .Where(dt => dt.WeighingSessionId == session.Id && dt.WeighingSessionLineId == line.Id)
                .OrderByDescending(dt => dt.UpdatedAt ?? dt.CreatedAt)
                .FirstOrDefault();
            var completedAt = session.Weight2Time ?? session.UpdatedAt ?? session.CreatedAt;
            var actualWeightKg = deliveryTicket?.AllocatedWeight
                ?? line.ActualAllocatedWeight
                ?? session.NetWeight;

            return new OutgoingVehicleListItem(
                vr.Id,
                session.Id,
                BusinessNumberFormatter.ToDisplay(session.SessionNo),
                vr.ErpCutOrderId,
                vr.TransactionType,
                session.VehiclePlate,
                session.MoocNumber,
                vr.TransportMethod,
                vr.ReceiverName,
                vr.CustomerName,
                vr.ProductCode,
                vr.ProductName,
                resolvedProductType,
                session.DriverName,
                actualWeightKg,
                isBagged ? vr.BagCount : null,
                weighTicket?.Weight1 ?? session.Weight1,
                actualWeightKg,
                isBagged ? (deliveryTicket?.AllocatedBagCount ?? line.ActualAllocatedBagCount) : null,
                session.NetWeight,
                isBagged ? line.ActualAllocatedBagCount : null,
                weighTicket?.Weight2Time ?? session.Weight2Time ?? session.Weight1Time,
                weighTicket?.Weight2User ?? weighTicket?.Weight1User,
                weighTicket is null ? null : BusinessNumberFormatter.ToDisplay(weighTicket.TicketNo),
                deliveryTicket is null ? null : BusinessNumberFormatter.ToDisplay(deliveryTicket.DeliveryNo),
                completedAt,
                weighTicket?.IsPrinted ?? session.HasPrintedMasterWeighTicket,
                deliveryTicket?.IsPrinted ?? line.HasPrintedDeliveryTicket,
                session.UseActualWeightForBaggedCutOrders,
                vr.ErpExportCompleted,
                session.IsNoLoad,
                false);
        }).ToList().AsReadOnly();
    }

    private static DateTime ResolveOutgoingCompletedAt(CutOrder registration, WeighingSession? session)
    {
        if (session != null)
        {
            return session.Weight2Time
                ?? session.Weight1Time
                ?? session.CreatedAt;
        }

        return registration.UpdatedAt ?? NormalizeCreatedAtForDisplay(registration.CutOrderSource, registration.CreatedAt);
    }

    public async Task<IReadOnlyList<ExportScaleCutOrderListItem>> GetActiveExportScaleCutOrdersAsync(ExportScaleCutOrderFilter filter, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var query = _db.CutOrders.AsNoTracking()
            .Where(co => co.StationCode == stationCode
                && !co.IsDeleted
                && !co.IsCancelled
                && co.IsExportScale
                && co.TransactionType == TransactionType.OUTBOUND
                && (co.ProcessingStage == ProcessingStage.WEIGHING
                    || (co.ExportFinalizedAt.HasValue && co.CutOrderStatus == CutOrderStatus.COMPLETED)));

        if (!filter.IncludeErpCompletedFinalized)
        {
            query = query.Where(co => !(co.ExportFinalizedAt.HasValue && co.ErpExportCompleted));
        }

        if (!string.IsNullOrWhiteSpace(filter.ErpCutOrderId))
        {
            query = query.Where(co => co.ErpCutOrderId != null && co.ErpCutOrderId.Contains(filter.ErpCutOrderId));
        }

        if (!string.IsNullOrWhiteSpace(filter.VehiclePlate))
        {
            query = query.Where(co => co.VehiclePlate.Contains(filter.VehiclePlate));
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            query = query.Where(co => co.CustomerName != null && co.CustomerName.Contains(filter.CustomerName));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProductCode))
        {
            query = query.Where(co => co.ProductCode != null && co.ProductCode.Contains(filter.ProductCode));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProductName))
        {
            query = query.Where(co => co.ProductName != null && co.ProductName.Contains(filter.ProductName));
        }

        var cutOrders = await query
            .OrderBy(co => co.ExportFinalizedAt.HasValue || co.CutOrderStatus == CutOrderStatus.COMPLETED ? 1 : 0)
            .ThenByDescending(co => co.UpdatedAt ?? co.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var cutOrderIds = cutOrders.Select(co => co.Id).ToList();
        var tripRows = await (
            from line in _db.WeighingSessionLines.AsNoTracking()
            join session in _db.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            where cutOrderIds.Contains(line.CutOrderId)
                && line.StationCode == stationCode
                && session.StationCode == stationCode
                && !line.IsDeleted
                && !session.IsDeleted
                && !session.IsCancelled
                && line.LineStatus == WeighingSessionLineStatus.ALLOCATED
                && (session.SessionStatus == WeighingSessionStatus.READY_TO_COMPLETE
                    || session.SessionStatus == WeighingSessionStatus.COMPLETED)
            select new
            {
                line.CutOrderId,
                line.WeighingSessionId,
                line.ActualAllocatedWeight,
                session.Weight2Time,
                session.UpdatedAt,
                session.CreatedAt
            })
            .ToListAsync(ct);

        var progressByCutOrder = tripRows
            .GroupBy(x => x.CutOrderId)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    AccumulatedWeight = x.Sum(item => item.ActualAllocatedWeight ?? 0m),
                    TripCount = x.Select(item => item.WeighingSessionId).Distinct().Count(),
                    LastTripAt = x.Max(item => item.Weight2Time ?? item.UpdatedAt ?? item.CreatedAt)
                });

        return cutOrders.Select(co =>
        {
            progressByCutOrder.TryGetValue(co.Id, out var progress);
            var accumulatedWeight = progress?.AccumulatedWeight ?? 0m;
            var plannedWeight = co.PlannedWeight ?? 0m;

            return new ExportScaleCutOrderListItem(
                co.Id,
                co.ErpCutOrderId,
                co.VehiclePlate,
                co.MoocNumber,
                co.CustomerName,
                co.ProductCode,
                co.ProductName,
                co.PlannedWeight,
                accumulatedWeight,
                plannedWeight - accumulatedWeight,
                progress?.TripCount ?? 0,
                progress?.LastTripAt,
                co.ExportFinalizedAt.HasValue || co.CutOrderStatus == CutOrderStatus.COMPLETED,
                co.ErpExportCompleted,
                co.CutOrderStatus,
                co.ProcessingStage,
                co.Notes,
                co.IsTemporaryExport,
                co.TemporaryExportDisplayCode);
        }).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<TemporaryExportCutOrderOption>> GetActiveTemporaryExportCutOrderOptionsAsync(Guid? realCutOrderId, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        CutOrder? realCutOrder = null;
        if (realCutOrderId.HasValue)
        {
            realCutOrder = await _db.CutOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.StationCode == stationCode && x.Id == realCutOrderId.Value, ct);
        }

        var query = _db.CutOrders.AsNoTracking()
            .Where(co => co.StationCode == stationCode
                && co.IsTemporaryExport
                && co.IsExportScale
                && co.TransactionType == TransactionType.OUTBOUND
                && !co.IsDeleted
                && !co.IsCancelled
                && !co.ExportFinalizedAt.HasValue
                && (
                    (co.ProcessingStage == ProcessingStage.WEIGHING && co.CutOrderStatus == CutOrderStatus.IN_SESSION)
                    || _db.WeighingSessionLines.Any(line =>
                        line.CutOrderId == co.Id
                        && line.StationCode == stationCode
                        && !line.IsDeleted
                        && _db.WeighingSessions.Any(session =>
                            session.Id == line.WeighingSessionId
                            && session.StationCode == stationCode
                            && !session.IsDeleted
                            && !session.IsCancelled)))
                && !_db.CutOrders.Any(activeReal =>
                    activeReal.Id == co.MappedRealCutOrderId
                    && activeReal.StationCode == stationCode
                    && !activeReal.IsDeleted
                    && !activeReal.IsCancelled
                    && !activeReal.IsTemporaryExport));

        var temporaryCutOrders = await query
            .OrderByDescending(co => co.UpdatedAt ?? co.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var cutOrderIds = temporaryCutOrders.Select(co => co.Id).ToList();
        var tripRows = await (
            from line in _db.WeighingSessionLines.AsNoTracking()
            join session in _db.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            where cutOrderIds.Contains(line.CutOrderId)
                && line.StationCode == stationCode
                && session.StationCode == stationCode
                && !line.IsDeleted
                && !session.IsDeleted
                && !session.IsCancelled
            select new
            {
                line.CutOrderId,
                line.WeighingSessionId,
                line.ActualAllocatedWeight,
                session.Weight2Time,
                session.UpdatedAt,
                session.CreatedAt
            })
            .ToListAsync(ct);

        var progressByCutOrder = tripRows
            .GroupBy(x => x.CutOrderId)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    AccumulatedWeight = x.Sum(item => item.ActualAllocatedWeight ?? 0m),
                    TripCount = x.Select(item => item.WeighingSessionId).Distinct().Count(),
                    LastTripAt = x.Max(item => item.Weight2Time ?? item.UpdatedAt ?? item.CreatedAt)
                });

        return temporaryCutOrders
            .Select(co =>
            {
                progressByCutOrder.TryGetValue(co.Id, out var progress);

                return new TemporaryExportCutOrderOption(
                    co.Id,
                    co.TemporaryExportDisplayCode ?? co.ErpCutOrderId ?? co.Id.ToString("N")[..8],
                    co.CustomerCode,
                    co.CustomerName,
                    co.ProductCode,
                    co.ProductName,
                    co.PlannedWeight,
                    progress?.AccumulatedWeight ?? 0m,
                    progress?.TripCount ?? 0,
                    progress?.LastTripAt,
                    co.Notes,
                    CalculateTemporaryExportMatchScore(co, realCutOrder));
            })
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.LastTripAt ?? DateTime.MinValue)
            .ThenByDescending(x => x.TripCount)
            .ToList()
            .AsReadOnly();
    }

    private static int CalculateTemporaryExportMatchScore(CutOrder temporaryCutOrder, CutOrder? realCutOrder)
    {
        if (realCutOrder == null)
        {
            return 0;
        }

        var score = 0;
        if (!string.IsNullOrWhiteSpace(temporaryCutOrder.TemporaryExportSourceErpCutOrderId)
            && string.Equals(temporaryCutOrder.TemporaryExportSourceErpCutOrderId, realCutOrder.ErpCutOrderId, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(temporaryCutOrder.CustomerCode)
            && string.Equals(temporaryCutOrder.CustomerCode, realCutOrder.CustomerCode, StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }
        else if (!string.IsNullOrWhiteSpace(temporaryCutOrder.CustomerName)
            && string.Equals(temporaryCutOrder.CustomerName, realCutOrder.CustomerName, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(temporaryCutOrder.ProductCode)
            && string.Equals(temporaryCutOrder.ProductCode, realCutOrder.ProductCode, StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }
        else if (!string.IsNullOrWhiteSpace(temporaryCutOrder.ProductName)
            && string.Equals(temporaryCutOrder.ProductName, realCutOrder.ProductName, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(temporaryCutOrder.Notes)
            && !string.IsNullOrWhiteSpace(realCutOrder.Notes)
            && temporaryCutOrder.Notes.Contains(realCutOrder.Notes, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }

    public async Task<string> GenerateTemporaryExportDisplayCodeAsync(CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        const string prefix = "CL-TAM-";
        var codes = await _db.CutOrders.AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.TemporaryExportDisplayCode != null && x.TemporaryExportDisplayCode.StartsWith(prefix))
            .Select(x => x.TemporaryExportDisplayCode!)
            .ToListAsync(ct);

        var next = codes
            .Select(code => int.TryParse(code[prefix.Length..], out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{next:0000}";
    }

    public async Task<IReadOnlyList<ExportVehicleTripListItem>> GetExportVehicleTripsAsync(Guid cutOrderId, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var tripRows = await (
            from line in _db.WeighingSessionLines.AsNoTracking()
            join cutOrder in _db.CutOrders.AsNoTracking()
                on line.CutOrderId equals cutOrder.Id
            join session in _db.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            where line.CutOrderId == cutOrderId
                && line.StationCode == stationCode
                && cutOrder.StationCode == stationCode
                && session.StationCode == stationCode
                && cutOrder.IsExportScale
                && cutOrder.TransactionType == TransactionType.OUTBOUND
                && !cutOrder.IsDeleted
                && !line.IsDeleted
                && !session.IsDeleted
            orderby session.CreatedAt descending
            select new
            {
                Line = line,
                Session = session
            })
            .ToListAsync(ct);

        var sessionIds = tripRows.Select(x => x.Session.Id).Distinct().ToList();
        var weighTickets = await _db.WeighTickets.AsNoTracking()
            .Where(wt => wt.WeighingSessionId.HasValue
                && wt.StationCode == stationCode
                && sessionIds.Contains(wt.WeighingSessionId.Value)
                && !wt.IsDeleted)
            .ToListAsync(ct);
        var deliveryTickets = await _db.DeliveryTickets.AsNoTracking()
            .Where(dt => dt.WeighingSessionId.HasValue
                && dt.StationCode == stationCode
                && sessionIds.Contains(dt.WeighingSessionId.Value)
                && !dt.IsDeleted)
            .ToListAsync(ct);

        return tripRows.Select(row =>
        {
            var session = row.Session;
            var line = row.Line;
            var weighTicket = weighTickets
                .Where(wt => wt.WeighingSessionId == session.Id)
                .OrderBy(wt => wt.RecordRole == WeighTicketRecordRoles.MasterSession ? 0 : 1)
                .ThenByDescending(wt => wt.UpdatedAt ?? wt.CreatedAt)
                .FirstOrDefault();
            var deliveryTicket = deliveryTickets
                .Where(dt => dt.WeighingSessionId == session.Id && dt.WeighingSessionLineId == line.Id)
                .OrderByDescending(dt => dt.UpdatedAt ?? dt.CreatedAt)
                .FirstOrDefault();

            return new ExportVehicleTripListItem(
                session.Id,
                line.Id,
                BusinessNumberFormatter.ToDisplay(session.SessionNo),
                session.VehiclePlate,
                session.MoocNumber,
                session.DriverName,
                session.Weight1,
                session.Weight2,
                session.NetWeight,
                line.ActualAllocatedWeight,
                session.Weight1Time,
                session.Weight2Time,
                session.SessionStatus,
                weighTicket is null ? null : BusinessNumberFormatter.ToDisplay(weighTicket.TicketNo),
                deliveryTicket is null ? null : BusinessNumberFormatter.ToDisplay(deliveryTicket.DeliveryNo),
                weighTicket?.IsPrinted ?? session.HasPrintedMasterWeighTicket,
                deliveryTicket?.IsPrinted ?? line.HasPrintedDeliveryTicket);
        }).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<VehicleAutocompleteSource>> SearchVehicleHistorySourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => vr.StationCode == stationCode && !vr.IsDeleted && !vr.IsCancelled && vr.VehiclePlate.Contains(normalized))
            .OrderByDescending(vr => vr.VehiclePlate.StartsWith(normalized))
            .ThenByDescending(vr => vr.UpdatedAt ?? vr.CreatedAt)
            .Select(vr => new VehicleAutocompleteSource(
                vr.VehiclePlate,
                vr.MoocNumber,
                vr.ReceiverName,
                null,
                null,
                null,
                null,
                null,
                "HISTORY"))
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<VehicleAutocompleteSource>> SearchMoocHistorySourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => vr.StationCode == stationCode && !vr.IsDeleted && !vr.IsCancelled && vr.MoocNumber != null && vr.MoocNumber.Contains(normalized))
            .OrderByDescending(vr => vr.MoocNumber != null && vr.MoocNumber.StartsWith(normalized))
            .ThenByDescending(vr => vr.UpdatedAt ?? vr.CreatedAt)
            .Select(vr => new VehicleAutocompleteSource(
                vr.VehiclePlate,
                vr.MoocNumber,
                vr.ReceiverName,
                null,
                null,
                null,
                null,
                null,
                "HISTORY"))
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DriverAutocompleteSource>> SearchDriverHistorySourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => vr.StationCode == stationCode && !vr.IsDeleted && !vr.IsCancelled && vr.ReceiverName != null && vr.ReceiverName.Contains(normalized))
            .OrderByDescending(vr => vr.ReceiverName != null && vr.ReceiverName.StartsWith(normalized))
            .ThenByDescending(vr => vr.UpdatedAt ?? vr.CreatedAt)
            .Select(vr => new DriverAutocompleteSource(
                vr.ReceiverName!,
                vr.VehiclePlate,
                vr.MoocNumber,
                "HISTORY"))
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<CustomerAutocompleteSource>> SearchCustomerHistorySourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => vr.StationCode == stationCode && !vr.IsCancelled && vr.CustomerName != null
                && ((vr.CustomerCode != null && vr.CustomerCode.Contains(normalized)) || vr.CustomerName.Contains(normalized)))
            .OrderByDescending(vr => vr.CustomerName != null && vr.CustomerName.StartsWith(normalized))
            .ThenByDescending(vr => vr.UpdatedAt ?? vr.CreatedAt)
            .Select(vr => new CustomerAutocompleteSource(
                vr.CustomerCode,
                vr.CustomerName!,
                "HISTORY"))
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ProductAutocompleteSource>> SearchProductCodeHistorySourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => vr.StationCode == stationCode && !vr.IsCancelled && vr.ProductCode != null && vr.ProductCode.Contains(normalized))
            .OrderByDescending(vr => vr.ProductCode != null && vr.ProductCode.StartsWith(normalized))
            .ThenByDescending(vr => vr.UpdatedAt ?? vr.CreatedAt)
            .Select(vr => new ProductAutocompleteSource(
                vr.ProductCode!,
                vr.ProductName ?? string.Empty,
                vr.ProductType,
                "HISTORY"))
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ProductAutocompleteSource>> SearchProductNameHistorySourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => vr.StationCode == stationCode && !vr.IsCancelled && vr.ProductName != null
                && ((vr.ProductCode != null && vr.ProductCode.Contains(normalized)) || vr.ProductName.Contains(normalized)))
            .OrderByDescending(vr => vr.ProductName != null && vr.ProductName.StartsWith(normalized))
            .ThenByDescending(vr => vr.UpdatedAt ?? vr.CreatedAt)
            .Select(vr => new ProductAutocompleteSource(
                vr.ProductCode ?? string.Empty,
                vr.ProductName!,
                vr.ProductType,
                "HISTORY"))
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }
}


