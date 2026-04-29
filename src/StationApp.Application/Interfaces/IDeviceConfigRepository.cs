using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IDeviceConfigRepository
{
    Task<DeviceConfig?> GetActiveAsync(CancellationToken ct);
    Task SaveAsync(DeviceConfig config, CancellationToken ct);
}
