using StationApp.Contracts.Sync;
using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IWeighingSessionImageSyncClient
{
    Task<SyncWeighTicketResponse> PushImageAsync(WeighingSessionImage image, CancellationToken ct);
}
