using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface ICrusherInboundReportService
{
    Task<CrusherInboundReportDocument> BuildAsync(
        CrusherInboundReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetCustomersAsync(CancellationToken ct);
}
