namespace StationApp.Application.Services;

public static class VehicleIdentifierNormalizer
{
    public static string NormalizePlate(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static string? NormalizeOptional(string? value)
    {
        var normalized = NormalizePlate(value);
        return normalized.Length == 0 ? null : normalized;
    }
}
