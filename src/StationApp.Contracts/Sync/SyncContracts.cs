namespace StationApp.Contracts.Sync;

public sealed class SyncWeighTicketRequest
{
    public Guid Id { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}

public sealed class SyncWeighTicketResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class SyncStationMasterDataRequest
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public List<SyncStationFeatureFlagItem> FeatureFlags { get; set; } = [];
    public List<SyncStationOperationSettingItem> OperationSettings { get; set; } = [];
}

public sealed class SyncStationFeatureFlagItem
{
    public string FeatureKey { get; set; } = string.Empty;
    public string FeatureValue { get; set; } = string.Empty;
}

public sealed class SyncStationOperationSettingItem
{
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
}

public sealed class SyncWeighingSessionImageRequest
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public Guid WeighingSessionId { get; set; }
    public string CaptureStage { get; set; } = string.Empty;
    public string CameraCode { get; set; } = string.Empty;
    public string CameraName { get; set; } = string.Empty;
    public string? RtspUrlSnapshot { get; set; }
    public string ImageFormat { get; set; } = "jpg";
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
    public long FileSizeBytes { get; set; }
    public DateTime CapturedAt { get; set; }
    public string CapturedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
