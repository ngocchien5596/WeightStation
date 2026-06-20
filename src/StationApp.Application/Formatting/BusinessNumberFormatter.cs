namespace StationApp.Application.Formatting;

public static class BusinessNumberFormatter
{
    public static string NormalizeStationCode(string stationCode)
    {
        if (string.IsNullOrWhiteSpace(stationCode))
        {
            throw new ArgumentException("Station code cannot be null or empty.", nameof(stationCode));
        }

        return stationCode.Trim().ToUpperInvariant();
    }

    public static string PrefixWithStation(string stationCode, string businessNumber)
    {
        if (string.IsNullOrWhiteSpace(businessNumber))
        {
            throw new ArgumentException("Business number cannot be null or empty.", nameof(businessNumber));
        }

        var trimmed = businessNumber.Trim();
        return trimmed.Contains('-', StringComparison.Ordinal)
            ? trimmed
            : $"{NormalizeStationCode(stationCode)}-{trimmed}";
    }

    public static string BuildCounterKey(string counterType, string stationCode, string businessPrefix)
    {
        if (string.IsNullOrWhiteSpace(counterType))
        {
            throw new ArgumentException("Counter type cannot be null or empty.", nameof(counterType));
        }

        if (string.IsNullOrWhiteSpace(businessPrefix))
        {
            throw new ArgumentException("Business prefix cannot be null or empty.", nameof(businessPrefix));
        }

        return $"{counterType.Trim()}_{NormalizeStationCode(stationCode)}_{businessPrefix.Trim()}";
    }

    public static string ToDisplay(string? businessNumber)
    {
        if (string.IsNullOrWhiteSpace(businessNumber))
        {
            return businessNumber ?? string.Empty;
        }

        var trimmed = businessNumber.Trim();
        var separatorIndex = trimmed.IndexOf('-');
        return separatorIndex > 0 && separatorIndex < trimmed.Length - 1
            ? trimmed[(separatorIndex + 1)..]
            : trimmed;
    }
}
