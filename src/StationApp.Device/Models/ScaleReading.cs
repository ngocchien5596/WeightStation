using StationApp.Domain.Enums;

namespace StationApp.Device.Models;

public sealed class ScaleReading
{
    public decimal Weight { get; init; }
    public bool IsStable { get; init; }
    public WeightMode Mode { get; init; }
    public DateTime CapturedAt { get; init; }
    public string RawPayload { get; init; } = "";
}
