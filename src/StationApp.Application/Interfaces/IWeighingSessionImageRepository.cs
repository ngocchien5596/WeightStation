using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IWeighingSessionImageRepository
{
    Task AddAsync(WeighingSessionImage image, CancellationToken ct);
    Task<IReadOnlyList<WeighingSessionImage>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct);
}
