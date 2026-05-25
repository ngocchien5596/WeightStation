using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface ICameraSettingsProvider
{
    Task<CameraSystemSettings> GetAsync(CancellationToken ct);
}
