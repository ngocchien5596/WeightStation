using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IInboundSummaryReportService
{
    Task<InboundSummaryReportDocument> BuildAsync(
        InboundSummaryReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetCustomersAsync(CancellationToken ct);
}
