using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class DeleteExportVehicleTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public DeleteExportVehicleTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến xe cần xóa.");

        if (session.IsDeleted)
        {
            return;
        }

        if (session.Weight2.HasValue || session.Weight2Time.HasValue)
        {
            throw new InvalidOperationException("Không thể xóa chuyến xe đã hoàn thành cân lần 2.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var activeLines = lines.Where(x => !x.IsDeleted).ToList();
        if (activeLines.Count != 1)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ xóa chuyến xuất khẩu có đúng 1 dòng cắt lệnh.");
        }

        var line = activeLines[0];
        var cutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh nguồn của chuyến xe.");

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn đã bị hủy hoặc xóa.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể xóa chuyến xe thuộc cắt lệnh đã chốt.");
        }

        var weighTickets = (await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct))
            .Where(x => !x.IsDeleted)
            .ToList();
        var deliveryTickets = (await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct))
            .Where(x => !x.IsDeleted)
            .ToList();
        var existingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(cutOrder.Id, ct);
        var existingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(cutOrder.Id, ct);

        var now = _clock.NowLocal;
        var username = _userContext.Username;

        session.IsDeleted = true;
        session.IsCancelled = true;
        session.SessionStatus = WeighingSessionStatus.CANCELLED;
        session.DeletedAt = now;
        session.DeletedBy = username;
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.LastSyncAttemptAt = null;
        session.LastSyncError = null;
        session.UpdatedAt = now;
        session.UpdatedBy = username;

        line.IsDeleted = true;
        line.DeletedAt = now;
        line.DeletedBy = username;
        line.LineStatus = WeighingSessionLineStatus.CANCELLED;
        line.ActualAllocatedWeight = null;
        line.ActualAllocatedBagCount = null;
        line.BagCountDisplay = null;
        line.IsReturnedBrokenTrip = false;
        line.DeliveryTicketId = null;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = username;

        foreach (var weighTicket in weighTickets)
        {
            weighTicket.IsDeleted = true;
            weighTicket.IsCancelled = true;
            weighTicket.Status = TicketStatus.TICKET_CANCELLED;
            weighTicket.NetWeight = 0m;
            weighTicket.DeletedAt = now;
            weighTicket.DeletedBy = username;
            weighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = username;
        }

        foreach (var deliveryTicket in deliveryTickets)
        {
            deliveryTicket.IsDeleted = true;
            deliveryTicket.AllocatedWeight = 0m;
            deliveryTicket.AllocatedBagCount = 0;
            deliveryTicket.DeletedAt = now;
            deliveryTicket.DeletedBy = username;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = username;
        }

        if (cutOrder.CurrentPrimaryWeighTicketId.HasValue && weighTickets.Any(x => x.Id == cutOrder.CurrentPrimaryWeighTicketId.Value))
        {
            cutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(
                existingWeighTickets.Where(x => weighTickets.All(deleted => deleted.Id != x.Id)))?.Id;
        }

        if (cutOrder.CurrentPrimaryDeliveryTicketId.HasValue && deliveryTickets.Any(x => x.Id == cutOrder.CurrentPrimaryDeliveryTicketId.Value))
        {
            cutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(
                existingDeliveryTickets.Where(x => deliveryTickets.All(deleted => deleted.Id != x.Id)))?.Id;
        }

        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = username;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            await _sessionRepo.UpdateLineAsync(line, innerCt);

            foreach (var weighTicket in weighTickets)
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
        }, ct);
    }

    private static WeighTicket? SelectPrimaryWeighTicket(IEnumerable<WeighTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .OrderByDescending(x => x.Weight2Time ?? x.Weight1Time ?? x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }

    private static DeliveryTicket? SelectPrimaryDeliveryTicket(IEnumerable<DeliveryTicket> tickets)
    {
        return tickets
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }
}
