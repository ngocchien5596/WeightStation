using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Infrastructure.Persistence;

public sealed class BackfillCutOrdersService
{
    private readonly StationDbContext _dbContext;

    public BackfillCutOrdersService(StationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Clean up any legacy statuses that are no longer supported by the enum
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE cut_orders SET CutOrderStatus = 'LOADING_IN_PROGRESS' WHERE CutOrderStatus = 'OVERWEIGHT_PENDING_ACTION'", 
            ct);

        var tickets = await _dbContext.WeighTickets
            .Where(t => t.CutOrderId == Guid.Empty)
            .ToListAsync(ct);

        var deliveryTickets = await _dbContext.DeliveryTickets
            .Where(t => t.CutOrderId == Guid.Empty)
            .ToListAsync(ct);

        if (!tickets.Any() && !deliveryTickets.Any())
            return;

        var registrationsToCreate = new List<CutOrder>();

        // 1. Group by ErpCutOrderId
        var erpGroups = tickets
            .Where(t => !string.IsNullOrEmpty(t.ErpCutOrderId))
            .GroupBy(t => t.ErpCutOrderId!)
            .ToList();

        foreach (var group in erpGroups)
        {
            var primaryTicket = group.OrderByDescending(t => t.CreatedAt).First();
            var reg = CreateRegistrationFromTicket(primaryTicket, CutOrderSource.ERP);
            registrationsToCreate.Add(reg);

            foreach (var ticket in group)
            {
                ticket.CutOrderId = reg.Id;
            }

            var matchedDeliveries = deliveryTickets
                .Where(d => d.ErpCutOrderId == group.Key)
                .ToList();

            foreach (var del in matchedDeliveries)
            {
                del.CutOrderId = reg.Id;
            }
        }

        // 2. Group by SplitGroupId
        var remainingTickets = tickets.Where(t => t.CutOrderId == Guid.Empty).ToList();
        var splitGroups = remainingTickets
            .Where(t => t.SplitGroupId.HasValue)
            .GroupBy(t => t.SplitGroupId!.Value)
            .ToList();

        foreach (var group in splitGroups)
        {
            var primaryTicket = group.OrderByDescending(t => t.CreatedAt).First();
            var reg = CreateRegistrationFromTicket(primaryTicket, CutOrderSource.MANUAL);
            registrationsToCreate.Add(reg);

            foreach (var ticket in group)
            {
                ticket.CutOrderId = reg.Id;
            }

            var matchedDeliveries = deliveryTickets
                .Where(d => d.CutOrderId == Guid.Empty && d.SplitGroupId == group.Key)
                .ToList();

            foreach (var del in matchedDeliveries)
            {
                del.CutOrderId = reg.Id;
            }
        }

        // 3. Fallback: 1 weigh ticket = 1 vehicle registration
        remainingTickets = tickets.Where(t => t.CutOrderId == Guid.Empty).ToList();
        foreach (var ticket in remainingTickets)
        {
            var reg = CreateRegistrationFromTicket(ticket, CutOrderSource.MANUAL);
            registrationsToCreate.Add(reg);
            ticket.CutOrderId = reg.Id;
        }

        // 4. Any remaining delivery tickets
        var remainingDeliveries = deliveryTickets.Where(d => d.CutOrderId == Guid.Empty).ToList();
        foreach (var delivery in remainingDeliveries)
        {
            var reg = new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = delivery.ErpCutOrderId,
                CutOrderSource = string.IsNullOrEmpty(delivery.ErpCutOrderId) ? CutOrderSource.MANUAL : CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.COMPLETED,
                TransactionType = TransactionType.OUTBOUND,
                CustomerCode = delivery.CustomerCode,
                ProductCode = delivery.ProductCode,
                SyncStatus = SyncStatus.SYNC_QUEUED,
                IdempotencyKey = Guid.NewGuid(),
                CreatedAt = delivery.CreatedAt,
                CreatedBy = delivery.CreatedBy
            };
            registrationsToCreate.Add(reg);
            delivery.CutOrderId = reg.Id;
        }

        await _dbContext.CutOrders.AddRangeAsync(registrationsToCreate, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    private CutOrder CreateRegistrationFromTicket(WeighTicket ticket, CutOrderSource source)
    {
        return new CutOrder
        {
            Id = Guid.NewGuid(),
            ErpCutOrderId = ticket.ErpCutOrderId,
            CutOrderSource = source,
            CutOrderStatus = MapStatus(ticket.Status),
            TransactionType = ticket.TransactionType,
            TransportMethod = ticket.TransportMethod,
            VehiclePlate = ticket.VehiclePlate,
            MoocNumber = ticket.MoocNumber,
            ReceiverName = ticket.DriverName,
            CustomerCode = ticket.CustomerCode,
            CustomerName = ticket.CustomerName,
            ProductCode = ticket.ProductCode,
            ProductName = ticket.ProductName,
            PlannedWeight = ticket.PlannedWeight,
            BagCount = ticket.BagCount,
            Notes = ticket.Notes,
            IsCancelled = ticket.IsCancelled,
            HasOverweightCase = ticket.IsOverWeight,
            CurrentPrimaryWeighTicketId = ticket.Id,
            SyncStatus = ticket.SyncStatus,
            IdempotencyKey = ticket.IdempotencyKey,
            AppVersion = ticket.AppVersion,
            CreatedAt = ticket.CreatedAt,
            CreatedBy = ticket.CreatedBy,
            UpdatedAt = ticket.UpdatedAt,
            UpdatedBy = ticket.UpdatedBy
        };
    }

    private CutOrderStatus MapStatus(TicketStatus status)
    {
        return status switch
        {
            TicketStatus.TICKET_CREATED => CutOrderStatus.REGISTERED,
            TicketStatus.LOADING_STARTED => CutOrderStatus.LOADING_IN_PROGRESS,
            TicketStatus.TICKET_COMPLETED => CutOrderStatus.COMPLETED,
            TicketStatus.TICKET_CANCELLED => CutOrderStatus.CANCELLED,
            _ => CutOrderStatus.REGISTERED
        };
    }
}


