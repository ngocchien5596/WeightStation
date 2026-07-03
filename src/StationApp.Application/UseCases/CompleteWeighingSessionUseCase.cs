using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class CompleteWeighingSessionUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly ICutOrderRepository _regRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public CompleteWeighingSessionUseCase(
        IWeighingSessionRepository sessionRepo,
        ICutOrderRepository regRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _regRepo = regRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public static bool CanMoveToOutYard(WeighingSession? session)
    {
        return session != null
            && !session.IsCancelled
            && session.SessionStatus == WeighingSessionStatus.READY_TO_COMPLETE
            && session.Weight1.HasValue
            && session.Weight2.HasValue
            && session.NetWeight.HasValue
            && session.OverweightResolutionStatus is OverweightResolutionStatus.NOT_APPLICABLE
                or OverweightResolutionStatus.SPLIT_CONFIRMED
                or OverweightResolutionStatus.NO_SPLIT_CONFIRMED;
    }

    public async Task ExecuteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (!CanMoveToOutYard(session))
        {
            throw new InvalidOperationException("Lượt xe chưa đủ điều kiện để chuyển ra.");
        }

        var lines = await _sessionRepo.GetLinesBySessionIdAsync(sessionId, ct);
        if (lines.Count == 0
            || lines.Any(x => x.LineStatus != WeighingSessionLineStatus.ALLOCATED)
            || lines.Any(x => !x.ActualAllocatedWeight.HasValue))
        {
            throw new InvalidOperationException("Lượt xe chưa hoàn tất cân hoặc chưa phân bổ xong.");
        }

        var registrations = await _regRepo.GetByWeighingSessionIdAsync(sessionId, ct);
        var now = _clock.NowLocal;

        session.SessionStatus = WeighingSessionStatus.COMPLETED;
        session.UpdatedAt = now;
        session.UpdatedBy = _userContext.Username;

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

            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _sessionRepo.UpdateAsync(session, innerCt);
            foreach (var registration in registrations)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }
        }, ct);
    }
}
