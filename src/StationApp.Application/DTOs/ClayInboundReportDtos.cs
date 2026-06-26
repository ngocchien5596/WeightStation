namespace StationApp.Application.DTOs;

public sealed record ClayInboundReportFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? VehicleKeyword
);

public sealed record ClayInboundReportRow(
    int RowNo,
    string SessionNo,
    string InternalVehicleNo,
    string? CustomerName,
    string? ProductName,
    DateTime? Weight2Time,
    decimal GrossWeightTon,
    decimal TareWeightTon,
    decimal NetWeightTon
);

public sealed record ClayInboundReportDocument(
    DateTime FromTime,
    DateTime ToTime,
    string? VehicleKeyword,
    string StationName,
    string PreparedByDisplayName,
    byte[]? LogoBytes,
    IReadOnlyList<ClayInboundReportRow> Rows,
    decimal TotalNetWeightTon
);
