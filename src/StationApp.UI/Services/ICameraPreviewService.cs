using StationApp.Application.DTOs;
using System.Windows.Media.Imaging;

namespace StationApp.UI.Services;

public interface ICameraPreviewService : IDisposable
{
    event EventHandler<CameraPreviewStatusChangedEventArgs>? StatusChanged;
    event EventHandler<CameraPreviewFrameReceivedEventArgs>? FrameReceived;

    string? ActiveCameraCode { get; }
    Guid? ActivePreviewSessionId { get; }
    bool IsPreviewRunning { get; }

    Task StartPreviewAsync(CameraEndpointSettings camera, CancellationToken ct);
    Task StopPreviewAsync();
    void AttachHostWindow(IntPtr hostHandle, int width, int height);
    void ResizeHostWindow(int width, int height);
    void DetachHostWindow();
}

public sealed class CameraPreviewStatusChangedEventArgs : EventArgs
{
    public required string? CameraCode { get; init; }
    public required string StatusText { get; init; }
}

public sealed class CameraPreviewFrameReceivedEventArgs : EventArgs
{
    public required string CameraCode { get; init; }
    public required Guid PreviewSessionId { get; init; }
    public required long Sequence { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required int FrameWidth { get; init; }
    public required int FrameHeight { get; init; }
    public required BitmapSource Frame { get; init; }
}
