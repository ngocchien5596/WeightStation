using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class SplitOverweightTicketUseCase
{
    private readonly IVehicleRegistrationRepository _regRepo;
    private readonly IWeighTicketRepository _ticketRepo;
    private readonly IDeliveryTicketRepository _deliveryTicketRepo;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncPayloadFactory _payloadFactory;
    private readonly IAppConfigRepository _configRepo;
    private readonly ITicketNumberGenerator _ticketNoGen;
    private readonly IDeliveryNumberGenerator _deliveryNoGen;
    private readonly IVehicleRepository _vehicleRepo;

    public SplitOverweightTicketUseCase(
        IVehicleRegistrationRepository regRepo,
        IWeighTicketRepository ticketRepo,
        IDeliveryTicketRepository deliveryTicketRepo,
        ISyncOutboxRepository outboxRepo,
        IUnitOfWork uow,
        IAppVersionProvider versionProvider,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        ISyncPayloadFactory payloadFactory,
        IAppConfigRepository configRepo,
        ITicketNumberGenerator ticketNoGen,
        IDeliveryNumberGenerator deliveryNoGen,
        IVehicleRepository vehicleRepo)
    {
        _regRepo = regRepo;
        _ticketRepo = ticketRepo;
        _deliveryTicketRepo = deliveryTicketRepo;
        _outboxRepo = outboxRepo;
        _uow = uow;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _payloadFactory = payloadFactory;
        _configRepo = configRepo;
        _ticketNoGen = ticketNoGen;
        _deliveryNoGen = deliveryNoGen;
        _vehicleRepo = vehicleRepo;
    }

    public async Task<OperationResult<WeighTicket>> ExecuteAsync(SplitOverweightTicketRequest request, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(request.RegistrationId, ct)
            ?? throw new Exception($"Vehicle Registration {request.RegistrationId} not found");

        if (reg.CurrentPrimaryWeighTicketId == null)
            throw new InvalidOperationException("No primary weigh ticket found for this registration.");

        var ticket1 = await _ticketRepo.GetPrimaryByVehicleRegistrationIdAsync(reg.Id, ct)
            ?? throw new Exception($"Weigh Ticket {reg.CurrentPrimaryWeighTicketId} not found");

        var deliveryTicket1 = await _deliveryTicketRepo.GetPrimaryByVehicleRegistrationIdAsync(reg.Id, ct);

        // Fetch overweight_split_residual_ratio from config
        decimal splitRatio = 0m;
        var splitRatioStr = await _configRepo.GetValueAsync("overweight_split_residual_ratio", ct);
        if (!string.IsNullOrWhiteSpace(splitRatioStr) && decimal.TryParse(splitRatioStr, out var parsedRatio))
        {
            splitRatio = parsedRatio;
        }

        var now = _clock.NowLocal;

        // Calculate weights
        decimal netWeightGoc = Math.Abs((ticket1.Weight1 ?? 0) - request.Weight2);
        // Fetch vehicle for TtcpWeight
        decimal ttcpWeightValue = 0m;
        var vehicle = await _vehicleRepo.GetByPlateAndMoocAsync(reg.VehiclePlate, reg.MoocNumber ?? "", ct);
        if (vehicle != null && vehicle.TtcpWeight.HasValue)
        {
            ttcpWeightValue = vehicle.TtcpWeight.Value;
        }
        else
        {
            ttcpWeightValue = reg.PlannedWeight ?? 0m;
        }

        decimal allowedThreshold = ttcpWeightValue * 1.10m;
        
        decimal netWeight1 = Math.Round(allowedThreshold * (1 - splitRatio), 0);
        decimal netWeight2 = netWeightGoc - netWeight1;

        // Update Ticket 1
        ticket1.Weight2 = reg.TransactionType == TransactionType.OUTBOUND 
            ? (ticket1.Weight1 ?? 0) + netWeight1 
            : (ticket1.Weight1 ?? 0) - netWeight1;
        ticket1.NetWeight = netWeight1;
        ticket1.Weight2User = _userContext.Username;
        ticket1.Weight2Time = now;
        ticket1.Weight2UpdatedAt = now;
        ticket1.Weight2Mode = request.Mode;
        ticket1.Weight2IsStable = request.IsStable;
        ticket1.IsOverWeight = false; // It fits the split requirement
        ticket1.IsPrimaryDisplay = true;
        ticket1.SyncStatus = SyncStatus.SYNC_QUEUED;
        ticket1.Status = TicketStatus.TICKET_COMPLETED;
        ticket1.SplitGroupId = Guid.NewGuid();
        ticket1.SplitSequence = 1;
        ticket1.UpdatedAt = now;
        ticket1.UpdatedBy = _userContext.Username;

        // Create Ticket 2
        var ticket2 = new WeighTicket
        {
            Id = Guid.NewGuid(),
            VehicleRegistrationId = reg.Id,
            TicketNo = await _ticketNoGen.GenerateAsync(ct),
            TransactionType = reg.TransactionType,
            TransportMethod = reg.TransportMethod,
            VehiclePlate = reg.VehiclePlate,
            CustomerCode = reg.CustomerCode,
            CustomerName = reg.CustomerName,
            ProductCode = reg.ProductCode,
            ProductName = reg.ProductName,
            MoocNumber = reg.MoocNumber,
            Notes = reg.Notes,
            PlannedWeight = reg.PlannedWeight,
            Weight1 = ticket1.Weight2, // Ticket 1's Weight 2
            Weight1IsStable = ticket1.Weight2IsStable,
            Weight1Mode = ticket1.Weight2Mode,
            Weight1Time = ticket1.Weight2Time,
            Weight1UpdatedAt = ticket1.Weight2UpdatedAt,
            Weight1User = ticket1.Weight2User,
            Weight2 = request.Weight2, // original Gross Weight
            Weight2IsStable = request.IsStable,
            Weight2Mode = request.Mode,
            Weight2Time = now,
            Weight2UpdatedAt = now,
            Weight2User = _userContext.Username,
            NetWeight = netWeight2,
            Status = TicketStatus.TICKET_COMPLETED,
            SyncStatus = SyncStatus.SYNC_QUEUED,
            RecordRole = "WORKING",
            IsPrimaryDisplay = false,
            IsOverWeight = true,
            SplitGroupId = ticket1.SplitGroupId,
            SplitSequence = 2,
            SourceTicketId = ticket1.Id,
            CreatedAt = now,
            CreatedBy = _userContext.Username,
            UpdatedAt = now,
            UpdatedBy = _userContext.Username,
            AppVersion = _versionProvider.GetVersion()
        };

        DeliveryTicket? deliveryTicket2 = null;

        if (deliveryTicket1 != null)
        {
            // Update Delivery Ticket 1
            deliveryTicket1.IsOverWeight = false;
            deliveryTicket1.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket1.SplitGroupId = ticket1.SplitGroupId;
            deliveryTicket1.SplitSequence = 1;
            deliveryTicket1.UpdatedAt = now;
            deliveryTicket1.UpdatedBy = _userContext.Username;

            // Create Delivery Ticket 2
            deliveryTicket2 = new DeliveryTicket
            {
                Id = Guid.NewGuid(),
                VehicleRegistrationId = reg.Id,
                DeliveryNo = await _deliveryNoGen.GenerateAsync(ct),
                ErpVehicleRegistrationId = deliveryTicket1.ErpVehicleRegistrationId,
                CustomerCode = deliveryTicket1.CustomerCode,
                ProductCode = deliveryTicket1.ProductCode,
                Notes = deliveryTicket1.Notes,
                RecordRole = "WORKING",
                SplitGroupId = ticket1.SplitGroupId,
                SplitSequence = 2,
                SourceDeliveryTicketId = deliveryTicket1.Id,
                SyncStatus = SyncStatus.SYNC_QUEUED,
                CreatedAt = now,
                CreatedBy = _userContext.Username,
                UpdatedAt = now,
                UpdatedBy = _userContext.Username
            };
        }

        // Update VehicleRegistration
        reg.RegistrationStatus = RegistrationStatus.COMPLETED;
        reg.HasOverweightCase = true;
        reg.CurrentPrimaryWeighTicketId = ticket1.Id;
        if (deliveryTicket1 != null)
        {
            reg.CurrentPrimaryDeliveryTicketId = deliveryTicket1.Id;
        }
        reg.SyncStatus = SyncStatus.SYNC_QUEUED;
        reg.UpdatedAt = now;
        reg.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _ticketRepo.UpdateAsync(ticket1, innerCt);
            await _ticketRepo.AddAsync(ticket2, innerCt);

            if (deliveryTicket1 != null && deliveryTicket2 != null)
            {
                await _deliveryTicketRepo.UpdateAsync(deliveryTicket1, innerCt);
                await _deliveryTicketRepo.AddAsync(deliveryTicket2, innerCt);
            }

            await _regRepo.UpdateAsync(reg, innerCt);

            // Enqueue Outbox for VehicleRegistration
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = reg.Id,
                AggregateType = nameof(VehicleRegistration),
                PayloadJson = _payloadFactory.CreatePayload(reg),
                IdempotencyKey = Guid.NewGuid(),
                Status = OutboxStatus.PENDING,
                CreatedAt = now
            }, innerCt);

            // Enqueue Outbox for Ticket 1
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = ticket1.Id,
                AggregateType = nameof(WeighTicket),
                PayloadJson = _payloadFactory.CreatePayload(ticket1),
                IdempotencyKey = Guid.NewGuid(),
                Status = OutboxStatus.PENDING,
                CreatedAt = now
            }, innerCt);

            // Enqueue Outbox for Ticket 2
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = ticket2.Id,
                AggregateType = nameof(WeighTicket),
                PayloadJson = _payloadFactory.CreatePayload(ticket2),
                IdempotencyKey = Guid.NewGuid(),
                Status = OutboxStatus.PENDING,
                CreatedAt = now
            }, innerCt);

        }, ct);

        await _audit.LogAsync("SPLIT_OVERWEIGHT_TICKET", nameof(VehicleRegistration), reg.Id,
            new { ticket1Net = ticket1.NetWeight, ticket2Net = ticket2.NetWeight, allowedThreshold }, ct);

        return OperationResult<WeighTicket>.Ok(ticket2);
    }
}
