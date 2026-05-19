using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Exceptions;

namespace StationApp.Application.UseCases;

public sealed class CaptureWeight2UseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _ticketRepo;
    private readonly ISyncOutboxRepository _outboxRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly ISyncPayloadFactory _payloadFactory;

    public CaptureWeight2UseCase(
        ICutOrderRepository regRepo,
        IWeighTicketRepository ticketRepo,
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
        _outboxRepo = outboxRepo;
        _uow = uow;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _payloadFactory = payloadFactory;
    }

    public async Task<OperationResult<WeighTicket>> ExecuteAsync(CaptureWeightRequest request, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new Exception($"Cut order {request.CutOrderId} not found");

        if (reg.CurrentPrimaryWeighTicketId == null)
            throw new InvalidOperationException("No primary weigh ticket found for this registration. Capture Weight 1 first.");

        var ticket = await _ticketRepo.GetPrimaryByCutOrderIdAsync(reg.Id, ct)
            ?? throw new Exception($"Weigh Ticket {reg.CurrentPrimaryWeighTicketId} not found");

        // Guard
        if (reg.CutOrderStatus != CutOrderStatus.LOADING_IN_PROGRESS)
        {
            throw new InvalidOperationException($"Cannot capture Weight 2 when status is {reg.CutOrderStatus}");
        }

        var now = _clock.NowLocal;
        ticket.Weight2 = request.Weight;
        ticket.Weight2User = _userContext.Username;
        ticket.Weight2Time = now;
        ticket.Weight2UpdatedAt = now;
        ticket.Weight2Mode = request.Mode;
        ticket.Weight2IsStable = request.IsStable;

        // Calculate NetWeight
        ticket.NetWeight = Math.Abs((ticket.Weight1 ?? 0) - (ticket.Weight2 ?? 0));
        ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
        ticket.AppVersion = _versionProvider.GetVersion();
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = _userContext.Username;

        // No longer transitioning to OVERWEIGHT_PENDING_ACTION. 
        // We will leave the state as LOADING_IN_PROGRESS. The UI flow completes it.
        ticket.IsOverWeight = false; 


        reg.SyncStatus = SyncStatus.SYNC_QUEUED;
        reg.UpdatedAt = now;
        reg.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _ticketRepo.UpdateAsync(ticket, innerCt);
            await _regRepo.UpdateAsync(reg, innerCt);

            // CutOrder outbox
            await _outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = reg.Id,
                AggregateType = nameof(CutOrder),
                PayloadJson = _payloadFactory.CreatePayload(reg),
                IdempotencyKey = reg.IdempotencyKey,
                Status = OutboxStatus.PENDING,
                CreatedAt = now
            }, innerCt);

            // WeighTicket outbox
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

        await _audit.LogAsync("CAPTURE_WEIGHT_2", nameof(CutOrder), reg.Id,
            new { ticket.Weight2, ticket.Weight2IsStable, ticket.Weight2Mode, isOverweight = false }, ct);

        return OperationResult<WeighTicket>.Ok(ticket);
    }
}




