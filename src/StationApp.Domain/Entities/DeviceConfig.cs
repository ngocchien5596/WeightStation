namespace StationApp.Domain.Entities;

public class DeviceConfig
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string ComPort { get; set; } = string.Empty;
    public int Baudrate { get; set; }
    public string? Parity { get; set; }
    public int? DataBits { get; set; }
    public int? StopBits { get; set; }
    public string? FrameEndChar { get; set; }
    public string ParserType { get; set; } = string.Empty;
    public decimal? StabilityThreshold { get; set; }
    public int? StableCycles { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
