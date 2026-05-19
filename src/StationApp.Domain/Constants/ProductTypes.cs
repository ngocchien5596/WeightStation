using System.Collections.Generic;
using System.Linq;
using StationApp.Domain.Enums;

namespace StationApp.Domain.Constants;

public static class ProductTypes
{
    public const string Bagged = "Bao";
    public const string Bulk = "R\u1eddi/X\u00e1";
    public const string Inbound = "H\u00e0ng nh\u1eadp";

    public static readonly IReadOnlyList<string> All =
    [
        Bagged,
        Bulk,
        Inbound
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim(), StringComparer.Ordinal);

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return All.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static string? InferForTransaction(TransactionType transactionType)
        => transactionType == TransactionType.INBOUND ? Inbound : null;
}
