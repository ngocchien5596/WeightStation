using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IExportSummaryReportExporter
{
    Task ExportAsync(ExportSummaryReportDocument document, string outputPath, CancellationToken ct);

    Task ExportExportScaleAsync(ExportScaleSummaryReportDocument document, string outputPath, CancellationToken ct);
}
