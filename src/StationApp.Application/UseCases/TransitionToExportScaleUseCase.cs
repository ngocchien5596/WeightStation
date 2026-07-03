using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class TransitionToExportScaleUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public TransitionToExportScaleUseCase(
        ICutOrderRepository cutOrderRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _cutOrderRepo = cutOrderRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(TransitionToExportScaleRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh.");

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh đã bị hủy hoặc xóa.");
        }

        if (cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ cân xuất khẩu cho cắt lệnh xuất hàng.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            throw new InvalidOperationException("Cắt lệnh đã chốt, không thể chuyển sang cân xuất khẩu.");
        }

        if (!cutOrder.IsExportScale
            && (cutOrder.CutOrderStatus != CutOrderStatus.REGISTERED || cutOrder.ProcessingStage != ProcessingStage.IN_YARD))
        {
            throw new InvalidOperationException("Cắt lệnh không còn ở trạng thái xe vào để chuyển sang cân xuất khẩu.");
        }

        if (!cutOrder.IsExportScale && cutOrder.WeighingSessionId.HasValue)
        {
            throw new InvalidOperationException("Cắt lệnh đã thuộc một lượt cân khác.");
        }

        var now = _clock.NowLocal;
        cutOrder.IsExportScale = true;
        cutOrder.CutOrderStatus = CutOrderStatus.IN_SESSION;
        cutOrder.ProcessingStage = ProcessingStage.WEIGHING;
        cutOrder.WeighingSessionId = null;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.ExportStartedAt ??= now;
        cutOrder.ExportStartedBy ??= _userContext.Username;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _cutOrderRepo.UpdateAsync(cutOrder, innerCt),
            ct);
    }
}
