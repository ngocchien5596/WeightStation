using StationApp.Domain.Enums;

namespace StationApp.Domain.Entities;

public class WeighingSessionImage
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public Guid WeighingSessionId { get; set; }
    public CameraCaptureStage CaptureStage { get; set; }
    public string CameraCode { get; set; } = string.Empty;
    public string CameraName { get; set; } = string.Empty;
    public string? RtspUrlSnapshot { get; set; }
    public string ImageFormat { get; set; } = "jpg";
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
    public long FileSizeBytes { get; set; }
    public DateTime CapturedAt { get; set; }
    public string CapturedBy { get; set; } = string.Empty;
    public ImageSyncStatus SyncStatus { get; set; } = ImageSyncStatus.PENDING;
    public DateTime? LastSyncAttemptAt { get; set; }
    public DateTime? LastSyncSuccessAt { get; set; }
    public string? LastSyncError { get; set; }
    public int RetryCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
