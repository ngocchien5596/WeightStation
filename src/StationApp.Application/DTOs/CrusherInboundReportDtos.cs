namespace StationApp.Application.DTOs;

public sealed record CrusherInboundReportFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? VehicleKeyword
);

public sealed record CrusherInboundReportRow(
    int RowNo,
    string SessionNo,
    string InternalVehicleNo,
    string? CustomerName,
    string? ProductName,
    DateTime? Weight2Time,
    decimal GrossWeightTon,
    decimal TareWeightTon,
    decimal NetWeightTon,
    decimal ReturnedBrokenWeightTon,
    decimal ActualInboundWeightTon,
    bool IsReturnedBrokenTrip = false
);

public sealed record CrusherInboundReportDocument(
    DateTime FromTime,
    DateTime ToTime,
    string? VehicleKeyword,
    string StationName,
    string PreparedByDisplayName,
    byte[]? LogoBytes,
    IReadOnlyList<CrusherInboundReportRow> Rows,
    decimal TotalNetWeightTon,
    decimal ReturnedBrokenWeightTon = 0m,
    decimal ActualInboundWeightTon = 0m
);
