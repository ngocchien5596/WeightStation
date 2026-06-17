using StationApp.Application.DTOs;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Application.Interfaces;

public interface IWeighingSessionRepository
{
    Task AddAsync(WeighingSession session, CancellationToken ct);
    Task UpdateAsync(WeighingSession session, CancellationToken ct);
    Task<WeighingSession?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<WeighingSession?> GetBySessionNoAsync(string sessionNo, CancellationToken ct);
    Task<IReadOnlyList<WeighingSession>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<WeighingSession>> GetBySyncStatusAsync(StationApp.Domain.Enums.SyncStatus syncStatus, int batchSize, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionListItem>> SearchActiveSessionsAsync(string? keyword, TransactionType? transactionType, CancellationToken ct);
    Task<IReadOnlyList<CrusherWeighingSessionListItem>> SearchCrusherSessionsAsync(string? keyword, DateTime? selectedDate, CancellationToken ct);
    Task<IReadOnlyList<CrusherWeighingSessionListItem>> SearchClaySessionsAsync(string? keyword, DateTime? selectedDate, CancellationToken ct);
    Task<IReadOnlyList<OutgoingSessionListItem>> SearchCompletedSessionsAsync(string? keyword, DateTime? completedDate, CancellationToken ct);
    Task ApplySyncResultAsync(Guid sessionId, StationApp.Domain.Enums.SyncStatus syncStatus, DateTime attemptedAt, string? error, CancellationToken ct);

    Task AddLineAsync(WeighingSessionLine line, CancellationToken ct);
    Task UpdateLineAsync(WeighingSessionLine line, CancellationToken ct);
    Task<WeighingSessionLine?> GetLineByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionLine>> GetLinesBySyncStatusAsync(StationApp.Domain.Enums.SyncStatus syncStatus, int batchSize, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionLine>> GetLinesBySessionIdAsync(Guid sessionId, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionLineItem>> GetLineItemsBySessionIdAsync(Guid sessionId, CancellationToken ct);
    Task ApplyLineSyncResultAsync(Guid lineId, StationApp.Domain.Enums.SyncStatus syncStatus, DateTime attemptedAt, string? error, CancellationToken ct);
    Task<WeighingSession?> GetReusablePendingWeight2SessionAsync(string vehiclePlate, string? moocNumber, StationApp.Domain.Enums.TransactionType transactionType, CancellationToken ct);
}
