namespace StationApp.Application.DTOs;

public sealed record InboundSummaryReportFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? CustomerCode
);

public sealed record InboundSummaryReportRow(
    string? CustomerName,
    string? ProductName,
    string VehiclePlate,
    DateTime? Weight1Time,
    DateTime? Weight2Time,
    decimal? Weight1,
    decimal? Weight2,
    decimal NetWeightKg,
    string? Notes,
    string? WeigherName
);

public sealed record InboundSummaryReportDocument(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? ProductDisplayName,
    string? CustomerCode,
    string PreparedByDisplayName,
    IReadOnlyList<InboundSummaryReportRow> Rows,
    decimal TotalNetWeightKg,
    decimal? MonthlyCumulativeNetWeightKg,
    decimal? YearlyCumulativeNetWeightKg
);
