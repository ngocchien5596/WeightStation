using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Services;

public sealed class InboundSummaryReportService : IInboundSummaryReportService
{
    private readonly StationDbContext _dbContext;
    private readonly IAppConfigRepository _appConfigRepository;

    public InboundSummaryReportService(StationDbContext dbContext, IAppConfigRepository appConfigRepository)
    {
        _dbContext = dbContext;
        _appConfigRepository = appConfigRepository;
    }

    public async Task<InboundSummaryReportDocument> BuildAsync(
        InboundSummaryReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct)
    {
        var products = await _dbContext.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(ct);

        var productDisplayName = ResolveProductDisplayName(filter.ProductCode, [], products);
        var stationCode = await ResolveStationCodeAsync(ct);

        var sessions = await _dbContext.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && !x.IsCancelled)
            .Where(x => x.TransactionType == TransactionType.INBOUND)
            .Where(x => x.Weight2Time.HasValue && x.Weight2Time.Value >= filter.FromTime && x.Weight2Time.Value <= filter.ToTime)
            .OrderBy(x => x.Weight2Time)
            .ThenBy(x => x.SessionNo)
            .ToListAsync(ct);

        var rows = new List<InboundSummaryReportRow>();
        if (sessions.Count > 0)
        {
            rows = await BuildRowsAsync(sessions, filter, stationCode, ct);
            productDisplayName = ResolveProductDisplayName(filter.ProductCode, rows, products);
        }

        var totalNetWeightKg = decimal.Round(rows.Sum(x => x.NetWeightKg), 3, MidpointRounding.AwayFromZero);

        decimal? monthlyCumulative = null;
        decimal? yearlyCumulative = null;
        if (!string.IsNullOrWhiteSpace(filter.ProductCode))
        {
            monthlyCumulative = await CalculateCumulativeNetWeightAsync(
                filter.ProductCode,
                filter.CustomerCode,
                new DateTime(filter.ToTime.Year, filter.ToTime.Month, 1, 0, 0, 0),
                filter.ToTime,
                stationCode,
                ct);
            yearlyCumulative = await CalculateCumulativeNetWeightAsync(
                filter.ProductCode,
                filter.CustomerCode,
                new DateTime(filter.ToTime.Year, 1, 1, 0, 0, 0),
                filter.ToTime,
                stationCode,
                ct);
        }

