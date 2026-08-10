using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.Application.UseCases;

public sealed class BuildExportScaleSummaryReportUseCase
{
    private readonly IExportSummaryReportService _service;
    private readonly ICurrentUserContext _currentUserContext;

    public BuildExportScaleSummaryReportUseCase(
        IExportSummaryReportService service,
        ICurrentUserContext currentUserContext)
    {
        _service = service;
        _currentUserContext = currentUserContext;
    }

    public Task<ExportScaleSummaryReportDocument> ExecuteAsync(
        Guid? cutOrderId,
        DateTime fromTime,
        DateTime toTime,
        DateTime? targetDateForShiftReport,
        CancellationToken ct)
    {
        if (fromTime > toTime)
        {
            throw new InvalidOperationException("Từ giờ không được lớn hơn Đến giờ.");
        }

        var preparedBy = string.IsNullOrWhiteSpace(_currentUserContext.DisplayName)
            ? _currentUserContext.Username
            : _currentUserContext.DisplayName;

        return _service.BuildExportScaleReportAsync(cutOrderId, fromTime, toTime, targetDateForShiftReport, preparedBy, ct);
    }
}

public sealed class ExportExportScaleSummaryReportUseCase
{
    private readonly IExportSummaryReportExporter _exporter;

    public ExportExportScaleSummaryReportUseCase(IExportSummaryReportExporter exporter)
    {
        _exporter = exporter;
    }

    public Task ExecuteAsync(ExportScaleSummaryReportDocument document, string outputPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Đường dẫn xuất báo cáo không hợp lệ.");
        }

        return _exporter.ExportExportScaleAsync(document, outputPath, ct);
    }
}

public sealed class GetExportScaleSummaryReportLookupOptionsUseCase
{
    private readonly IExportSummaryReportService _service;

    public GetExportScaleSummaryReportLookupOptionsUseCase(IExportSummaryReportService service)
    {
        _service = service;
    }

    public Task<IReadOnlyList<ReportLookupOptionDto>> GetCutOrdersAsync(CancellationToken ct)
        => _service.GetExportScaleCutOrderOptionsAsync(ct);
}
