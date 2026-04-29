using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CompleteOverweightTicketWithoutSplitUseCase
{
    private readonly IVehicleRegistrationRepository _regRepo;
    private readonly IWeighTicketRepository _ticketRepo;
    private readonly IDeliveryTicketRepository _deliveryTicketRepo;
    private readonly EnsurePrimaryDeliveryTicketUseCase _ensurePrimaryDeliveryTicketUseCase;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncPayloadFactory _payloadFactory;

    public CompleteOverweightTicketWithoutSplitUseCase(
        IVehicleRegistrationRepository regRepo,
        IWeighTicketRepository ticketRepo,
        IDeliveryTicketRepository deliveryTicketRepo,
        EnsurePrimaryDeliveryTicketUseCase ensurePrimaryDeliveryTicketUseCase,
        ISyncOutboxRepository outboxRepo,
        IUnitOfWork uow,
        IAppVersionProvider versionProvider,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit,
        ISyncPayloadFactory payloadFactory)
    {
        _regRepo = regRepo;
        _ticketRepo = ticketRepo;
        _deliveryTicketRepo = deliveryTicketRepo;
        _ensurePrimaryDeliveryTicketUseCase = ensurePrimaryDeliveryTicketUseCase;
        _outboxRepo = outboxRepo;
        _uow = uow;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _payloadFactory = payloadFactory;
    }

    public async Task<OperationResult<WeighTicket>> ExecuteAsync(CompleteOverweightTicketWithoutSplitRequest request, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(request.RegistrationId, ct)
            ?? throw new Exception($"Vehicle Registration {request.RegistrationId} not found");

        if (reg.RegistrationStatus != RegistrationStatus.LOADING_IN_PROGRESS)
        {
            throw new InvalidOperationException($"Cannot complete overweight registration when status is {reg.RegistrationStatus}");
        }

        if (reg.CurrentPrimaryWeighTicketId == null)
            throw new InvalidOperationException("No primary weigh ticket found for this registration.");

        var ticket = await _ticketRepo.GetPrimaryByVehicleRegistrationIdAsync(reg.Id, ct)
            ?? throw new Exception($"Weigh Ticket {reg.CurrentPrimaryWeighTicketId} not found");

        var deliveryTicket = await _deliveryTicketRepo.GetPrimaryByVehicleRegistrationIdAsync(reg.Id, ct);

        var now = _clock.NowLocal;

        // Update Ticket
        ticket.Weight2 = request.Weight;
        ticket.Weight2User = _userContext.Username;
        ticket.Weight2Time = now;
        ticket.Weight2UpdatedAt = now;
        ticket.Weight2Mode = request.Mode;
        ticket.Weight2IsStable = request.IsStable;
        ticket.NetWeight = Math.Abs((ticket.Weight1 ?? 0) - request.Weight);
        ticket.IsOverWeight = true; // Still marked as overweight
        ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
        ticket.Status = TicketStatus.TICKET_COMPLETED;
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = _userContext.Username;
        ticket.AppVersion = _versionProvider.GetVersion();

        if (deliveryTicket != null)
        {
            deliveryTicket.IsOverWeight = true;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
        }

        // Update Registration
        reg.RegistrationStatus = RegistrationStatus.COMPLETED;
        reg.HasOverweightCase = true;
        reg.SyncStatus = SyncStatus.SYNC_QUEUED;
        reg.UpdatedAt = now;
        reg.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _ticketRepo.UpdateAsync(ticket, innerCt);
            if (deliveryTicket != null)
            {
                await _deliveryTicketRepo.UpdateAsync(deliveryTicket, innerCt);
            }
            await _regRepo.UpdateAsync(reg, innerCt);

            // Enqueue outbox records
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = reg.Id,
                AggregateType = nameof(VehicleRegistration),
                PayloadJson = _payloadFactory.CreatePayload(reg),
                IdempotencyKey = reg.IdempotencyKey,
                Status = OutboxStatus.PENDING,
                CreatedAt = now
            }, innerCt);

            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = ticket.Id,
                AggregateType = nameof(WeighTicket),
                PayloadJson = _payloadFactory.CreatePayload(ticket),
                IdempotencyKey = ticket.IdempotencyKey,
                Status = OutboxStatus.PENDING,
                CreatedAt = now
            }, innerCt);
        }, ct);

        if (deliveryTicket == null)
        {
            await _ensurePrimaryDeliveryTicketUseCase.ExecuteAsync(reg.Id, ct);
        }

        await _audit.LogAsync("COMPLETE_OVERWEIGHT_WITHOUT_SPLIT", nameof(VehicleRegistration), reg.Id,
            new { ticket.Weight2, ticket.NetWeight }, ct);

        return OperationResult<WeighTicket>.Ok(ticket);
    }
}

public sealed record CompleteOverweightTicketWithoutSplitRequest(
    Guid RegistrationId,
    decimal Weight,
    bool IsStable,
    WeightMode Mode);
