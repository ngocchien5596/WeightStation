using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IInboundSummaryReportExporter
{
    Task ExportAsync(InboundSummaryReportDocument document, string outputPath, CancellationToken ct);
}
