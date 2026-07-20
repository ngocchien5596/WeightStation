using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed record DeleteSessionWeight2Request(Guid SessionId, string Reason);

public sealed class DeleteSessionWeight2UseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditService _audit;

    public DeleteSessionWeight2UseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository cutOrderRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditService audit)
    {
        _sessionRepo = sessionRepo;
        _cutOrderRepo = cutOrderRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _audit = audit;
    }

    public async Task ExecuteAsync(DeleteSessionWeight2Request request, CancellationToken ct)
    {
        if (!StationAuthorization.CanDeleteWeight2(_userContext.RoleCode))
        {
            throw new UnauthorizedAccessException("Chỉ Quản lý hoặc Quản trị hệ thống được xóa lượt cân lần 2.");
        }

        var reason = NormalizeReason(request.Reason);
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân cần xóa cân lần 2.");

        if (!string.IsNullOrWhiteSpace(_userContext.StationCode)
            && !string.Equals(session.StationCode, _userContext.StationCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Không được xóa cân lần 2 của lượt cân khác trạm đang thao tác.");
        }

        if (session.IsDeleted || session.IsCancelled)
        {
            throw new InvalidOperationException("Lượt cân không còn hợp lệ để xóa cân lần 2.");
        }

        if (session.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ xóa cân lần 2 cho lượt cân xuất hàng nội địa hoặc xuất khẩu.");
        }

        if (!session.Weight2.HasValue && !session.Weight2Time.HasValue)
        {
            throw new InvalidOperationException("Lượt cân chưa có cân lần 2 để xóa.");
        }

        var cutOrders = (await _cutOrderRepo.GetByWeighingSessionIdAsync(session.Id, ct))
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .ToList();
        if (cutOrders.Count == 0 || cutOrders.Any(x => x.TransactionType != TransactionType.OUTBOUND))
        {
            throw new InvalidOperationException("Lượt cân không thuộc luồng xuất hàng hợp lệ.");
        }

        if (cutOrders.Any(IsLockedExportCutOrder))
        {
            throw new InvalidOperationException("Không thể xóa lượt cân lần 2 vì cắt lệnh xuất khẩu đã chốt tổng hoặc đã hoàn tất ERP.");
        }

        var lines = (await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct))
            .Where(x => !x.IsDeleted)
            .ToList();
        var weighTickets = (await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct))
            .Where(x => !x.IsDeleted)
            .ToList();
        var deliveryTickets = (await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct))
            .Where(x => !x.IsDeleted)
            .ToList();

        var now = _clock.NowLocal;
        var username = _userContext.Username;
        var oldWeight2 = session.Weight2;
        var oldWeight2Time = session.Weight2Time;
        var oldNetWeight = session.NetWeight;
        var oldStatus = session.SessionStatus;
        var printedWeighTickets = weighTickets.Where(x => x.IsPrinted).Select(x => x.TicketNo).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var printedDeliveryTickets = deliveryTickets.Where(x => x.IsPrinted).Select(x => x.DeliveryNo).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var deletedDeliveryNos = deliveryTickets.Select(x => x.DeliveryNo).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var cancelledTicketNos = weighTickets
            .Where(x => IsDerivedWeighTicket(x))
            .Select(x => x.TicketNo)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        ResetSession(session, now, username);
        foreach (var ticket in weighTickets)
        {
            if (string.Equals(ticket.RecordRole, WeighTicketRecordRoles.MasterSession, StringComparison.OrdinalIgnoreCase))
            {
                ResetMasterTicket(ticket, now, username);
            }
            else if (IsDerivedWeighTicket(ticket))
            {
                CancelDerivedTicket(ticket, now, username);
            }
        }

        foreach (var line in lines)
        {
            ResetLine(line, now, username);
        }

        foreach (var deliveryTicket in deliveryTickets)
        {
            DeleteDeliveryTicket(deliveryTicket, now, username);
        }

        foreach (var cutOrder in cutOrders)
        {
            var allCutOrderWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(cutOrder.Id, ct);
            var allCutOrderDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(cutOrder.Id, ct);

            if (cutOrder.CurrentPrimaryWeighTicketId.HasValue
                && weighTickets.Any(x => x.Id == cutOrder.CurrentPrimaryWeighTicketId.Value && x.IsDeleted))
            {
                cutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(allCutOrderWeighTickets)?.Id;
            }

            if (cutOrder.CurrentPrimaryDeliveryTicketId.HasValue
                && deliveryTickets.Any(x => x.Id == cutOrder.CurrentPrimaryDeliveryTicketId.Value && x.IsDeleted))
            {
                cutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(allCutOrderDeliveryTickets)?.Id;
            }

            if (!cutOrder.IsExportScale && cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
            {
                cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
                cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
            }

            cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
            cutOrder.LastSyncAttemptAt = null;
            cutOrder.LastSyncError = null;
            cutOrder.UpdatedAt = now;
            cutOrder.UpdatedBy = username;
        }

        var auditDetail = new AuditLogDetailBuilder()
            .WithSubject(nameof(WeighingSession.SessionNo), session.SessionNo)
            .WithSubject(nameof(WeighingSession.VehiclePlate), session.VehiclePlate)
            .WithSubject(nameof(WeighingSession.MoocNumber), session.MoocNumber)
            .WithSubject(nameof(WeighingSession.StationCode), session.StationCode)
            .WithReason(reason)
            .AddChange(nameof(WeighingSession.Weight2), oldWeight2, null, "kg")
            .AddChange(nameof(WeighingSession.Weight2Time), oldWeight2Time, null)
            .AddChange(nameof(WeighingSession.NetWeight), oldNetWeight, null, "kg")
            .AddChange(nameof(WeighingSession.SessionStatus), oldStatus.ToString(), session.SessionStatus.ToString())
            .WithSummary("CutOrderCodes", cutOrders.Select(x => x.ErpCutOrderId).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray())
            .WithSummary("DeletedDeliveryTickets", deletedDeliveryNos)
            .WithSummary("CancelledWeighTickets", cancelledTicketNos)
            .WithSummary("PrintedWeighTickets", printedWeighTickets)
            .WithSummary("PrintedDeliveryTickets", printedDeliveryTickets)
            .AddNote("Xóa lượt cân lần 2, đưa lượt cân về trạng thái chờ cân lần 2.")
            .Build();

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            foreach (var ticket in weighTickets)
            {
                await _weighRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            foreach (var cutOrder in cutOrders)
            {
                await _cutOrderRepo.UpdateAsync(cutOrder, innerCt);
            }
        }, ct);

        await _audit.LogAsync("DELETE_WEIGHT_2", nameof(WeighingSession), session.Id, auditDetail, ct);
    }

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Vui lòng nhập lý do xóa lượt cân lần 2.");
        }

        return reason.Trim();
    }

    private static bool IsLockedExportCutOrder(CutOrder cutOrder)
        => cutOrder.IsExportScale
            && (cutOrder.ExportFinalizedAt.HasValue
                || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED
                || cutOrder.ErpExportCompleted);

    private static bool IsDerivedWeighTicket(WeighTicket ticket)
        => string.Equals(ticket.RecordRole, WeighTicketRecordRoles.CutOrderDerived, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ticket.RecordRole, WeighTicketRecordRoles.SplitDerived, StringComparison.OrdinalIgnoreCase);

    private static void ResetSession(WeighingSession session, DateTime now, string username)
    {
        session.Weight2 = null;
        session.Weight2Time = null;
        session.NetWeight = null;
        session.SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2;
        session.IsOverweight = false;
        session.OverweightAmount = 0m;
        session.OverweightResolutionStatus = OverweightResolutionStatus.NOT_APPLICABLE;
        session.OverweightResolvedAt = null;
        session.OverweightResolvedBy = null;
        session.SyncStatus = SyncStatus.SYNC_QUEUED;
        session.LastSyncAttemptAt = null;
        session.LastSyncError = null;
        session.UpdatedAt = now;
        session.UpdatedBy = username;
    }

    private static void ResetMasterTicket(WeighTicket ticket, DateTime now, string username)
    {
        ticket.Weight2 = null;
        ticket.Weight2Time = null;
        ticket.Weight2User = null;
        ticket.Weight2Mode = null;
        ticket.Weight2IsStable = null;
        ticket.Weight2UpdatedAt = null;
        ticket.NetWeight = null;
        ticket.IsOverWeight = false;
        ticket.Status = TicketStatus.LOADING_STARTED;
        ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = username;
    }

    private static void CancelDerivedTicket(WeighTicket ticket, DateTime now, string username)
    {
        ticket.IsDeleted = true;
        ticket.IsCancelled = true;
        ticket.Status = TicketStatus.TICKET_CANCELLED;
        ticket.NetWeight = 0m;
        ticket.DeletedAt = now;
        ticket.DeletedBy = username;
        ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = username;
    }

    private static void ResetLine(WeighingSessionLine line, DateTime now, string username)
    {
        line.ActualAllocatedWeight = null;
        line.ActualAllocatedBagCount = null;
        line.BagCountDisplay = null;
        line.SystemCalculatedBagCount = null;
        line.BagCountConfirmedAt = null;
        line.BagCountConfirmedBy = null;
        line.BagCountConfirmationMode = null;
        line.Note = null;
        line.IsReturnedBrokenTrip = false;
        line.LineStatus = WeighingSessionLineStatus.PENDING;
        line.DeliveryTicketId = null;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = username;
    }

    private static void DeleteDeliveryTicket(DeliveryTicket ticket, DateTime now, string username)
    {
        ticket.IsDeleted = true;
        ticket.DeletedAt = now;
        ticket.DeletedBy = username;
        ticket.AllocatedWeight = 0m;
        ticket.AllocatedBagCount = 0;
        ticket.IsOverWeight = false;
        ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = username;
    }

    private static WeighTicket? SelectPrimaryWeighTicket(IEnumerable<WeighTicket> tickets)
        => tickets
            .Where(x => !x.IsDeleted && !x.IsCancelled)
            .OrderByDescending(x => x.Weight2Time ?? x.Weight1Time ?? x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();

    private static DeliveryTicket? SelectPrimaryDeliveryTicket(IEnumerable<DeliveryTicket> tickets)
        => tickets
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
}
