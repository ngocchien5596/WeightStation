using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class ToggleExportReturnedBrokenTripUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _cutOrderRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public ToggleExportReturnedBrokenTripUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository cutOrderRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _cutOrderRepo = cutOrderRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionLineId, bool isReturnedBrokenTrip, CancellationToken ct)
    {
        var line = await _sessionRepo.GetLineByIdAsync(sessionLineId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy dòng chuyến xe cần cập nhật.");
        var cutOrder = await _cutOrderRepo.GetByIdAsync(line.CutOrderId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy cắt lệnh của chuyến xe.");
        var session = await _sessionRepo.GetByIdAsync(line.WeighingSessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân của chuyến xe.");

        if (line.IsDeleted || cutOrder.IsDeleted || cutOrder.IsCancelled || session.IsDeleted || session.IsCancelled)
        {
            throw new InvalidOperationException("Chuyến xe không còn hợp lệ để cập nhật trạng thái hàng hoàn.");
        }

        if (!cutOrder.IsExportScale || cutOrder.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Chỉ hỗ trợ đánh dấu hàng hoàn cho luồng cân xuất khẩu.");
        }

        if ((line.ActualAllocatedWeight ?? 0m) <= 0m)
        {
            throw new InvalidOperationException("Chỉ được đánh dấu hàng hoàn khi chuyến đã có số lượng thực xuất.");
        }

        if (line.IsReturnedBrokenTrip == isReturnedBrokenTrip)
        {
            return;
        }

        var now = _clock.NowLocal;
        line.IsReturnedBrokenTrip = isReturnedBrokenTrip;
        line.SyncStatus = SyncStatus.SYNC_QUEUED;
        line.LastSyncAttemptAt = null;
        line.LastSyncError = null;
        line.UpdatedAt = now;
        line.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _sessionRepo.UpdateLineAsync(line, innerCt),
            ct);
    }
}
