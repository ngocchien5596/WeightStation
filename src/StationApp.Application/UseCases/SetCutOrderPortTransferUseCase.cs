using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Enums;

namespace StationApp.Application.UseCases;

public sealed class SetCutOrderPortTransferUseCase
{
    private readonly ICutOrderRepository _regRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public SetCutOrderPortTransferUseCase(
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

    public async Task ExecuteAsync(Guid cutOrderId, bool enabled, CancellationToken ct)
    {
        var registration = await _regRepo.GetByIdAsync(cutOrderId, ct)
            ?? throw new InvalidOperationException("Kh\u00f4ng t\u00ecm th\u1ea5y c\u1eaft l\u1ec7nh.");

        if (registration.IsDeleted || registration.IsCancelled)
        {
            throw new InvalidOperationException("C\u1eaft l\u1ec7nh kh\u00f4ng c\u00f2n h\u1ee3p l\u1ec7 \u0111\u1ec3 \u0111\u00e1nh d\u1ea5u chuy\u1ec3n t\u1ea3i.");
        }

        if (registration.IsExportScale || registration.TransactionType != TransactionType.OUTBOUND)
        {
            throw new InvalidOperationException("Ch\u1ec9 h\u1ed7 tr\u1ee3 \u0111\u00e1nh d\u1ea5u chuy\u1ec3n t\u1ea3i cho c\u1eaft l\u1ec7nh xu\u1ea5t n\u1ed9i \u0111\u1ecba.");
        }

        if (registration.IsPortTransfer == enabled)
        {
            return;
        }

        registration.IsPortTransfer = enabled;
        registration.SyncStatus = SyncStatus.SYNC_QUEUED;
        registration.LastSyncAttemptAt = null;
        registration.LastSyncError = null;
        registration.UpdatedAt = _clock.NowLocal;
        registration.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _regRepo.UpdateAsync(registration, innerCt),
            ct);
    }
}

