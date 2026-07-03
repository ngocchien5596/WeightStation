using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class SetWeighingSessionPortTransferUseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public SetWeighingSessionPortTransferUseCase(
        ICutOrderRepository regRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _regRepo = regRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionId, bool enabled, CancellationToken ct)
    {
        var registrations = (await _regRepo.GetByWeighingSessionIdAsync(sessionId, ct))
            .Where(x => !x.IsDeleted
                && !x.IsCancelled
                && !x.IsExportScale
                && x.TransactionType == TransactionType.OUTBOUND)
            .ToList();

        if (registrations.Count == 0)
        {
            throw new InvalidOperationException("Kh\u00f4ng c\u00f3 c\u1eaft l\u1ec7nh xu\u1ea5t n\u1ed9i \u0111\u1ecba h\u1ee3p l\u1ec7 \u0111\u1ec3 \u0111\u00e1nh d\u1ea5u chuy\u1ec3n t\u1ea3i.");
        }

        var changed = registrations
            .Where(x => x.IsPortTransfer != enabled)
            .ToList();
        if (changed.Count == 0)
        {
            return;
        }

        var now = _clock.NowLocal;
        foreach (var registration in changed)
        {
            registration.IsPortTransfer = enabled;
            registration.SyncStatus = SyncStatus.SYNC_QUEUED;
            registration.LastSyncAttemptAt = null;
            registration.LastSyncError = null;
            registration.UpdatedAt = now;
            registration.UpdatedBy = _userContext.Username;
        }

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var registration in changed)
            {
                await _regRepo.UpdateAsync(registration, innerCt);
            }
        }, ct);
    }
}

