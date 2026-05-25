using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface ICameraCaptureService
{
    Task<IReadOnlyList<CameraCaptureImageResult>> CaptureAsync(
        IReadOnlyList<CameraEndpointSettings> cameras,
        int timeoutMs,
        int jpegQuality,
        int warmupFrames,
        CancellationToken ct);
}
