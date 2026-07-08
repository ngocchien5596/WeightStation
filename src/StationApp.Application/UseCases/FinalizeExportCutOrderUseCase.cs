using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.Formatting;
using StationApp.Domain.Constants;
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

        if (request.ExportUnweighedWeight < 0m)
        {
            throw new InvalidOperationException("S\u1ed1 l\u01b0\u1ee3ng kh\u00e1c kh\u00f4ng \u0111\u01b0\u1ee3c \u00e2m.");
        }

        var trips = await _cutOrderRepo.GetExportVehicleTripsAsync(cutOrder.Id, ct);
        if (trips.Any(x => x.SessionStatus is WeighingSessionStatus.PENDING_WEIGHT1
                or WeighingSessionStatus.PENDING_WEIGHT2
                or WeighingSessionStatus.ALLOCATION_PENDING))
        {
            throw new InvalidOperationException("Không thể chốt khi còn chuyến xe dở dang.");
        }

        var completedTrips = trips
            .Where(x => x.SessionStatus is WeighingSessionStatus.READY_TO_COMPLETE or WeighingSessionStatus.COMPLETED)
            .ToList();
        var totalWeight = ResolveFinalizedWeighedWeight(cutOrder, completedTrips);
        if (totalWeight <= 0m)
        {
            throw new InvalidOperationException("Chưa có chuyến xe hợp lệ để chốt số lượng.");
        }

        var exportUnweighedWeight = Math.Round(request.ExportUnweighedWeight, 3, MidpointRounding.AwayFromZero);
        var finalizedWeight = totalWeight + exportUnweighedWeight;

        var now = _clock.NowLocal;
        cutOrder.ExportUnweighedWeight = exportUnweighedWeight;
        cutOrder.ExportFinalizedWeight = finalizedWeight;
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

    private static decimal ResolveFinalizedWeighedWeight(CutOrder cutOrder, IEnumerable<ExportVehicleTripListItem> completedTrips)
    {
        if (!ExportPackageTypes.IsBagged(cutOrder.ExportPackageType, cutOrder.BagWeightKg))
        {
            return completedTrips.Sum(x => ExportReturnedBrokenTripHelper.ResolveSignedWeight(x.ActualAllocatedWeight, x.IsReturnedBrokenTrip));
        }

        var bagWeightKg = cutOrder.BagWeightKg.GetValueOrDefault();
        if (bagWeightKg <= 0m)
        {
            throw new InvalidOperationException("Tr\u1ecdng l\u01b0\u1ee3ng bao (kg) ph\u1ea3i l\u1edbn h\u01a1n 0 \u0111\u1ec3 ch\u1ed1t c\u1eaft l\u1ec7nh \u0111\u00f3ng bao.");
        }

        var totalBags = completedTrips.Sum(x => ExportReturnedBrokenTripHelper.ResolveSignedBagCount(
            null,
            x.BagCountDisplay,
            x.ActualAllocatedWeight,
            cutOrder.BagWeightKg,
            x.IsReturnedBrokenTrip));

        return decimal.Round(totalBags * bagWeightKg, 3, MidpointRounding.AwayFromZero);
    }
}
