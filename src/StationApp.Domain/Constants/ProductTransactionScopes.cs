using StationApp.Domain.Enums;

namespace StationApp.Domain.Constants;

public static class ProductTransactionScopes
{
    public const string Inbound = "INBOUND";
    public const string Outbound = "OUTBOUND";
    public const string Both = "BOTH";

    public static readonly IReadOnlyList<string> All =
    [
        Inbound,
        Outbound,
        Both
    ];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Both;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, Inbound, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Nhập hàng", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Nhap hang", StringComparison.OrdinalIgnoreCase))
        {
            return Inbound;
        }

        if (string.Equals(trimmed, Outbound, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Xuất hàng", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Xuat hang", StringComparison.OrdinalIgnoreCase))
        {
            return Outbound;
        }

        if (string.Equals(trimmed, Both, StringComparison.OrdinalIgnoreCase)
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
            Inbound => "Nhập hàng",
            Outbound => "Xuất hàng",
            _ => "Cả hai"
        };

    public static bool AllowsTransaction(string? value, TransactionType transactionType)
        => Normalize(value) switch
        {
            Both => true,
            Inbound => transactionType == TransactionType.INBOUND,
            Outbound => transactionType == TransactionType.OUTBOUND,
            _ => true
        };

    public static string ForTransaction(TransactionType transactionType)
        => transactionType == TransactionType.INBOUND ? Inbound : Outbound;

    public static string MergeForTransaction(string? currentValue, TransactionType transactionType)
    {
        var current = Normalize(currentValue);
        var next = ForTransaction(transactionType);
        return current == next || current == Both ? current : Both;
    }
}
