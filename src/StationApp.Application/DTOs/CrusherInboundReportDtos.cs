namespace StationApp.Application.DTOs;

public sealed record CrusherInboundReportFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? CustomerCode
);

public sealed record CrusherInboundReportRow(
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

public sealed record CrusherInboundReportDocument(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? ProductDisplayName,
    string? CustomerCode,
    string PreparedByDisplayName,
    IReadOnlyList<CrusherInboundReportRow> Rows,
    decimal TotalNetWeightKg
);
