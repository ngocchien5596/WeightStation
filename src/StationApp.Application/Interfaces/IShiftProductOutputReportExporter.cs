using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IShiftProductOutputReportExporter
{
    Task ExportAsync(ShiftProductOutputReportDocument document, string outputPath, CancellationToken ct);
}
