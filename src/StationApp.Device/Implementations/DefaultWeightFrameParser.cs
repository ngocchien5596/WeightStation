using StationApp.Device.Abstractions;

namespace StationApp.Device.Implementations;

public sealed class DefaultWeightFrameParser : IWeightFrameParser
{
    public decimal? TryParse(string rawFrame, out bool isStable)
    {
        isStable = false;
        if (string.IsNullOrWhiteSpace(rawFrame) || rawFrame.Length < 7)
            return null;

        try
        {
            isStable = rawFrame.Contains("ST", StringComparison.OrdinalIgnoreCase);
            var numericPart = new string(rawFrame.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (decimal.TryParse(numericPart, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var weight))
                return weight;
        }
        catch { }
        return null;
    }
}
