using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CancelTicketUseCase
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

    public CancelTicketUseCase(
        IVehicleRegistrationRepository regRepo,
        IWeighTicketRepository ticketRepo,
        IDeliveryTicketRepository deliveryTicketRepo,
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
        _outboxRepo = outboxRepo;
        _uow = uow;
        _versionProvider = versionProvider;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
        _payloadFactory = payloadFactory;
    }

    public async Task<OperationResult<VehicleRegistration>> ExecuteAsync(CancelTicketRequest request, CancellationToken ct)
    {
        var reg = await _regRepo.GetByIdAsync(request.RegistrationId, ct)
            ?? throw new Exception($"Vehicle Registration {request.RegistrationId} not found");

        if (reg.RegistrationStatus == RegistrationStatus.CANCELLED)
        {
            throw new InvalidOperationException("Phiếu đã hủy.");
        }

        if (reg.RegistrationStatus == RegistrationStatus.COMPLETED)
        {
            throw new InvalidOperationException("Phiếu đã hoàn thành, không thể hủy.");
        }

        if (reg.RegistrationStatus != RegistrationStatus.REGISTERED
            && reg.RegistrationStatus != RegistrationStatus.LOADING_IN_PROGRESS)
        {
            throw new InvalidOperationException("Phiếu ở trạng thái hiện tại không thể hủy.");
        }

        var now = _clock.NowLocal;
        var currentUser = _userContext.Username;
        var weighTickets = await _ticketRepo.GetAllByVehicleRegistrationIdAsync(reg.Id, ct);
        var deliveryTickets = await _deliveryTicketRepo.GetAllByVehicleRegistrationIdAsync(reg.Id, ct);

        reg.RegistrationStatus = RegistrationStatus.CANCELLED;
        reg.IsCancelled = true;
        reg.SyncStatus = SyncStatus.SYNC_QUEUED;
        reg.CurrentPrimaryWeighTicketId = null;
        reg.CurrentPrimaryDeliveryTicketId = null;
        reg.UpdatedAt = now;
        reg.UpdatedBy = currentUser;

        foreach (var ticket in weighTickets)
        {
            ticket.Status = TicketStatus.TICKET_CANCELLED;
            ticket.IsCancelled = true;
            ticket.IsDeleted = true;
            ticket.DeletedAt = now;
            ticket.DeletedBy = currentUser;
            ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = currentUser;
        }

        foreach (var ticket in deliveryTickets)
        {
            ticket.IsDeleted = true;
            ticket.DeletedAt = now;
            ticket.DeletedBy = currentUser;
            ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = currentUser;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _regRepo.UpdateAsync(reg, innerCt);
            foreach (var ticket in weighTickets)
            {
                await _ticketRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var ticket in deliveryTickets)
            {
                await _deliveryTicketRepo.UpdateAsync(ticket, innerCt);
            }

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

            foreach (var ticket in weighTickets)
            {
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
            }
        }, ct);

        await _audit.LogAsync("CANCEL_VEHICLE_REGISTRATION", nameof(VehicleRegistration), reg.Id,
            new { Reason = "User cancelled" }, ct);

        return OperationResult<VehicleRegistration>.Ok(reg);
    }
}
