using System;

namespace StationApp.Domain.Constants;

public static class ExportPackageTypes
{
    public const string Bagged = "BAGGED";
    public const string Bulk = "BULK";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, Bagged, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "DongBao", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Dong bao", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "\u0110\u00f3ng bao", StringComparison.OrdinalIgnoreCase))
        {
            return Bagged;
        }

        if (string.Equals(trimmed, Bulk, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Roi", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "R\u1eddi", StringComparison.OrdinalIgnoreCase))
        {
            return Bulk;
        }

        return null;
    }

    public static string ResolveForExistingData(string? value, decimal? bagWeightKg)
        => Normalize(value) ?? (bagWeightKg.GetValueOrDefault() > 0m ? Bagged : Bulk);

    public static bool IsBagged(string? value, decimal? bagWeightKg)
        => string.Equals(ResolveForExistingData(value, bagWeightKg), Bagged, StringComparison.Ordinal);

    public static string ToDisplayName(string? value, decimal? bagWeightKg)
        => IsBagged(value, bagWeightKg) ? "\u0110\u00f3ng bao" : "R\u1eddi";
}
