using System;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;

namespace StationApp.Application.UseCases;

public sealed class SetWeighingSessionBaggedActualWeightOverrideUseCase
{
    private readonly IWeighingSessionRepository _sessionRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public SetWeighingSessionBaggedActualWeightOverrideUseCase(
        IWeighingSessionRepository sessionRepo,
        IUnitOfWork uow,
        ICurrentUserContext userContext,
        IClock clock)
    {
        _sessionRepo = sessionRepo;
        _uow = uow;
        _userContext = userContext;
        _clock = clock;
    }

    public async Task ExecuteAsync(Guid sessionId, bool enabled, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt cân.");

        if (session.UseActualWeightForBaggedCutOrders == enabled)
        {
            return;
        }

        session.UseActualWeightForBaggedCutOrders = enabled;
        session.UpdatedAt = _clock.NowLocal;
        session.UpdatedBy = _userContext.Username;

        await _uow.ExecuteInTransactionAsync(
            innerCt => _sessionRepo.UpdateAsync(session, innerCt),
            ct);
    }
}
