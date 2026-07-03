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

public sealed class TransferExportVehicleTripUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IWeighTicketRepository _weighRepo;
    private readonly IDeliveryTicketRepository _deliveryRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;
    private readonly IAuditLogRepository _auditLogRepository;

    public TransferExportVehicleTripUseCase(
        ICutOrderRepository cutOrderRepo,
        IWeighingSessionRepository sessionRepo,
        IWeighTicketRepository weighRepo,
        IDeliveryTicketRepository deliveryRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock,
        IAuditLogRepository auditLogRepository)
    {
        _cutOrderRepo = cutOrderRepo;
        _sessionRepo = sessionRepo;
        _weighRepo = weighRepo;
        _deliveryRepo = deliveryRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
        _auditLogRepository = auditLogRepository;
    }

    public async Task ExecuteAsync(TransferExportVehicleTripRequest request, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(request.SessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến xe cần chuyển.");

        if (session.SessionStatus == WeighingSessionStatus.CANCELLED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến xe đã bị hủy.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(session.Id, ct);
        var activeLines = lines.Where(x => !x.IsDeleted).ToList();
        if (activeLines.Count != 1)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ chuyển chuyến xe xuất khẩu có đúng 1 dòng cắt lệnh.");
        }

        var line = activeLines[0];
        var sourceCutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh nguồn của chuyến xe.");
        var targetCutOrder = await _cutOrderRepo.GetByIdAsync(request.TargetCutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh đích.");

        ValidateTransferSourceCutOrder(sourceCutOrder);
        ValidateTransferTargetCutOrder(targetCutOrder);

        if (sourceCutOrder.Id == targetCutOrder.Id)
        {
            return;
        }

        var now = _clock.NowLocal;
        var weighTickets = await _weighRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var deliveryTickets = await _deliveryRepo.GetByWeighingSessionIdAsync(session.Id, ct);
        var sourceExistingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(sourceCutOrder.Id, ct);
        var sourceExistingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(sourceCutOrder.Id, ct);
        var targetExistingWeighTickets = await _weighRepo.GetAllByCutOrderIdAsync(targetCutOrder.Id, ct);
        var targetExistingDeliveryTickets = await _deliveryRepo.GetAllByCutOrderIdAsync(targetCutOrder.Id, ct);
        var targetPlannedWeight = await ResolveRemainingPlannedWeightAsync(targetCutOrder, ct);

        line.CutOrderId = targetCutOrder.Id;
        line.CustomerCode = targetCutOrder.CustomerCode;
        line.CustomerName = targetCutOrder.CustomerName;
        line.DistributorCode = targetCutOrder.CustomerCode;
        line.DistributorName = targetCutOrder.CustomerName;
        line.ProductCode = targetCutOrder.ProductCode;
        line.ProductName = targetCutOrder.ProductName;
        line.PlannedWeight = targetPlannedWeight;
        line.PlannedBagCount = targetCutOrder.BagCount;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        foreach (var weighTicket in weighTickets.Where(x => !x.IsDeleted))
        {
            weighTicket.CutOrderId = targetCutOrder.Id;
            weighTicket.ErpCutOrderId = targetCutOrder.ErpCutOrderId;
            weighTicket.CustomerCode = targetCutOrder.CustomerCode;
            weighTicket.CustomerName = targetCutOrder.CustomerName;
            weighTicket.ProductCode = targetCutOrder.ProductCode;
            weighTicket.ProductName = targetCutOrder.ProductName;
            weighTicket.PlannedWeight = targetPlannedWeight;
            weighTicket.BagCount = targetCutOrder.BagCount;
            weighTicket.Notes = targetCutOrder.Notes;
            weighTicket.TransportMethod = targetCutOrder.TransportMethod;
            weighTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            weighTicket.UpdatedAt = now;
            weighTicket.UpdatedBy = _userContext.Username;
        }

        foreach (var deliveryTicket in deliveryTickets.Where(x => !x.IsDeleted))
        {
            deliveryTicket.CutOrderId = targetCutOrder.Id;
            deliveryTicket.ErpCutOrderId = targetCutOrder.ErpCutOrderId ?? string.Empty;
            deliveryTicket.CustomerCode = targetCutOrder.CustomerCode;
            deliveryTicket.ProductCode = targetCutOrder.ProductCode;
            deliveryTicket.Notes = targetCutOrder.Notes;
            deliveryTicket.SyncStatus = SyncStatus.SYNC_QUEUED;
            deliveryTicket.UpdatedAt = now;
            deliveryTicket.UpdatedBy = _userContext.Username;
        }

        var sourceCurrentPrimaryWeighTicketId = sourceCutOrder.CurrentPrimaryWeighTicketId;
        var sourceCurrentPrimaryDeliveryTicketId = sourceCutOrder.CurrentPrimaryDeliveryTicketId;

        sourceCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        sourceCutOrder.UpdatedAt = now;
        sourceCutOrder.UpdatedBy = _userContext.Username;

        targetCutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        targetCutOrder.UpdatedAt = now;
        targetCutOrder.UpdatedBy = _userContext.Username;
        targetCutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(targetExistingWeighTickets.Concat(weighTickets))?.Id;
        targetCutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(targetExistingDeliveryTickets.Concat(deliveryTickets))?.Id;

        if (sourceCurrentPrimaryWeighTicketId.HasValue && weighTickets.Any(x => x.Id == sourceCurrentPrimaryWeighTicketId.Value))
        {
            sourceCutOrder.CurrentPrimaryWeighTicketId = SelectPrimaryWeighTicket(
                sourceExistingWeighTickets.Where(x => weighTickets.All(moved => moved.Id != x.Id)))?.Id;
        }

        if (sourceCurrentPrimaryDeliveryTicketId.HasValue && deliveryTickets.Any(x => x.Id == sourceCurrentPrimaryDeliveryTicketId.Value))
        {
            sourceCutOrder.CurrentPrimaryDeliveryTicketId = SelectPrimaryDeliveryTicket(
                sourceExistingDeliveryTickets.Where(x => deliveryTickets.All(moved => moved.Id != x.Id)))?.Id;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateLineAsync(line, innerCt);

            foreach (var weighTicket in weighTickets)
            {
                await _weighRepo.UpdateAsync(weighTicket, innerCt);
            }

            foreach (var deliveryTicket in deliveryTickets)
            {
                await _deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            await _cutOrderRepo.UpdateAsync(sourceCutOrder, innerCt);
            await _cutOrderRepo.UpdateAsync(targetCutOrder, innerCt);
        }, ct);

        var sourceErpId = sourceCutOrder.ErpCutOrderId;
        var targetErpId = targetCutOrder.ErpCutOrderId;

        var sourceDisplayCode = sourceCutOrder.IsTemporaryExport
            ? sourceCutOrder.TemporaryExportDisplayCode ?? sourceErpId
            : sourceErpId;
        var targetDisplayCode = targetCutOrder.IsTemporaryExport
            ? targetCutOrder.TemporaryExportDisplayCode ?? targetErpId
            : targetErpId;

        var auditDetail = new
        {
            SessionNo = session.SessionNo,
            SourceCutOrderId = sourceCutOrder.Id,
            SourceErpCutOrderId = sourceErpId,
            SourceDisplayCode = sourceDisplayCode,
            TargetCutOrderId = targetCutOrder.Id,
            TargetErpCutOrderId = targetErpId,
            TargetDisplayCode = targetDisplayCode,
            VehiclePlate = session.VehiclePlate,
            Weight1 = session.Weight1,
            Weight2 = session.Weight2,
            NetWeight = session.NetWeight,
            Reason = $"Chuyển chuyến từ cắt lệnh {sourceDisplayCode ?? sourceCutOrder.Id.ToString()} sang {targetDisplayCode ?? targetCutOrder.Id.ToString()}"
        };

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = "TRANSFER_EXPORT_TRIP",
            EntityType = "WeighingSession",
            EntityId = session.Id,
            DetailJson = System.Text.Json.JsonSerializer.Serialize(auditDetail, new System.Text.Json.JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = _clock.NowLocal,
            StationCode = _userContext.StationCode
        };

        await _auditLogRepository.AddAsync(auditLog, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<decimal?> ResolveRemainingPlannedWeightAsync(CutOrder cutOrder, CancellationToken ct)
    {
        var activeSummary = await _cutOrderRepo.GetActiveExportScaleCutOrdersAsync(
            new ExportScaleCutOrderFilter(cutOrder.ErpCutOrderId, null, null, null, null),
            ct);
        var currentSummary = activeSummary.FirstOrDefault(x => x.CutOrderId == cutOrder.Id);
        return currentSummary != null && currentSummary.RemainingWeight > 0m
            ? currentSummary.RemainingWeight
            : cutOrder.PlannedWeight;
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

    private static void ValidateTransferSourceCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn không thuộc luồng cân xuất khẩu.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh nguồn đã bị hủy hoặc xóa.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến từ cắt lệnh đã chốt.");
        }
    }

    private static void ValidateTransferTargetCutOrder(CutOrder cutOrder)
    {
        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh đích chưa được chuyển sang luồng cân xuất khẩu.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh đích đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ chuyển chuyến sang cắt lệnh xuất hàng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Không thể chuyển chuyến sang cắt lệnh đã chốt.");
        }

        if (cutOrder.CutOrderStatus != CutOrderStatus.IN_SESSION || cutOrder.ProcessingStage != ProcessingStage.WEIGHING)
        {
            throw new InvalidOperationException("Cắt lệnh đích không ở trạng thái cân xuất khẩu.");
        }
    }
}
