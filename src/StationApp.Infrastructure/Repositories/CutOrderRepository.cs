using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StationApp.Infrastructure.Persistence;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Infrastructure.Repositories;

public class CutOrderRepository : ICutOrderRepository
{
    private readonly StationDbContext _db;
    private static readonly TimeZoneInfo VnTimeZone = GetVnTimeZone();

    public CutOrderRepository(StationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(CutOrder registration, CancellationToken ct)
    {
        await _db.CutOrders.AddAsync(registration, ct);
    }

    public Task UpdateAsync(CutOrder registration, CancellationToken ct)
    {
        _db.CutOrders.Update(registration);
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
        return await _db.CutOrders
            .FirstOrDefaultAsync(v => v.ErpCutOrderId == erpCutOrderId, ct);
    }

    public async Task<IReadOnlyList<CutOrder>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct)
    {
        return await _db.CutOrders
            .Where(x => x.WeighingSessionId == weighingSessionId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CutOrder>> GetBySyncStatusAsync(SyncStatus syncStatus, int take, CancellationToken ct)
    {
        return await _db.CutOrders
            .Where(x => x.SyncStatus == syncStatus)
            .OrderBy(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CutOrder>> SearchAsync(string? keyword, CancellationToken ct)
    {
        var query = _db.CutOrders.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(v => v.VehiclePlate.Contains(keyword) || 
                                     (v.ErpCutOrderId != null && v.ErpCutOrderId.Contains(keyword)));
        }
        return await query.OrderByDescending(v => v.CreatedAt).Take(100).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CutOrder>> GetUnprocessedInboundAsync(CancellationToken ct)
    {
        return await _db.CutOrders
            .Where(v => !v.IsInboundProcessed && v.CutOrderSource == CutOrderSource.ERP)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeightViewListItem>> GetWeightViewListAsync(string? keyword, CancellationToken ct)
    {
        var regQuery = _db.CutOrders.AsNoTracking()
            .Where(vr => vr.ProcessingStage == ProcessingStage.WEIGHING && !vr.IsCancelled);

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
                .Where(wt => !wt.IsDeleted && wt.TicketNo != null && wt.TicketNo.Contains(keyword))
                .Select(wt => wt.CutOrderId)
                .ToListAsync(ct);

            var extraRegIds = matchedByTicketNo.Where(id => !regIds.Contains(id)).Distinct().ToList();
            if (extraRegIds.Any())
            {
                var extraRegs = await _db.CutOrders.AsNoTracking()
                    .Where(vr => extraRegIds.Contains(vr.Id))
                    .ToListAsync(ct);

                registrations.AddRange(extraRegs);
                regIds = registrations.Select(x => x.Id).ToList();
            }
        }

        var weighTickets = await _db.WeighTickets.AsNoTracking()
            .Where(wt => regIds.Contains(wt.CutOrderId) && !wt.IsDeleted)
            .ToListAsync(ct);

        var deliveryTickets = await _db.DeliveryTickets.AsNoTracking()
            .Where(dt => regIds.Contains(dt.CutOrderId) && !dt.IsDeleted)
            .ToListAsync(ct);

        var sessionIds = registrations
            .Where(vr => vr.WeighingSessionId.HasValue)
            .Select(vr => vr.WeighingSessionId!.Value)
            .Distinct()
            .ToList();
        var sessionById = sessionIds.Count == 0
            ? new Dictionary<Guid, WeighingSession>()
            : await _db.WeighingSessions.AsNoTracking()
                .Where(session => sessionIds.Contains(session.Id))
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
                primaryWeighTicket?.TicketNo,
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
                primaryDeliveryTicket?.DeliveryNo,
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
        var query = _db.CutOrders.AsNoTracking()
            .Where(vr => vr.ProcessingStage == ProcessingStage.IN_YARD && !vr.IsCancelled);

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

        var list = registrations.Select(vr => new IncomingVehicleListItem(
            vr.Id,
            vr.ErpCutOrderId,
            vr.TransactionType,
            vr.VehiclePlate,
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
            vr.ProductType
        )).ToList();

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
    {
        if (source != CutOrderSource.ERP)
        {
            return createdAt;
        }

        var utcValue = createdAt.Kind switch
        {
            DateTimeKind.Utc => createdAt,
            DateTimeKind.Local => createdAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utcValue, VnTimeZone);
    }

    private static TimeZoneInfo GetVnTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    public async Task<IReadOnlyList<OutgoingVehicleListItem>> GetOutgoingListAsync(OutgoingVehicleListFilter filter, CancellationToken ct)
    {
        var query =
            from vr in _db.CutOrders.AsNoTracking()
            join ws in _db.WeighingSessions.AsNoTracking()
                on vr.WeighingSessionId equals ws.Id into sessionGroup
            from session in sessionGroup.DefaultIfEmpty()
            where vr.ProcessingStage == ProcessingStage.OUT_YARD
                && vr.CutOrderStatus == CutOrderStatus.COMPLETED
                && !vr.IsCancelled
            select new
            {
                Registration = vr,
                Session = session
            };

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
        }

        var registrations = await query
            .OrderByDescending(x => (x.Session != null ? x.Session.UpdatedAt : x.Registration.UpdatedAt) ?? (x.Session != null ? x.Session.CreatedAt : x.Registration.CreatedAt))
            .Take(200)
            .ToListAsync(ct);

        if (filter.CompletedDate.HasValue)
        {
            var start = filter.CompletedDate.Value.Date;
            var end = start.AddDays(1);
            registrations = registrations
                .Where(x =>
                {
                    var completedAt = ResolveOutgoingCompletedAt(x.Registration, x.Session);
                    return completedAt >= start && completedAt < end;
                })
                .ToList();
        }
        var regIds = registrations.Select(x => x.Registration.Id).ToList();
        var sessionIds = registrations
            .Where(x => x.Session != null)
            .Select(x => x.Session!.Id)
            .Distinct()
            .ToList();

        var registrationWeighTickets = await _db.WeighTickets.AsNoTracking()
            .Where(wt => regIds.Contains(wt.CutOrderId) && !wt.IsDeleted
                && wt.RecordRole == WeighTicketRecordRoles.MasterSession)
            .ToListAsync(ct);

        var sessionMasterWeighTickets = await _db.WeighTickets.AsNoTracking()
            .Where(wt => wt.WeighingSessionId.HasValue
                && sessionIds.Contains(wt.WeighingSessionId.Value)
                && !wt.IsDeleted
                && wt.RecordRole == WeighTicketRecordRoles.MasterSession)
            .ToListAsync(ct);

        var deliveryTickets = await _db.DeliveryTickets.AsNoTracking()
            .Where(dt => regIds.Contains(dt.CutOrderId) && !dt.IsDeleted
                && dt.RecordRole == DeliveryTicketRecordRoles.Normal)
            .ToListAsync(ct);

        return registrations.Select(item =>
        {
            var vr = item.Registration;
            var session = item.Session;
            var relatedWeigh = registrationWeighTickets.Where(wt => wt.CutOrderId == vr.Id).ToList();
            var sessionMasterWeigh = session == null
                ? null
                : sessionMasterWeighTickets
                    .Where(wt => wt.WeighingSessionId == session.Id)
                    .OrderByDescending(wt => wt.UpdatedAt ?? wt.CreatedAt)
                    .FirstOrDefault();
            var relatedDelivery = deliveryTickets.Where(dt => dt.CutOrderId == vr.Id).ToList();
            var primaryWeigh = sessionMasterWeigh ?? ResolvePrimaryWeighTicket(vr, relatedWeigh);
            var hasPrintedWeighTicket = session?.HasPrintedMasterWeighTicket
                ?? sessionMasterWeigh?.IsPrinted
                ?? (relatedWeigh.Count > 0 && relatedWeigh.All(wt => wt.IsPrinted));

            return new OutgoingVehicleListItem(
                vr.Id,
                vr.WeighingSessionId,
                session?.SessionNo,
                vr.ErpCutOrderId,
                vr.TransactionType,
                vr.VehiclePlate,
                vr.MoocNumber,
                vr.ReceiverName,
                vr.CustomerName,
                vr.ProductName,
                session?.DriverName,
                primaryWeigh?.Weight1,
                primaryWeigh?.Weight2,
                primaryWeigh?.NetWeight,
                ResolveOutgoingCompletedAt(vr, session),
                vr.TransportMethod,
                hasPrintedWeighTicket,
                relatedDelivery.Count == 0 || relatedDelivery.All(dt => dt.IsPrinted)
            );
        }).ToList().AsReadOnly();
    }

    private static DateTime ResolveOutgoingCompletedAt(CutOrder registration, WeighingSession? session)
    {
        if (session != null)
        {
            return session.UpdatedAt ?? session.CreatedAt;
        }

        return registration.UpdatedAt ?? NormalizeCreatedAtForDisplay(registration.CutOrderSource, registration.CreatedAt);
    }

    public async Task<IReadOnlyList<VehicleAutocompleteSource>> SearchVehicleHistorySourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => !vr.IsCancelled && vr.VehiclePlate.Contains(normalized))
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
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => !vr.IsCancelled && vr.MoocNumber != null && vr.MoocNumber.Contains(normalized))
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
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => !vr.IsCancelled && vr.ReceiverName != null && vr.ReceiverName.Contains(normalized))
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
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => !vr.IsCancelled && vr.CustomerName != null
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
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => !vr.IsCancelled && vr.ProductCode != null && vr.ProductCode.Contains(normalized))
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
        var normalized = keyword.Trim();

        var list = await _db.CutOrders.AsNoTracking()
            .Where(vr => !vr.IsCancelled && vr.ProductName != null
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


