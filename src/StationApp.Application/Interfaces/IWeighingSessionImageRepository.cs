using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IWeighingSessionImageRepository
{
    Task AddAsync(WeighingSessionImage image, CancellationToken ct);
    Task UpdateAsync(WeighingSessionImage image, CancellationToken ct);
    Task<WeighingSessionImage?> GetByIdAsync(Guid imageId, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionImage>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionImage>> GetPendingSyncAsync(int batchSize, CancellationToken ct);
}
