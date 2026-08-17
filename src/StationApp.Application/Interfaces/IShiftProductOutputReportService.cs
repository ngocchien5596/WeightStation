using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IShiftProductOutputReportService
{
    Task<ShiftProductOutputReportDocument> BuildAsync(
        ShiftProductOutputReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct);
}
