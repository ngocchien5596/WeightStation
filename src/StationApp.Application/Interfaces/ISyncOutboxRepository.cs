using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface ISyncOutboxRepository
{
    Task EnqueueAsync(SyncOutbox message, CancellationToken ct);
    Task<IReadOnlyList<SyncOutbox>> GetPendingAsync(DateTime now, int batchSize, CancellationToken ct);
    Task MarkProcessingAsync(Guid id, CancellationToken ct);
    Task MarkSuccessAsync(Guid id, CancellationToken ct);
    Task MarkFailedRetryableAsync(Guid id, string error, DateTime nextRetryAt, CancellationToken ct);
    Task MarkFailedFinalAsync(Guid id, string error, CancellationToken ct);
}
