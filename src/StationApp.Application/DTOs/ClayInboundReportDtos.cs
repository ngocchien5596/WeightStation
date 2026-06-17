namespace StationApp.Application.DTOs;

public sealed record ClayInboundReportFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? CustomerCode
);

public sealed record ClayInboundReportRow(
    string SessionNo,
    string InternalVehicleNo,
    string? DriverName,
    string? CustomerName,
    string? ProductName,
    string WeighingModeDisplay,
    DateTime? Weight1Time,
    DateTime? Weight2Time,
    decimal? StandardTareWeightKg,
    decimal? Weight1,
    decimal? Weight2,
    decimal NetWeightKg,
    string? Notes,
    string? WeigherName
);

public sealed record ClayInboundReportDocument(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? ProductDisplayName,
    string? CustomerCode,
    string PreparedByDisplayName,
    IReadOnlyList<ClayInboundReportRow> Rows,
    decimal TotalNetWeightKg
);
