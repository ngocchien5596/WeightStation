using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Exceptions;

namespace StationApp.Application.UseCases;

public sealed class CompleteTicketUseCase
{
    private readonly IVehicleRegistrationRepository _regRepo;
    private readonly IWeighTicketRepository _ticketRepo;
    private readonly EnsurePrimaryDeliveryTicketUseCase _ensurePrimaryDeliveryTicketUseCase;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncPayloadFactory _payloadFactory;

    public CompleteTicketUseCase(
        IVehicleRegistrationRepository regRepo,
        IWeighTicketRepository ticketRepo,
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
        _ensurePrimaryDeliveryTicketUseCase = ensurePrimaryDeliveryTicketUseCase;
        _outboxRepo = outboxRepo;
        _uow = uow;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _payloadFactory = payloadFactory;
    }

    public async Task<OperationResult<WeighTicket>> ExecuteAsync(CompleteTicketRequest request, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(request.RegistrationId, ct)
            ?? throw new Exception($"Vehicle Registration {request.RegistrationId} not found");

        if (reg.RegistrationStatus != RegistrationStatus.LOADING_IN_PROGRESS)
        {
            throw new InvalidOperationException($"Cannot complete registration when status is {reg.RegistrationStatus}");
        }

        if (reg.CurrentPrimaryWeighTicketId == null)
            throw new InvalidOperationException("No primary weigh ticket found. Cannot complete.");

        var ticket = await _ticketRepo.GetPrimaryByVehicleRegistrationIdAsync(reg.Id, ct)
            ?? throw new Exception($"Weigh Ticket {reg.CurrentPrimaryWeighTicketId} not found");

        var now = _clock.NowLocal;
        ticket.Status = TicketStatus.TICKET_COMPLETED;
        ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
        ticket.IsOverWeight = false;
        ticket.AppVersion = _versionProvider.GetVersion();
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = _userContext.Username;

        reg.RegistrationStatus = RegistrationStatus.COMPLETED;
        reg.HasOverweightCase = false;
        reg.SyncStatus = SyncStatus.SYNC_QUEUED;
        reg.UpdatedAt = now;
        reg.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _ticketRepo.UpdateAsync(ticket, innerCt);
            await _regRepo.UpdateAsync(reg, innerCt);

            // Outbox for VehicleRegistration
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

            // Outbox for WeighTicket
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

        await _ensurePrimaryDeliveryTicketUseCase.ExecuteAsync(reg.Id, ct);

        await _audit.LogAsync("COMPLETE_TICKET", nameof(VehicleRegistration), reg.Id,
            new { ticket.NetWeight, ticket.Weight1, ticket.Weight2 }, ct);

        return OperationResult<WeighTicket>.Ok(ticket);
    }
}
