using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IClayInboundReportService
{
    Task<ClayInboundReportDocument> BuildAsync(
        ClayInboundReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetCarrierOptionsAsync(CancellationToken ct);

    Task<IReadOnlyList<ReportLookupOptionDto>> GetVesselOptionsAsync(
        ClayInboundVesselLookupFilter filter,
        CancellationToken ct);
}
