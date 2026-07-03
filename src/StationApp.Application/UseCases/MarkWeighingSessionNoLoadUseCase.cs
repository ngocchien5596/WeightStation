using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class MarkWeighingSessionNoLoadUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly WeighingSessionTicketSyncService _ticketSyncService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public MarkWeighingSessionNoLoadUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        WeighingSessionTicketSyncService ticketSyncService,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _ticketSyncService = ticketSyncService;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(MarkWeighingSessionNoLoadRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (session.SessionStatus == WeighingSessionStatus.CANCELLED)
        {
            throw new InvalidOperationException("Lượt cân hiện tại không thể chuyển xe ra theo luồng không lấy hàng.");
        }

        if (session.IsNoLoad)
        {
            return;
        }

        if (session.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT1 or WeighingSessionStatus.PENDING_WEIGHT2)
        {
            throw new InvalidOperationException("Phải lưu cân lần 2 trước khi đánh dấu không lấy hàng.");
        }

        if (!session.Weight2.HasValue)
        {
            throw new InvalidOperationException("Lượt cân hiện tại chưa có số cân lần 2.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var registrations = await _regRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var now = _clock.NowLocal;
        var masterWeighTicket = weighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession);

        session.SessionStatus = WeighingSessionStatus.COMPLETED;
        session.NetWeight = 0m;
        session.Ttcp10WeightSnapshot ??= 0m;
        session.IsOverweight = false;
        session.OverweightAmount = 0m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
        session.OverweightResolvedAt = null;
        session.OverweightResolvedBy = null;
        session.IsNoLoad = true;
        session.HasPrintedMasterWeighTicket = false;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

        foreach (var line in lines)
        {
            line.ActualAllocatedWeight = 0m;
            line.ActualAllocatedBagCount = 0;
            line.BagCountDisplay = 0;
            line.IsReturnedBrokenTrip = false;
            line.LineStatus = WeighingSessionLineStatus.ALLOCATED;
            line.HasPrintedDeliveryTicket = false;
            line.UpdatedAt = now;
            line.UpdatedBy = _userContext.Username;
        }

        foreach (var registration in registrations)
        {
            if (registration.IsExportScale)
            {
                registration.CutOrderStatus = CutOrderStatus.IN_SESSION;
                registration.ProcessingStage = ProcessingStage.WEIGHING;
                registration.WeighingSessionId = null;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }
            else
            {
                registration.CutOrderStatus = CutOrderStatus.COMPLETED;
                registration.ProcessingStage = ProcessingStage.OUT_YARD;
                registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            }

            registration.CurrentPrimaryWeighTicketId = masterWeighTicket?.Id;
            registration.CurrentPrimaryDeliveryTicketId = null;
            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        if (masterWeighTicket != null)
        {
            _ticketSyncService.SyncMasterTicketFromSession(session, masterWeighTicket, now, _userContext.Username);
            masterWeighTicket.IsDeleted = false;
            masterWeighTicket.IsCancelled = false;
            masterWeighTicket.Status = TicketStatus.TICKET_COMPLETED;
            masterWeighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            masterWeighTicket.DeletedAt = null;
            masterWeighTicket.DeletedBy = null;
            masterWeighTicket.UpdatedAt = now;
            masterWeighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var weighTicket in weighTickets)
        {
            if (masterWeighTicket != null && weighTicket.Id == masterWeighTicket.Id)
            {
                continue;
            }

            weighTicket.IsDeleted = true;
            weighTicket.IsCancelled = true;
            weighTicket.Status = TicketStatus.TICKET_CANCELLED;
            weighTicket.NetWeight = 0m;
            weighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            weighTicket.DeletedAt = now;
            weighTicket.DeletedBy = _userContext.Username;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var deliveryTicket in deliveryTickets)
        {
            deliveryTicket.IsDeleted = true;
            deliveryTicket.AllocatedWeight = 0m;
            deliveryTicket.AllocatedBagCount = 0;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
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
