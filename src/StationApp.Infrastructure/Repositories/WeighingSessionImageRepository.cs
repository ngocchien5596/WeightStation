using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
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
        await _db.WeighingSessionImages.AddAsync(image, ct);
    }

    public async Task<IReadOnlyList<WeighingSessionImage>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct)
    {
        return await _db.WeighingSessionImages
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.WeighingSessionId == weighingSessionId)
            .OrderBy(x => x.CapturedAt)
            .ToListAsync(ct);
    }
}
