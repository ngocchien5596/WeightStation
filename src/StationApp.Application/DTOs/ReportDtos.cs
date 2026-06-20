using StationApp.Domain.Enums;

namespace StationApp.Application.DTOs;

public sealed record ExportSummaryReportFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? CustomerCode,
    OutgoingFlowType FlowType = OutgoingFlowType.All
);

public sealed record ExportSummaryReportRow(
    DateTime ExportedAt,
    string? WeighTicketNo,
    string? CustomerCode,
    string? CustomerName,
    string? CutOrderCode,
    string? DeliveryNo,
    string VehiclePlate,
    string? DriverName,
    int PlannedBagCount,
    decimal PlannedWeightKg,
    decimal PlannedTon,
    int ActualBagCount,
    decimal ActualWeightKg,
    decimal ActualTon,
    string ProductDisplayName,
    string? Notes,
    decimal DifferenceTon,
    decimal? StandardKgPerBag,
    decimal? ActualKgPerBag,
    string Status
);

public sealed record ExportSummaryReportDocument(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    string? CustomerCode,
    decimal ToleranceKgPerBag,
    string PreparedByDisplayName,
    IReadOnlyList<ExportSummaryReportRow> Rows
);

public sealed record ReportLookupOptionDto(
    string Code,
    string DisplayName
);