        return new InboundSummaryReportDocument(
            filter.FromTime,
            filter.ToTime,
            filter.ProductCode,
            productDisplayName,
            filter.CustomerCode,
            preparedByDisplayName,
            rows,
            totalNetWeightKg,
            monthlyCumulative,
            yearlyCumulative);
    }

    public async Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct)
    {
        return await _dbContext.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ProductCode)
            .Select(x => new ReportLookupOptionDto(x.ProductCode, $"{x.ProductCode} - {x.ProductName}"))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReportLookupOptionDto>> GetCustomersAsync(CancellationToken ct)
    {
        return await _dbContext.Customers.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CustomerCode)
            .Select(x => new ReportLookupOptionDto(x.CustomerCode, $"{x.CustomerCode} - {x.CustomerName}"))
            .ToListAsync(ct);
    }

    private async Task<List<InboundSummaryReportRow>> BuildRowsAsync(
        IReadOnlyList<Domain.Entities.WeighingSession> sessions,
        InboundSummaryReportFilter filter,
        string stationCode,
        CancellationToken ct)
    {
        var sessionIds = sessions.Select(x => x.Id).ToList();
        var lines = await _dbContext.WeighingSessionLines.AsNoTracking()
            .Where(x => x.StationCode == stationCode && sessionIds.Contains(x.WeighingSessionId) && !x.IsDeleted)
            .ToListAsync(ct);

        var cutOrderIds = lines.Select(x => x.CutOrderId).Distinct().ToList();
        var cutOrders = await _dbContext.CutOrders.AsNoTracking()
            .Where(x => x.StationCode == stationCode && cutOrderIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(ct);

        var weighTickets = await _dbContext.WeighTickets.AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.WeighingSessionId.HasValue && sessionIds.Contains(x.WeighingSessionId.Value) && !x.IsDeleted)
            .ToListAsync(ct);

        var cutOrdersById = cutOrders.ToDictionary(x => x.Id);
        var linesBySessionId = lines.GroupBy(x => x.WeighingSessionId).ToDictionary(x => x.Key, x => x.ToList());
        var masterTicketBySessionId = weighTickets
            .Where(x => string.Equals(x.RecordRole, "MASTER_SESSION", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.WeighingSessionId!.Value)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.UpdatedAt ?? y.CreatedAt).First());

        var rows = new List<InboundSummaryReportRow>();
        foreach (var session in sessions)
        {
            if (!linesBySessionId.TryGetValue(session.Id, out var sessionLines) || sessionLines.Count == 0)
            {
                continue;
            }

            var lineCutOrders = sessionLines
                .Select(x => cutOrdersById.GetValueOrDefault(x.CutOrderId))
                .Where(x => x != null)
                .Cast<Domain.Entities.CutOrder>()
                .ToList();

            if (lineCutOrders.Count == 0 || !MatchesFilter(lineCutOrders, filter))
            {
                continue;
            }

            var primaryCutOrder = lineCutOrders.OrderBy(x => x.CreatedAt).First();
            var masterTicket = masterTicketBySessionId.GetValueOrDefault(session.Id);
            rows.Add(new InboundSummaryReportRow(
                primaryCutOrder.CustomerName,
                primaryCutOrder.ProductName,
                session.VehiclePlate,
                session.Weight1Time,
                session.Weight2Time,
                session.Weight1,
                session.Weight2,
                decimal.Round(session.NetWeight ?? 0m, 3, MidpointRounding.AwayFromZero),
                ResolveNotes(primaryCutOrder, masterTicket),
                masterTicket?.Weight2User ?? masterTicket?.Weight1User ?? session.UpdatedBy ?? session.CreatedBy));
        }

        return rows;
    }

    private async Task<decimal> CalculateCumulativeNetWeightAsync(
        string productCode,
        string? customerCode,
        DateTime fromTime,
        DateTime toTime,
        string stationCode,
        CancellationToken ct)
    {
        var sessions = await _dbContext.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && !x.IsCancelled)
            .Where(x => x.TransactionType == TransactionType.INBOUND)
            .Where(x => x.Weight2Time.HasValue && x.Weight2Time.Value >= fromTime && x.Weight2Time.Value <= toTime)
            .Select(x => new { x.Id, x.NetWeight })
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            return 0m;
        }

        var sessionIds = sessions.Select(x => x.Id).ToList();
        var lines = await _dbContext.WeighingSessionLines.AsNoTracking()
            .Where(x => x.StationCode == stationCode && sessionIds.Contains(x.WeighingSessionId) && !x.IsDeleted)
            .ToListAsync(ct);

        var cutOrderIds = lines.Select(x => x.CutOrderId).Distinct().ToList();
        var cutOrders = await _dbContext.CutOrders.AsNoTracking()
            .Where(x => x.StationCode == stationCode && cutOrderIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(ct);

        var cutOrdersById = cutOrders.ToDictionary(x => x.Id);
        var linesBySession = lines.GroupBy(x => x.WeighingSessionId).ToDictionary(x => x.Key, x => x.ToList());

        decimal total = 0m;
        foreach (var session in sessions)
        {
            if (!linesBySession.TryGetValue(session.Id, out var sessionLines))
            {
                continue;
            }

            var sessionCutOrders = sessionLines
                .Select(x => cutOrdersById.GetValueOrDefault(x.CutOrderId))
                .Where(x => x != null)
                .Cast<Domain.Entities.CutOrder>()
                .ToList();

            if (!sessionCutOrders.Any(x => string.Equals(x.ProductCode, productCode, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(customerCode)
                && !sessionCutOrders.Any(x => string.Equals(x.CustomerCode, customerCode, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            total += session.NetWeight ?? 0m;
        }

        return decimal.Round(total, 3, MidpointRounding.AwayFromZero);
    }

    private static bool MatchesFilter(IReadOnlyList<Domain.Entities.CutOrder> cutOrders, InboundSummaryReportFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.ProductCode)
            && !cutOrders.Any(x => string.Equals(x.ProductCode, filter.ProductCode, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerCode)
            && !cutOrders.Any(x => string.Equals(x.CustomerCode, filter.CustomerCode, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private async Task<string> ResolveStationCodeAsync(CancellationToken ct)
    {
        var configured = await _appConfigRepository.GetValueAsync(AppConfigKeys.StationCode, ct);
        return string.IsNullOrWhiteSpace(configured) ? "QN01" : configured.Trim();
    }

    private static string? ResolveNotes(Domain.Entities.CutOrder cutOrder, Domain.Entities.WeighTicket? weighTicket)
    {
        var notes = new[] { cutOrder.Notes, weighTicket?.Notes }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    private static string? ResolveProductDisplayName(
        string? productCode,
        IReadOnlyList<InboundSummaryReportRow> rows,
        IReadOnlyList<Domain.Entities.Product> products)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            return null;
        }

        return products.FirstOrDefault(x => string.Equals(x.ProductCode, productCode, StringComparison.OrdinalIgnoreCase))?.ProductName
               ?? rows.Select(x => x.ProductName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }
}

public sealed class InboundSummaryReportExcelExporter : IInboundSummaryReportExporter
{
    public Task ExportAsync(InboundSummaryReportDocument document, string outputPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("BaoCaoNhap");

        BuildHeader(sheet, document);
        var lastTableRow = BuildTable(sheet, document);
        BuildFooter(sheet, document, lastTableRow);
        ApplySheetLayout(sheet, lastTableRow + 6);

        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    private static void BuildHeader(IXLWorksheet sheet, InboundSummaryReportDocument document)
    {
        sheet.Range("B2:G3").Merge().Value = "XI MĂNG CẨM PHẢ\nPHÒNG CHIẾN LƯỢC KINH DOANH";
        var leftHeader = sheet.Range("B2:G3");
        leftHeader.Style.Alignment.WrapText = true;
        leftHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        leftHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        leftHeader.Style.Font.Bold = true;
        leftHeader.Style.Font.FontSize = 12;

        sheet.Range("J2:O3").Merge().Value = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc";
        var rightHeader = sheet.Range("J2:O3");
        rightHeader.Style.Alignment.WrapText = true;
        rightHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rightHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rightHeader.Style.Font.Bold = true;
        rightHeader.Style.Font.FontSize = 12;

        var title = string.IsNullOrWhiteSpace(document.ProductDisplayName)
            ? "BẢNG TỔNG HỢP HÀNG NHẬP"
            : $"BẢNG TỔNG HỢP HÀNG NHẬP {document.ProductDisplayName}";
        sheet.Range("B5:O5").Merge().Value = title;
        var titleRange = sheet.Range("B5:O5");
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;

        sheet.Range("B6:O6").Merge().Value = $"Từ {document.FromTime:HH:mm:ss dd/MM/yyyy} đến {document.ToTime:HH:mm:ss dd/MM/yyyy}";
        var subtitleRange = sheet.Range("B6:O6");
        subtitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        subtitleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        subtitleRange.Style.Font.Bold = true;
        subtitleRange.Style.Font.FontSize = 12;
    }

    private static int BuildTable(IXLWorksheet sheet, InboundSummaryReportDocument document)
    {
        const int headerRow = 8;
        const int dataStartRow = 9;

        var headers = new[]
        {
            "KHÁCH HÀNG",
            "HÀNG HÓA",
            "SỐ PTVC",
            "NGÀY VÀO",
            "GIỜ VÀO",
            "NGÀY RA",
            "GIỜ RA",
            "KL CÂN 1",
            "KL CÂN 2",
            "KL HÀNG (KG)",
            "GHI CHÚ",
            "NGƯỜI CÂN"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(headerRow, i + 2).Value = headers[i];
        }

        var headerRange = sheet.Range(headerRow, 2, headerRow, headers.Length + 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = dataStartRow;
        foreach (var item in document.Rows)
        {
            sheet.Cell(row, 2).Value = item.CustomerName;
            sheet.Cell(row, 3).Value = item.ProductName;
            sheet.Cell(row, 4).Value = item.VehiclePlate;
            sheet.Cell(row, 5).Value = item.Weight1Time;
            sheet.Cell(row, 5).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 6).Value = item.Weight1Time;
            sheet.Cell(row, 6).Style.DateFormat.Format = "HH:mm";
            sheet.Cell(row, 7).Value = item.Weight2Time;
            sheet.Cell(row, 7).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 8).Value = item.Weight2Time;
            sheet.Cell(row, 8).Style.DateFormat.Format = "HH:mm";
            sheet.Cell(row, 9).Value = item.Weight1;
            sheet.Cell(row, 10).Value = item.Weight2;
            sheet.Cell(row, 11).Value = item.NetWeightKg;
            sheet.Cell(row, 12).Value = item.Notes;
            sheet.Cell(row, 13).Value = item.WeigherName;
            row++;
        }

        if (document.Rows.Count > 0)
        {
            sheet.Range(dataStartRow, 9, row - 1, 11).Style.NumberFormat.Format = "#,##0";
        }

        var totalRow = row;
        sheet.Range(totalRow, 2, totalRow, 10).Merge().Value = "TỔNG SỐ LƯỢNG";
        sheet.Range(totalRow, 2, totalRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(totalRow, 2, totalRow, 10).Style.Font.Bold = true;
        sheet.Cell(totalRow, 11).Value = document.TotalNetWeightKg;
        sheet.Cell(totalRow, 11).Style.NumberFormat.Format = "#,##0";

        var summaryRow = totalRow;
        if (document.MonthlyCumulativeNetWeightKg.HasValue)
        {
            summaryRow++;
            sheet.Range(summaryRow, 2, summaryRow, 10).Merge().Value = "LŨY KẾ THÁNG";
            sheet.Range(summaryRow, 2, summaryRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(summaryRow, 2, summaryRow, 10).Style.Font.Bold = true;
            sheet.Cell(summaryRow, 11).Value = document.MonthlyCumulativeNetWeightKg.Value;
            sheet.Cell(summaryRow, 11).Style.NumberFormat.Format = "#,##0";

            summaryRow++;
            sheet.Range(summaryRow, 2, summaryRow, 10).Merge().Value = "LŨY KẾ NĂM";
            sheet.Range(summaryRow, 2, summaryRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(summaryRow, 2, summaryRow, 10).Style.Font.Bold = true;
            sheet.Cell(summaryRow, 11).Value = document.YearlyCumulativeNetWeightKg ?? 0m;
            sheet.Cell(summaryRow, 11).Style.NumberFormat.Format = "#,##0";
        }

        var lastTableRow = summaryRow;
        var bodyRange = sheet.Range(headerRow, 2, lastTableRow, 13);
        bodyRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        bodyRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        bodyRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        bodyRange.Style.Alignment.WrapText = true;
        ApplyReportAlignment(sheet, document.Rows.Count, dataStartRow, totalRow, lastTableRow);

        return lastTableRow;
    }

    private static void ApplyReportAlignment(
        IXLWorksheet sheet,
        int rowCount,
        int dataStartRow,
        int totalRow,
        int lastTableRow)
    {
        if (rowCount > 0)
        {
            sheet.Range(dataStartRow, 2, totalRow - 1, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sheet.Range(dataStartRow, 5, totalRow - 1, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 9, totalRow - 1, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        sheet.Range(totalRow, 11, lastTableRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void BuildFooter(IXLWorksheet sheet, InboundSummaryReportDocument document, int lastTableRow)
    {
        var footerTitleRow = lastTableRow + 3;
        var footerNameRow = lastTableRow + 6;

        sheet.Range(footerTitleRow, 2, footerTitleRow, 4).Merge().Value = "Tổ trưởng";
        sheet.Range(footerTitleRow, 2, footerTitleRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(footerTitleRow, 2, footerTitleRow, 4).Style.Font.Bold = true;

        sheet.Range(footerTitleRow, 11, footerTitleRow, 13).Merge().Value = "Người lập";
        sheet.Range(footerTitleRow, 11, footerTitleRow, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(footerTitleRow, 11, footerTitleRow, 13).Style.Font.Bold = true;

        sheet.Range(footerNameRow, 11, footerNameRow, 13).Merge().Value = document.PreparedByDisplayName;
        sheet.Range(footerNameRow, 11, footerNameRow, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void ApplySheetLayout(IXLWorksheet sheet, int lastRelevantRow)
    {
        sheet.Column(1).Width = 2;
        sheet.Column(14).Width = 2;
        sheet.Column(15).Width = 2;

        sheet.SheetView.FreezeRows(8);
        sheet.Range(2, 2, Math.Max(8, lastRelevantRow), 13).Style.Font.FontName = "Times New Roman";
        sheet.Columns(2, 13).AdjustToContents(2, lastRelevantRow);
        ApplyColumnWidthLimits(sheet, new Dictionary<int, (double Min, double Max)>
        {
            [2] = (18, 36),
            [3] = (18, 36),
            [4] = (12, 18),
            [5] = (12, 14),
            [6] = (9, 11),
            [7] = (12, 14),
            [8] = (9, 11),
            [9] = (11, 14),
            [10] = (11, 14),
            [11] = (13, 16),
            [12] = (16, 34),
            [13] = (14, 24)
        });
        sheet.Rows(2, lastRelevantRow).AdjustToContents();
    }

    private static void ApplyColumnWidthLimits(IXLWorksheet sheet, IReadOnlyDictionary<int, (double Min, double Max)> limits)
    {
        foreach (var (columnNumber, (min, max)) in limits)
        {
            var column = sheet.Column(columnNumber);
            column.Width = Math.Clamp(column.Width, min, max);
        }
    }
}
