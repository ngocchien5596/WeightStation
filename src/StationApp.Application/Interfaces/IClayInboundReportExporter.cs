using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IClayInboundReportExporter
{
    Task ExportAsync(ClayInboundReportDocument document, string outputPath, CancellationToken ct);
}
