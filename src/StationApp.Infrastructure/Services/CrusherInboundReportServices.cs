using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Services;

public sealed class CrusherInboundReportService : ICrusherInboundReportService
{
    private readonly StationDbContext _dbContext;
    private readonly IStationScope _stationScope;

    public CrusherInboundReportService(StationDbContext dbContext, IStationScope stationScope)
    {
        _dbContext = dbContext;
        _stationScope = stationScope;
    }

    public async Task<CrusherInboundReportDocument> BuildAsync(
        CrusherInboundReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var products = await _dbContext.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(ct);

        var sessions = await _dbContext.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && !x.IsCancelled)
            .Where(x => x.TransactionType == TransactionType.INBOUND)
            .Where(x => x.SessionStatus == WeighingSessionStatus.COMPLETED)
            .Where(x => x.InternalVehicleNo != null && x.InternalVehicleNo != string.Empty)
            .Where(x => x.Weight2Time.HasValue && x.Weight2Time.Value >= filter.FromTime && x.Weight2Time.Value <= filter.ToTime)
            .OrderBy(x => x.Weight2Time)
            .ThenBy(x => x.SessionNo)
            .ToListAsync(ct);

        var rows = await BuildRowsAsync(sessions, filter, stationCode, ct);
        var productDisplayName = ResolveProductDisplayName(filter.ProductCode, rows, products);
        var totalNetWeightKg = decimal.Round(rows.Sum(x => x.NetWeightKg), 3, MidpointRounding.AwayFromZero);

        return new CrusherInboundReportDocument(
            filter.FromTime,
            filter.ToTime,
            filter.ProductCode,
            productDisplayName,
            filter.CustomerCode,
            preparedByDisplayName,
            rows,
            totalNetWeightKg);
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

    private async Task<List<CrusherInboundReportRow>> BuildRowsAsync(
        IReadOnlyList<Domain.Entities.WeighingSession> sessions,
        CrusherInboundReportFilter filter,
        string stationCode,
        CancellationToken ct)
    {
        if (sessions.Count == 0)
        {
            return [];
        }

        var sessionIds = sessions.Select(x => x.Id).ToList();
        var weighTickets = await _dbContext.WeighTickets.AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.WeighingSessionId.HasValue && sessionIds.Contains(x.WeighingSessionId.Value) && !x.IsDeleted)
            .ToListAsync(ct);

