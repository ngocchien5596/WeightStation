namespace StationApp.Application.DTOs;

public static class ShiftProductOutputReportGroups
{
    public const string Bulk = "Rời";
    public const string Bagged = "Bao";
    public const string Export = "Xuất khẩu";

    public static readonly IReadOnlyList<string> Ordered =
    [
        Bulk,
        Bagged,
        Export
    ];
}

public sealed record ShiftProductOutputReportFilter(
    DateTime ReportDate,
    string ShiftCode,
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode
);

public sealed record ShiftProductOutputReportProductSeed(
    string GroupName,
    string ProductCode,
    string ProductName
);

public sealed record ShiftProductOutputReportSourceRow(
    string GroupName,
    string ProductCode,
    string ProductName,
    Guid SessionId,
    Guid? CutOrderId,
    DateTime ExportedAt,
    decimal SignedWeightKg,
    bool IsReturnedBrokenTrip
);

public sealed record ShiftProductOutputReportRow(
    int Stt,
    string GroupName,
    string ProductCode,
    string ProductName,
    decimal ShiftOutputTon,
    int ReferenceCount
);

public sealed record ShiftProductOutputReportGroup(
    string GroupName,
    IReadOnlyList<ShiftProductOutputReportRow> Rows,
    decimal TotalShiftOutputTon,
    int TotalReferenceCount
);

public sealed record ShiftProductOutputReportDocument(
    DateTime ReportDate,
    string ShiftCode,
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string PreparedByDisplayName,
    IReadOnlyList<ShiftProductOutputReportGroup> Groups,
    decimal GrandTotalShiftOutputTon,
    int GrandTotalReferenceCount
)
{
    public IReadOnlyList<ShiftProductOutputReportRow> Rows => Groups.SelectMany(x => x.Rows).ToList();
}
