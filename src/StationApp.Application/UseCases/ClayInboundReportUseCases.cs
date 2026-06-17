using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.Application.UseCases;

public sealed class BuildClayInboundReportUseCase
{
    private readonly IClayInboundReportService _service;
    private readonly ICurrentUserContext _currentUserContext;

    public BuildClayInboundReportUseCase(
        IClayInboundReportService service,
        ICurrentUserContext currentUserContext)
    {
        _service = service;
        _currentUserContext = currentUserContext;
    }

    public Task<ClayInboundReportDocument> ExecuteAsync(ClayInboundReportFilter filter, CancellationToken ct)
    {
        if (filter.FromTime > filter.ToTime)
        {
            throw new InvalidOperationException("Từ giờ không được lớn hơn Đến giờ.");
        }

        var preparedBy = string.IsNullOrWhiteSpace(_currentUserContext.DisplayName)
            ? _currentUserContext.Username
            : _currentUserContext.DisplayName;

        return _service.BuildAsync(filter, preparedBy, ct);
    }
}

public sealed class ExportClayInboundReportUseCase
{
    private readonly IClayInboundReportExporter _exporter;

    public ExportClayInboundReportUseCase(IClayInboundReportExporter exporter)
    {
        _exporter = exporter;
    }

    public Task ExecuteAsync(ClayInboundReportDocument document, string outputPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Đường dẫn xuất báo cáo không hợp lệ.");
        }

        return _exporter.ExportAsync(document, outputPath, ct);
    }
}

public sealed class GetClayInboundReportLookupOptionsUseCase
{
    private readonly IClayInboundReportService _service;

    public GetClayInboundReportLookupOptionsUseCase(IClayInboundReportService service)
    {
        _service = service;
    }

    public Task<IReadOnlyList<ReportLookupOptionDto>> GetProductsAsync(CancellationToken ct)
        => _service.GetProductOptionsAsync(ct);

    public Task<IReadOnlyList<ReportLookupOptionDto>> GetCustomersAsync(CancellationToken ct)
        => _service.GetCustomersAsync(ct);
}
