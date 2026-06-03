using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public sealed class WeighingSessionImageRepository : IWeighingSessionImageRepository
{
    private readonly StationDbContext _db;

    public WeighingSessionImageRepository(StationDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(WeighingSessionImage image, CancellationToken ct)
    {
        image.SyncStatus = ImageSyncStatus.PENDING;
        image.LastSyncError = null;
        await _db.WeighingSessionImages.AddAsync(image, ct);
    }

    public Task UpdateAsync(WeighingSessionImage image, CancellationToken ct)
    {
        _db.WeighingSessionImages.Update(image);
        return Task.CompletedTask;
    }

    public async Task<WeighingSessionImage?> GetByIdAsync(Guid imageId, CancellationToken ct)
    {
        return await _db.WeighingSessionImages
            .FirstOrDefaultAsync(x => x.Id == imageId && !x.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<WeighingSessionImage>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct)
    {
        return await _db.WeighingSessionImages
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.WeighingSessionId == weighingSessionId)
            .OrderBy(x => x.CapturedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WeighingSessionImage>> GetPendingSyncAsync(int batchSize, CancellationToken ct)
    {
        return await _db.WeighingSessionImages
            .Where(x => !x.IsDeleted && (x.SyncStatus == ImageSyncStatus.PENDING || x.SyncStatus == ImageSyncStatus.FAILED))
            .OrderBy(x => x.LastSyncAttemptAt ?? x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }
}
