using StationApp.Application.DTOs;
using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IWeighingSessionRepository
{
    Task AddAsync(WeighingSession session, CancellationToken ct);
    Task UpdateAsync(WeighingSession session, CancellationToken ct);
    Task<WeighingSession?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<WeighingSession?> GetBySessionNoAsync(string sessionNo, CancellationToken ct);
    Task<IReadOnlyList<WeighingSession>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionListItem>> SearchActiveSessionsAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<OutgoingSessionListItem>> SearchCompletedSessionsAsync(string? keyword, DateTime? completedDate, CancellationToken ct);

    Task AddLineAsync(WeighingSessionLine line, CancellationToken ct);
    Task UpdateLineAsync(WeighingSessionLine line, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionLine>> GetLinesBySessionIdAsync(Guid sessionId, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionLineItem>> GetLineItemsBySessionIdAsync(Guid sessionId, CancellationToken ct);
    Task<WeighingSession?> GetReusablePendingWeight2SessionAsync(string vehiclePlate, string? moocNumber, StationApp.Domain.Enums.TransactionType transactionType, CancellationToken ct);
}
