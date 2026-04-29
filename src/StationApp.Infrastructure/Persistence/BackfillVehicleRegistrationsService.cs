using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Infrastructure.Persistence;

public sealed class BackfillVehicleRegistrationsService
{
    private readonly StationDbContext _dbContext;

    public BackfillVehicleRegistrationsService(StationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Clean up any legacy statuses that are no longer supported by the enum
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE vehicle_registrations SET RegistrationStatus = 'LOADING_IN_PROGRESS' WHERE RegistrationStatus = 'OVERWEIGHT_PENDING_ACTION'", 
            ct);

        var tickets = await _dbContext.WeighTickets
            .Where(t => t.VehicleRegistrationId == Guid.Empty)
            .ToListAsync(ct);

        var deliveryTickets = await _dbContext.DeliveryTickets
            .Where(t => t.VehicleRegistrationId == Guid.Empty)
            .ToListAsync(ct);

        if (!tickets.Any() && !deliveryTickets.Any())
            return;

        var registrationsToCreate = new List<VehicleRegistration>();

        // 1. Group by ErpVehicleRegistrationId
        var erpGroups = tickets
            .Where(t => !string.IsNullOrEmpty(t.ErpVehicleRegistrationId))
            .GroupBy(t => t.ErpVehicleRegistrationId!)
            .ToList();

        foreach (var group in erpGroups)
        {
            var primaryTicket = group.OrderByDescending(t => t.CreatedAt).First();
            var reg = CreateRegistrationFromTicket(primaryTicket, RegistrationSource.ERP);
            registrationsToCreate.Add(reg);

            foreach (var ticket in group)
            {
                ticket.VehicleRegistrationId = reg.Id;
            }

            var matchedDeliveries = deliveryTickets
                .Where(d => d.ErpVehicleRegistrationId == group.Key)
                .ToList();

            foreach (var del in matchedDeliveries)
            {
                del.VehicleRegistrationId = reg.Id;
            }
        }

        // 2. Group by SplitGroupId
        var remainingTickets = tickets.Where(t => t.VehicleRegistrationId == Guid.Empty).ToList();
        var splitGroups = remainingTickets
            .Where(t => t.SplitGroupId.HasValue)
            .GroupBy(t => t.SplitGroupId!.Value)
            .ToList();

        foreach (var group in splitGroups)
        {
            var primaryTicket = group.OrderByDescending(t => t.CreatedAt).First();
            var reg = CreateRegistrationFromTicket(primaryTicket, RegistrationSource.MANUAL);
            registrationsToCreate.Add(reg);

            foreach (var ticket in group)
            {
                ticket.VehicleRegistrationId = reg.Id;
            }

            var matchedDeliveries = deliveryTickets
                .Where(d => d.VehicleRegistrationId == Guid.Empty && d.SplitGroupId == group.Key)
                .ToList();

            foreach (var del in matchedDeliveries)
            {
                del.VehicleRegistrationId = reg.Id;
            }
        }

        // 3. Fallback: 1 weigh ticket = 1 vehicle registration
        remainingTickets = tickets.Where(t => t.VehicleRegistrationId == Guid.Empty).ToList();
        foreach (var ticket in remainingTickets)
        {
            var reg = CreateRegistrationFromTicket(ticket, RegistrationSource.MANUAL);
            registrationsToCreate.Add(reg);
            ticket.VehicleRegistrationId = reg.Id;
        }

        // 4. Any remaining delivery tickets
        var remainingDeliveries = deliveryTickets.Where(d => d.VehicleRegistrationId == Guid.Empty).ToList();
        foreach (var delivery in remainingDeliveries)
        {
            var reg = new VehicleRegistration
            {
                Id = Guid.NewGuid(),
                ErpVehicleRegistrationId = delivery.ErpVehicleRegistrationId,
                RegistrationSource = string.IsNullOrEmpty(delivery.ErpVehicleRegistrationId) ? RegistrationSource.MANUAL : RegistrationSource.ERP,
                RegistrationStatus = RegistrationStatus.COMPLETED,
                TransactionType = TransactionType.OUTBOUND,
                CustomerCode = delivery.CustomerCode,
                ProductCode = delivery.ProductCode,
                SyncStatus = SyncStatus.SYNC_QUEUED,
                IdempotencyKey = Guid.NewGuid(),
                CreatedAt = delivery.CreatedAt,
                CreatedBy = delivery.CreatedBy
            };
            registrationsToCreate.Add(reg);
            delivery.VehicleRegistrationId = reg.Id;
        }

        await _dbContext.VehicleRegistrations.AddRangeAsync(registrationsToCreate, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    private VehicleRegistration CreateRegistrationFromTicket(WeighTicket ticket, RegistrationSource source)
    {
        return new VehicleRegistration
        {
            Id = Guid.NewGuid(),
            ErpVehicleRegistrationId = ticket.ErpVehicleRegistrationId,
            RegistrationSource = source,
            RegistrationStatus = MapStatus(ticket.Status),
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

    private RegistrationStatus MapStatus(TicketStatus status)
    {
        return status switch
        {
            TicketStatus.TICKET_CREATED => RegistrationStatus.REGISTERED,
            TicketStatus.LOADING_STARTED => RegistrationStatus.LOADING_IN_PROGRESS,
            TicketStatus.TICKET_COMPLETED => RegistrationStatus.COMPLETED,
            TicketStatus.TICKET_CANCELLED => RegistrationStatus.CANCELLED,
            _ => RegistrationStatus.REGISTERED
        };
    }
}
