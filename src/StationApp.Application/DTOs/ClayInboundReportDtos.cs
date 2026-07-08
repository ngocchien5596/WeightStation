namespace StationApp.Application.DTOs;

public sealed record ClayInboundReportFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? CarrierCode,
    Guid? VesselCutOrderId = null
);

public sealed record ClayInboundVesselLookupFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? CarrierCode
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
    decimal NetWeightTon,
    decimal ReturnedBrokenWeightTon,
    decimal ActualInboundWeightTon,
    bool IsReturnedBrokenTrip = false
);

public sealed record ClayInboundReportDocument(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? CarrierCode,
    Guid? VesselCutOrderId,
    string? VesselDisplayName,
    string StationName,
    string PreparedByDisplayName,
    byte[]? LogoBytes,
    IReadOnlyList<ClayInboundReportRow> Rows,
    decimal TotalNetWeightTon,
    decimal ReturnedBrokenWeightTon = 0m,
    decimal ActualInboundWeightTon = 0m
);
