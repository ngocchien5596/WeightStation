using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface ICrusherInboundReportExporter
{
    Task ExportAsync(CrusherInboundReportDocument document, string outputPath, CancellationToken ct);
}
