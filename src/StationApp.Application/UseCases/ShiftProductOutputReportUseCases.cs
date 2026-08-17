using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.Application.UseCases;

public sealed class BuildShiftProductOutputReportUseCase
{
    private readonly IShiftProductOutputReportService _service;
    private readonly ICurrentUserContext _currentUserContext;

    public BuildShiftProductOutputReportUseCase(
        IShiftProductOutputReportService service,
        ICurrentUserContext currentUserContext)
    {
        _service = service;
        _currentUserContext = currentUserContext;
    }

    public Task<ShiftProductOutputReportDocument> ExecuteAsync(
        ShiftProductOutputReportFilter filter,
        CancellationToken ct)
    {
        if (filter.FromTime > filter.ToTime)
        {
            throw new InvalidOperationException("Từ giờ không được lớn hơn Đến giờ.");
        }

        if (filter.ReportDate == default)
        {
            throw new InvalidOperationException("Vui lòng chọn ngày báo cáo.");
        }

        var preparedBy = string.IsNullOrWhiteSpace(_currentUserContext.DisplayName)
            ? _currentUserContext.Username
            : _currentUserContext.DisplayName;

        return _service.BuildAsync(filter, preparedBy, ct);
    }
}

public sealed class ExportShiftProductOutputReportUseCase
{
    private readonly IShiftProductOutputReportExporter _exporter;

    public ExportShiftProductOutputReportUseCase(IShiftProductOutputReportExporter exporter)
    {
        _exporter = exporter;
    }

    public Task ExecuteAsync(ShiftProductOutputReportDocument document, string outputPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Đường dẫn xuất báo cáo không hợp lệ.");
        }

        return _exporter.ExportAsync(document, outputPath, ct);
    }
}

public sealed class GetShiftProductOutputReportLookupOptionsUseCase
{
    private readonly IShiftProductOutputReportService _service;

    public GetShiftProductOutputReportLookupOptionsUseCase(IShiftProductOutputReportService service)
    {
        _service = service;
    }

    public Task<IReadOnlyList<ReportLookupOptionDto>> GetProductsAsync(CancellationToken ct)
        => _service.GetProductOptionsAsync(ct);
}
