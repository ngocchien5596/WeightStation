using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CancelWeighingSessionUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CancelWeighingSessionUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(CancelWeighingSessionRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (session.SessionStatus == WeighingSessionStatus.COMPLETED)
        {
            throw new InvalidOperationException("Lượt cân đã hoàn tất, không thể hủy.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var now = _clock.NowLocal;

        session.SessionStatus = WeighingSessionStatus.CANCELLED;
        session.IsCancelled = true;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        foreach (var line in lines)
        {
            line.LineStatus = WeighingSessionLineStatus.CANCELLED;
            line.UpdatedAt = now;
            line.UpdatedBy = _userContext.Username;
        }

        foreach (var registration in registrations)
        {
            if (registration.IsExportScale)
            {
                registration.CutOrderStatus = CutOrderStatus.IN_SESSION;
                registration.ProcessingStage = ProcessingStage.WEIGHING;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }
            else
            {
                registration.CutOrderStatus = CutOrderStatus.REGISTERED;
                registration.ProcessingStage = ProcessingStage.IN_YARD;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }

            registration.WeighingSessionId = null;
            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        foreach (var weighTicket in weighTickets)
        {
            weighTicket.IsDeleted = true;
            weighTicket.IsCancelled = true;
            weighTicket.Status = TicketStatus.TICKET_CANCELLED;
            weighTicket.DeletedAt = now;
            weighTicket.DeletedBy = _userContext.Username;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var deliveryTicket in deliveryTickets)
        {
            deliveryTicket.IsDeleted = true;
            deliveryTicket.DeletedAt = now;
            deliveryTicket.DeletedBy = _userContext.Username;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }

            foreach (var weighTicket in weighTickets)
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }
        }, ct);
    }
}
