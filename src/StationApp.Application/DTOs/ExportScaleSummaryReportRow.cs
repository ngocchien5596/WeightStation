namespace StationApp.Application.DTOs;

public sealed record ExportScaleSummaryReportRow(
    int Stt,
    string SessionNo,
    string Shift,
    DateTime ExportDate,
    string VehiclePlate,
    decimal NetWeightTon,
    int BagCount,
    decimal BagShellWeightTon,
    decimal ReturnedBrokenWeightTon,
    int ReturnedBrokenBagCount,
    decimal ActualExportTon,
    int ActualExportBagCount,
    decimal DifferenceKgPerTrip,
    decimal? DifferenceKgPerBag,
    string? Notes,
    bool IsReturnedBrokenTrip
);