        var masterTicketBySessionId = weighTickets
            .Where(x => string.Equals(x.RecordRole, "MASTER_SESSION", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.WeighingSessionId!.Value)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.UpdatedAt ?? y.CreatedAt).First());

        var rows = new List<CrusherInboundReportRow>();
        foreach (var session in sessions)
        {
            if (!MatchesFilter(session, filter))
            {
                continue;
            }

            var masterTicket = masterTicketBySessionId.GetValueOrDefault(session.Id);
            var isSingleWeigh = IsSingleWeighMode(session.WeighingMode);
            rows.Add(new CrusherInboundReportRow(
                BusinessNumberFormatter.ToDisplay(session.SessionNo),
                session.InternalVehicleNo ?? session.VehiclePlate,
                session.DriverName,
                session.CustomerName,
                session.ProductName,
                isSingleWeigh ? "C\u00E2n 1 l\u1EA7n" : "C\u00E2n 2 l\u1EA7n",
                session.Weight1Time,
                session.Weight2Time,
                isSingleWeigh ? session.StandardTareWeightSnapshot : null,
                session.Weight1,
                session.Weight2,
                decimal.Round(session.NetWeight ?? 0m, 3, MidpointRounding.AwayFromZero),
                masterTicket?.Notes,
                masterTicket?.Weight2User ?? masterTicket?.Weight1User ?? session.UpdatedBy ?? session.CreatedBy));
        }

        return rows;
    }

    private static bool MatchesFilter(Domain.Entities.WeighingSession session, CrusherInboundReportFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.ProductCode)
            && !string.Equals(session.ProductCode, filter.ProductCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerCode)
            && !string.Equals(session.CustomerCode, filter.CustomerCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsSingleWeighMode(string? weighingMode)
        => string.Equals(weighingMode, CrusherWeighingModes.SingleWithStandardTare, StringComparison.OrdinalIgnoreCase);

    private static string ResolveWeighingModeDisplay(string? weighingMode)
    {
        return IsSingleWeighMode(weighingMode)
            ? "Cân 1 lần"
            : "Cân 2 lần";
    }

    private static string? ResolveProductDisplayName(
        string? productCode,
        IReadOnlyList<CrusherInboundReportRow> rows,
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

public sealed class CrusherInboundReportExcelExporter : ICrusherInboundReportExporter
{
    public Task ExportAsync(CrusherInboundReportDocument document, string outputPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("BaoCaoNhapTramDap");

        BuildHeader(sheet, document);
        var lastTableRow = BuildTable(sheet, document);
        BuildFooter(sheet, document, lastTableRow);
        ApplySheetLayout(sheet, lastTableRow + 6);

        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    private static void BuildHeader(IXLWorksheet sheet, CrusherInboundReportDocument document)
    {
        sheet.Range("B2:H3").Merge().Value = "XI MĂNG CẨM PHẢ\nPHÒNG CHIẾN LƯỢC KINH DOANH";
        var leftHeader = sheet.Range("B2:H3");
        leftHeader.Style.Alignment.WrapText = true;
        leftHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        leftHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        leftHeader.Style.Font.Bold = true;
        leftHeader.Style.Font.FontSize = 12;

        sheet.Range("K2:R3").Merge().Value = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc";
        var rightHeader = sheet.Range("K2:R3");
        rightHeader.Style.Alignment.WrapText = true;
        rightHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rightHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rightHeader.Style.Font.Bold = true;
        rightHeader.Style.Font.FontSize = 12;

        var title = string.IsNullOrWhiteSpace(document.ProductDisplayName)
            ? "BÁO CÁO NHẬP TRẠM ĐẬP"
            : $"BÁO CÁO NHẬP TRẠM ĐẬP {document.ProductDisplayName}";
        sheet.Range("B5:R5").Merge().Value = title;
        var titleRange = sheet.Range("B5:R5");
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;

        sheet.Range("B6:R6").Merge().Value = $"Từ {document.FromTime:HH:mm:ss dd/MM/yyyy} đến {document.ToTime:HH:mm:ss dd/MM/yyyy}";
        var subtitleRange = sheet.Range("B6:R6");
        subtitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        subtitleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        subtitleRange.Style.Font.Bold = true;
        subtitleRange.Style.Font.FontSize = 12;
    }

    private static int BuildTable(IXLWorksheet sheet, CrusherInboundReportDocument document)
    {
        const int headerRow = 8;
        const int dataStartRow = 9;

        var headers = new[]
        {
            "STT",
            "SỐ LƯỢT CÂN",
            "SỐ XE NỘI BỘ",
            "TÀI XẾ",
            "KHÁCH HÀNG",
            "HÀNG HÓA",
            "CHẾ ĐỘ CÂN",
            "NGÀY CÂN 1",
            "GIỜ CÂN 1",
            "NGÀY CÂN 2",
            "GIỜ CÂN 2",
            "CÂN LẦN 1 (KG)",
            "TL XE CHUẨN (KG)",
            "CÂN LẦN 2 (KG)",
            "TL HÀNG (KG)",
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
        var index = 1;
        foreach (var item in document.Rows)
        {
            sheet.Cell(row, 2).Value = index++;
            sheet.Cell(row, 3).Value = item.SessionNo;
            sheet.Cell(row, 4).Value = item.InternalVehicleNo;
            sheet.Cell(row, 5).Value = item.DriverName;
            sheet.Cell(row, 6).Value = item.CustomerName;
            sheet.Cell(row, 7).Value = item.ProductName;
            sheet.Cell(row, 8).Value = item.WeighingModeDisplay;
            sheet.Cell(row, 9).Value = item.Weight1Time;
            sheet.Cell(row, 9).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 10).Value = item.Weight1Time;
            sheet.Cell(row, 10).Style.DateFormat.Format = "HH:mm";
            sheet.Cell(row, 11).Value = item.Weight2Time;
            sheet.Cell(row, 11).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 12).Value = item.Weight2Time;
            sheet.Cell(row, 12).Style.DateFormat.Format = "HH:mm";
            sheet.Cell(row, 13).Value = item.Weight1;
            sheet.Cell(row, 14).Value = item.StandardTareWeightKg;
            sheet.Cell(row, 15).Value = item.Weight2;
            sheet.Cell(row, 16).Value = item.NetWeightKg;
            sheet.Cell(row, 17).Value = item.Notes;
            sheet.Cell(row, 18).Value = item.WeigherName;
            row++;
        }

        if (document.Rows.Count > 0)
        {
            sheet.Range(dataStartRow, 13, row - 1, 16).Style.NumberFormat.Format = "#,##0";
        }

        var totalRow = row;
        sheet.Range(totalRow, 2, totalRow, 15).Merge().Value = "TỔNG SỐ LƯỢNG";
        sheet.Range(totalRow, 2, totalRow, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(totalRow, 2, totalRow, 15).Style.Font.Bold = true;
        sheet.Cell(totalRow, 16).Value = document.TotalNetWeightKg;
        sheet.Cell(totalRow, 16).Style.NumberFormat.Format = "#,##0";

        var bodyRange = sheet.Range(headerRow, 2, totalRow, 18);
        bodyRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        bodyRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        bodyRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        bodyRange.Style.Alignment.WrapText = true;
        ApplyReportAlignment(sheet, document.Rows.Count, dataStartRow, totalRow);

        return totalRow;
    }

    private static void ApplyReportAlignment(IXLWorksheet sheet, int rowCount, int dataStartRow, int totalRow)
    {
        if (rowCount > 0)
        {
            sheet.Range(dataStartRow, 2, totalRow - 1, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sheet.Range(dataStartRow, 2, totalRow - 1, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 8, totalRow - 1, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 13, totalRow - 1, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        sheet.Cell(totalRow, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void BuildFooter(IXLWorksheet sheet, CrusherInboundReportDocument document, int lastTableRow)
    {
        var footerTitleRow = lastTableRow + 3;
        var footerNameRow = lastTableRow + 6;

        sheet.Range(footerTitleRow, 2, footerTitleRow, 5).Merge().Value = "Tổ trưởng";
        sheet.Range(footerTitleRow, 2, footerTitleRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(footerTitleRow, 2, footerTitleRow, 5).Style.Font.Bold = true;

        sheet.Range(footerTitleRow, 15, footerTitleRow, 18).Merge().Value = "Người lập";
        sheet.Range(footerTitleRow, 15, footerTitleRow, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(footerTitleRow, 15, footerTitleRow, 18).Style.Font.Bold = true;

        sheet.Range(footerNameRow, 15, footerNameRow, 18).Merge().Value = document.PreparedByDisplayName;
        sheet.Range(footerNameRow, 15, footerNameRow, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void ApplySheetLayout(IXLWorksheet sheet, int lastRelevantRow)
    {
        sheet.Column(1).Width = 2;
        sheet.Column(19).Width = 2;

        sheet.SheetView.FreezeRows(8);
        sheet.Range(2, 2, Math.Max(8, lastRelevantRow), 18).Style.Font.FontName = "Times New Roman";
        sheet.Columns(2, 18).AdjustToContents(2, lastRelevantRow);
        ApplyColumnWidthLimits(sheet, new Dictionary<int, (double Min, double Max)>
        {
            [2] = (6, 8),
            [3] = (13, 18),
            [4] = (13, 18),
            [5] = (16, 26),
            [6] = (20, 36),
            [7] = (18, 36),
            [8] = (12, 16),
            [9] = (12, 14),
            [10] = (9, 11),
            [11] = (12, 14),
            [12] = (9, 11),
            [13] = (13, 17),
            [14] = (13, 16),
            [15] = (13, 16),
            [16] = (13, 16),
            [17] = (16, 34),
            [18] = (14, 24)
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
