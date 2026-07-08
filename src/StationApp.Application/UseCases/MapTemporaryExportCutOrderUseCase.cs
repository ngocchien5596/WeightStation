using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class MapTemporaryExportCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public MapTemporaryExportCutOrderUseCase(
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

    public async Task ExecuteAsync(MapTemporaryExportCutOrderRequest request, CancellationToken ct)
    {
        var temporaryCutOrder = await _cutOrderRepo.GetByIdAsync(request.TemporaryCutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh tạm.");
        var realCutOrder = await _cutOrderRepo.GetByIdAsync(request.RealCutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh thật.");

        ValidateRealCutOrder(realCutOrder);

        if (temporaryCutOrder.Id == realCutOrder.Id)
        {
            return;
        }

        var trips = await _cutOrderRepo.GetExportVehicleTripsAsync(temporaryCutOrder.Id, ct);
        NormalizeRecoverableTemporaryCutOrderState(temporaryCutOrder, trips.Count);
        ValidateTemporaryCutOrder(temporaryCutOrder);

        var sessions = new List<WeighingSession>();
        var lines = new List<WeighingSessionLine>();
        var weighTickets = new List<WeighTicket>();
        var deliveryTickets = new List<DeliveryTicket>();

        foreach (var trip in trips)
        {
            var session = await _sessionRepo.GetByIdAsync(trip.SessionId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy chuyến xe thuộc cắt lệnh tạm.");
            var sessionLines = (await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct))
                .Where(x => !x.IsDeleted && x.CutOrderId == temporaryCutOrder.Id)
                .ToList();

            if (sessionLines.Count != 1)
            {
                throw new InvalidOperationException("Chỉ hỗ trợ map chuyến xe xuất khẩu có đúng 1 dòng cắt lệnh.");
            }

            sessions.Add(session);
            lines.Add(sessionLines[0]);
            weighTickets.AddRange((await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct)).Where(x => !x.IsDeleted));
            deliveryTickets.AddRange((await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct)).Where(x => !x.IsDeleted));
        }

        var now = _clock.NowLocal;
        var username = _userContext.Username;
        var targetExistingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(realCutOrder.Id, ct);
        var targetExistingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(realCutOrder.Id, ct);

        realCutOrder.IsExportScale = true;
        realCutOrder.IsTemporaryExport = false;
        realCutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        realCutOrder.ProcessingStage = ProcessingStage.WEIGHING;
        realCutOrder.WeighingSessionId = null;
        realCutOrder.ExportStartedAt ??= now;
        realCutOrder.ExportStartedBy ??= username;
        realCutOrder.TareWeightKg ??= temporaryCutOrder.TareWeightKg;
        realCutOrder.BagWeightKg ??= temporaryCutOrder.BagWeightKg;
        realCutOrder.BagCount ??= temporaryCutOrder.BagCount;
        realCutOrder.ExportPackageType ??= temporaryCutOrder.ExportPackageType;
        realCutOrder.MappedTemporaryCutOrderId = temporaryCutOrder.Id;
        realCutOrder.MappedAt = now;
        realCutOrder.MappedBy = username;
        realCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        realCutOrder.UpdatedAt = now;
        realCutOrder.UpdatedBy = username;

        temporaryCutOrder.MappedRealCutOrderId = realCutOrder.Id;
        temporaryCutOrder.MappedAt = now;
        temporaryCutOrder.MappedBy = username;
        temporaryCutOrder.CutOrderStatus = CutOrderStatus.COMPLETED;
        temporaryCutOrder.ProcessingStage = ProcessingStage.OUT_YARD;
        temporaryCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        temporaryCutOrder.UpdatedAt = now;
        temporaryCutOrder.UpdatedBy = username;

        var targetPlannedWeight = ResolveTargetPlannedWeight(realCutOrder);
        foreach (var line in lines)
        {
            line.CutOrderId = realCutOrder.Id;
            line.CustomerCode = realCutOrder.CustomerCode;
            line.CustomerName = realCutOrder.CustomerName;
            line.DistributorCode = realCutOrder.CustomerCode;
            line.DistributorName = realCutOrder.CustomerName;
            line.ProductCode = realCutOrder.ProductCode;
            line.ProductName = realCutOrder.ProductName;
            line.PlannedWeight = targetPlannedWeight;
            line.PlannedBagCount = realCutOrder.BagCount;
            line.SyncStatus = SyncStatus.SYNC_QUEUED;
            line.LastSyncAttemptAt = null;
            line.LastSyncError = null;
            line.UpdatedAt = now;
            line.UpdatedBy = username;
        }

        foreach (var session in sessions)
        {
            session.SyncStatus = SyncStatus.SYNC_QUEUED;
            session.LastSyncAttemptAt = null;
            session.LastSyncError = null;
            session.UpdatedAt = now;
            session.UpdatedBy = username;
        }

        foreach (var ticket in weighTickets)
        {
            ticket.CutOrderId = realCutOrder.Id;
            ticket.ErpCutOrderId = realCutOrder.ErpCutOrderId;
            ticket.CustomerCode = realCutOrder.CustomerCode;
            ticket.CustomerName = realCutOrder.CustomerName;
            ticket.ProductCode = realCutOrder.ProductCode;
            ticket.ProductName = realCutOrder.ProductName;
            ticket.PlannedWeight = targetPlannedWeight;
            ticket.BagCount = realCutOrder.BagCount;
            ticket.Notes = realCutOrder.Notes;
            ticket.TransportMethod = realCutOrder.TransportMethod;
            ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = username;
        }

        foreach (var ticket in deliveryTickets)
        {
            ticket.CutOrderId = realCutOrder.Id;
            ticket.ErpCutOrderId = realCutOrder.ErpCutOrderId ?? string.Empty;
            ticket.CustomerCode = realCutOrder.CustomerCode;
            ticket.ProductCode = realCutOrder.ProductCode;
            ticket.Notes = realCutOrder.Notes;
            ticket.SyncStatus = SyncStatus.SYNC_QUEUED;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = username;
        }

        realCutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(targetExistingWeighTickets.Concat(weighTickets))?.Id;
        realCutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(targetExistingDeliveryTickets.Concat(deliveryTickets))?.Id;
        temporaryCutOrder.CurrentPrimaryWeighTicketId = null;
        temporaryCutOrder.CurrentPrimaryDeliveryTicketId = null;

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var session in sessions)
            {
                await _sessionRepo.UpdateAsync(session, innerCt);
            }

            foreach (var line in lines)
            {
                await _sessionRepo.UpdateLineAsync(line, innerCt);
            }

            foreach (var ticket in weighTickets)
            {
                await _weighRepo.UpdateAsync(ticket, innerCt);
            }

            foreach (var ticket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(ticket, innerCt);
            }

            await _cutOrderRepo.UpdateAsync(temporaryCutOrder, innerCt);
            await _cutOrderRepo.UpdateAsync(realCutOrder, innerCt);
        }, ct);
    }

    private static void ValidateTemporaryCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsTemporaryExport || !cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn không phải cắt lệnh xuất khẩu tạm.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh tạm đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND
            || cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION
            || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("Cắt lệnh tạm không ở trạng thái cân xuất khẩu đang hoạt động.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue)
        {
            throw new InvalidOperationException("Cắt lệnh tạm đã chốt tổng, không thể map.");
        }
    }

    private static void NormalizeRecoverableTemporaryCutOrderState(CutOrder cutOrder, int tripCount)
    {
        if (!cutOrder.IsTemporaryExport
            || !cutOrder.IsExportScale
            || cutOrder.IsDeleted
            || cutOrder.IsCancelled
            || cutOrder.ExportFinalizedAt.HasValue
            || cutOrder.TransactionType != TransactionType.OUTBOUND
            || tripCount <= 0)
        {
            return;
        }

        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
    }

    private static void ValidateRealCutOrder(CutOrder cutOrder)
    {
        if (cutOrder.IsTemporaryExport)
        {
            throw new InvalidOperationException("Cắt lệnh đích phải là cắt lệnh thật từ ERP.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh thật đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ map sang cắt lệnh xuất hàng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Cắt lệnh thật đã chốt tổng, không thể map.");
        }

        if (cutOrder.IsExportScale)
        {
            if (cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
            {
                throw new InvalidOperationException("Cắt lệnh thật không ở trạng thái cân xuất khẩu.");
            }

            return;
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.REGISTERED || cutOrder.ProcessingStage != ProcessingStage.IN_YARD)
        {
            throw new InvalidOperationException("Cắt lệnh thật không còn ở trạng thái xe vào để map sang cân xuất khẩu.");
        }

        if (cutOrder.WeighingSessionId.HasValue)
        {
            throw new InvalidOperationException("Cắt lệnh thật đã thuộc một lượt cân khác.");
        }
    }

    private static decimal? ResolveTargetPlannedWeight(CutOrder cutOrder)
        => cutOrder.PlannedWeight;

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
