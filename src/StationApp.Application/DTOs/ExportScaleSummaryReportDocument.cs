namespace StationApp.Application.DTOs;

public sealed record ExportScaleSummaryReportDocument(
    Guid CutOrderId,
    string CutOrderCode,
    string? CustomerName,
    string? ProductName,
    decimal PlannedWeightTon,
    int PlannedBagCount,
    decimal TareWeightKg,
    decimal NetCementWeightKg,
    decimal GrossWeightKg,
    DateTime? TargetDateForShiftReport,
    string PreparedByDisplayName,
    IReadOnlyList<ExportScaleSummaryReportRow> Rows
);
