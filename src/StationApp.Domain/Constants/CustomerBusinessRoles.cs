using StationApp.Domain.Enums;

namespace StationApp.Domain.Constants;

public static class CustomerBusinessRoles
{
    public const string Supplier = "SUPPLIER";
    public const string Distributor = "DISTRIBUTOR";
    public const string Both = "BOTH";

    public static readonly IReadOnlyList<string> All =
    [
        Supplier,
        Distributor,
        Both
    ];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Both;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, Supplier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "NCC", StringComparison.OrdinalIgnoreCase))
        {
            return Supplier;
        }

        if (string.Equals(trimmed, Distributor, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "NPP", StringComparison.OrdinalIgnoreCase))
        {
            return Distributor;
        }

        if (string.Equals(trimmed, Both, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "CA_HAI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Cả hai", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Ca hai", StringComparison.OrdinalIgnoreCase))
        {
            return Both;
        }

        return Both;
    }

    public static string ToDisplay(string? value)
        => Normalize(value) switch
        {
            Supplier => "NCC",
            Distributor => "NPP",
            _ => "Cả hai"
        };

    public static bool AllowsTransaction(string? value, TransactionType transactionType)
        => Normalize(value) switch
        {
            Both => true,
            Supplier => transactionType == TransactionType.INBOUND,
            Distributor => transactionType == TransactionType.OUTBOUND,
            _ => true
        };

    public static string ForTransaction(TransactionType transactionType)
        => transactionType == TransactionType.INBOUND ? Supplier : Distributor;

    public static string MergeForTransaction(string? currentValue, TransactionType transactionType)
    {
        var current = Normalize(currentValue);
        var next = ForTransaction(transactionType);
        return current == next || current == Both ? current : Both;
    }
}
