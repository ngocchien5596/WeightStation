using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public class SyncOutboxRepository : ISyncOutboxRepository
{
    private readonly StationDbContext _db;
    private readonly IClock _clock;

    public SyncOutboxRepository(StationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task EnqueueAsync(SyncOutbox message, CancellationToken ct)
        => await _db.SyncOutbox.AddAsync(message, ct);

    public async Task<IReadOnlyList<SyncOutbox>> GetPendingAsync(DateTime now, int batchSize, CancellationToken ct)
        => await _db.SyncOutbox
            .Where(m => m.Status == OutboxStatus.PENDING ||
                        (m.Status == OutboxStatus.FAILED_RETRYABLE && m.NextRetryAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkProcessingAsync(Guid id, CancellationToken ct)
    {
        var msg = await _db.SyncOutbox.FindAsync(new object[] { id }, ct);
        if (msg != null) { msg.Status = OutboxStatus.PROCESSING; msg.UpdatedAt = _clock.NowLocal; }
    }

    public async Task MarkSuccessAsync(Guid id, CancellationToken ct)
    {
        var msg = await _db.SyncOutbox.FindAsync(new object[] { id }, ct);
        if (msg != null) { msg.Status = OutboxStatus.SUCCESS; msg.UpdatedAt = _clock.NowLocal; }
    }

    public async Task MarkFailedRetryableAsync(Guid id, string error, DateTime nextRetryAt, CancellationToken ct)
    {
        var msg = await _db.SyncOutbox.FindAsync(new object[] { id }, ct);
        if (msg != null)
        {
            msg.Status = OutboxStatus.FAILED_RETRYABLE;
            msg.LastError = error;
            msg.RetryCount++;
            msg.NextRetryAt = nextRetryAt;
            msg.UpdatedAt = _clock.NowLocal;
        }
    }

    public async Task MarkFailedFinalAsync(Guid id, string error, CancellationToken ct)
    {
        var msg = await _db.SyncOutbox.FindAsync(new object[] { id }, ct);
        if (msg != null)
        {
            msg.Status = OutboxStatus.FAILED_FINAL;
            msg.LastError = error;
            msg.UpdatedAt = _clock.NowLocal;
        }
    }
}
