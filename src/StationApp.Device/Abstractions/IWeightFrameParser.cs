namespace StationApp.Device.Abstractions;

public interface IWeightFrameParser
{
    decimal? TryParse(string rawFrame, out bool isStable);
}
