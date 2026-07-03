using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Formatting;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class FinalizeExportCutOrderUseCase
{
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public FinalizeExportCutOrderUseCase(
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

    public async Task ExecuteAsync(FinalizeExportCutOrderRequest request, CancellationToken ct)
    {
        var cutOrder = await _cutOrderRepo.GetByIdAsync(request.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh.");

        if (!cutOrder.IsExportScale)
        {
            throw new InvalidOperationException("Cắt lệnh không thuộc luồng cân xuất khẩu.");
        }

        if (cutOrder.IsDeleted || cutOrder.IsCancelled)
        {
            throw new InvalidOperationException("Cắt lệnh đã bị hủy hoặc xóa.");
        }

        if (cutOrder.ExportFinalizedAt.HasValue || cutOrder.CutOrderStatus == CutOrderStatus.COMPLETED)
        {
            return;
        }

        var trips = await _cutOrderRepo.GetExportVehicleTripsAsync(cutOrder.Id, ct);
        if (trips.Any(x => x.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT1
                or WeighingSessionStatus.PENDING_WEIGHT2
                or WeighingSessionStatus.ALLOCATION_PENDING))
        {
            throw new InvalidOperationException("Không thể chốt khi còn chuyến xe dở dang.");
        }

        var totalWeight = trips
            .Where(x => x.SessionStatus is WeighingSessionStatus.READY_TO_COMPLETE or WeighingSessionStatus.COMPLETED)
            .Sum(x => ExportReturnedBrokenTripHelper.ResolveSignedWeight(x.ActualAllocatedWeight, x.IsReturnedBrokenTrip));
        if (totalWeight <= 0m)
        {
            throw new InvalidOperationException("Chưa có chuyến xe hợp lệ để chốt số lượng.");
        }

        var now = _clock.NowLocal;
        cutOrder.ExportFinalizedWeight = totalWeight;
        cutOrder.ExportFinalizedAt = now;
        cutOrder.ExportFinalizedBy = _userContext.Username;
        cutOrder.CutOrderStatus = CutOrderStatus.COMPLETED;
        cutOrder.ProcessingStage = ProcessingStage.OUT_YARD;
        cutOrder.WeighingSessionId = null;
        cutOrder.SyncStatus = SyncStatus.SYNC_QUEUED;
        cutOrder.UpdatedAt = now;
        cutOrder.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _cutOrderRepo.UpdateAsync(cutOrder, innerCt),
            ct);
    }
}
