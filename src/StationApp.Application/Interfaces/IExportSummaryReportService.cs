using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IExportSummaryReportService
{
    Task<ExportSummaryReportDocument> BuildAsync(
        ExportSummaryReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetCustomerOptionsAsync(CancellationToken ct);
}
