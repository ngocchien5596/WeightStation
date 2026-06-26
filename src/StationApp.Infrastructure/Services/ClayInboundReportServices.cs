using System.IO;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Services;

public sealed class ClayInboundReportService : IClayInboundReportService
{
    private readonly StationDbContext _dbContext;
    private readonly IStationScope _stationScope;

    public ClayInboundReportService(StationDbContext dbContext, IStationScope stationScope)
    {
        _dbContext = dbContext;
        _stationScope = stationScope;
    }

    public async Task<ClayInboundReportDocument> BuildAsync(
        ClayInboundReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var stationName = await _dbContext.Stations.AsNoTracking()
            .Where(x => x.StationCode == stationCode)
            .Select(x => x.StationName)
            .FirstOrDefaultAsync(ct) ?? stationCode;

        var sessions = await _dbContext.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && !x.IsCancelled)
            .Where(x => x.TransactionType == TransactionType.INBOUND)
            .Where(x => x.SessionStatus == WeighingSessionStatus.COMPLETED)
            .Where(x => x.InternalVehicleNo != null && x.InternalVehicleNo != string.Empty)
            .Where(x => x.Weight2Time.HasValue && x.Weight2Time.Value >= filter.FromTime && x.Weight2Time.Value <= filter.ToTime)
            .OrderBy(x => x.Weight2Time)
            .ThenBy(x => x.SessionNo)
            .ToListAsync(ct);

        var rows = BuildRows(sessions, filter);
        var totalNetWeightTon = decimal.Round(rows.Sum(x => x.NetWeightTon), 3, MidpointRounding.AwayFromZero);

        return new ClayInboundReportDocument(
            filter.FromTime,
            filter.ToTime,
            filter.VehicleKeyword,
            stationName,
            preparedByDisplayName,
            null,
            rows,
            totalNetWeightTon);
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

    private static List<ClayInboundReportRow> BuildRows(
        IReadOnlyList<Domain.Entities.WeighingSession> sessions,
        ClayInboundReportFilter filter)
    {
        var rows = new List<ClayInboundReportRow>();
        foreach (var session in sessions)
        {
            if (!MatchesFilter(session, filter))
            {
                continue;
            }

            rows.Add(new ClayInboundReportRow(
                rows.Count + 1,
                NormalizeSessionNo(BusinessNumberFormatter.ToDisplay(session.SessionNo)),
                session.InternalVehicleNo ?? session.VehiclePlate ?? string.Empty,
                session.CustomerName,
                session.ProductName,
                session.Weight2Time,
                ToTon(session.Weight1),
                ResolveTareWeightTon(session),
                ToTon(session.NetWeight)));
        }

        return rows;
    }

    private static bool MatchesFilter(Domain.Entities.WeighingSession session, ClayInboundReportFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.VehicleKeyword))
        {
            var keyword = filter.VehicleKeyword.Trim();
            var internalVehicleNo = session.InternalVehicleNo ?? string.Empty;
            var vehiclePlate = session.VehiclePlate ?? string.Empty;
            if (!internalVehicleNo.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                && !vehiclePlate.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static decimal ResolveTareWeightTon(Domain.Entities.WeighingSession session)
    {
        if (session.StandardTareWeightSnapshot.HasValue)
        {
            return ToTon(session.StandardTareWeightSnapshot.Value);
        }

        return ToTon(session.Weight2);
    }

    private static decimal ToTon(decimal? weightKg)
        => decimal.Round((weightKg ?? 0m) / 1000m, 3, MidpointRounding.AwayFromZero);

    private static string NormalizeSessionNo(string sessionNo)
    {
        if (sessionNo.StartsWith("QN02-", StringComparison.OrdinalIgnoreCase))
        {
            return sessionNo["QN02-".Length..];
        }

        if (sessionNo.StartsWith("QN03-", StringComparison.OrdinalIgnoreCase))
        {
            return sessionNo["QN03-".Length..];
        }

        return sessionNo;
    }
}

public sealed class ClayInboundReportExcelExporter : IClayInboundReportExporter
{
    public Task ExportAsync(ClayInboundReportDocument document, string outputPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("BaoCaoCanHangMoSet");

        BuildHeader(sheet, document);
        var lastTableRow = BuildTable(sheet, document);
        BuildFooter(sheet, document, lastTableRow);
        ApplySheetLayout(sheet, lastTableRow + 8);

        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    private static void BuildHeader(IXLWorksheet sheet, ClayInboundReportDocument document)
    {
        sheet.Range("B1:D1").Merge().Value = "CÔNG TY CỔ PHẦN XI MĂNG CẨM PHẢ";
        var companyName = sheet.Range("B1:D1");
        companyName.Style.Font.Bold = true;
        companyName.Style.Font.FontName = "Times New Roman";
        companyName.Style.Font.FontSize = 12;
        companyName.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        companyName.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        sheet.Range("B2:D2").Merge().Value = "Địa chỉ: Km6, Quốc lộ 18A, Cẩm Thạch, Cẩm Phả, Quảng Ninh";
        sheet.Range("B3:D3").Merge().Value = "Điện thoại: (84-203) 3.721.995 - (84-203) 3.721.996";
        sheet.Range("B2:D3").Style.Font.FontName = "Times New Roman";
        sheet.Range("B2:D3").Style.Font.FontSize = 11;

        if (document.LogoBytes is { Length: > 0 })
        {
            using var stream = new MemoryStream(document.LogoBytes);
            var picture = sheet.AddPicture(stream);
            picture.Placement = XLPicturePlacement.FreeFloating;
            picture.Width = 55;
            picture.Height = 57;
            picture.Left = Math.Max(0, (int)Math.Round((84d - picture.Width) / 2d));
            picture.Top = 0;
        }

        sheet.Range("G1:H2").Merge().Value = "BÁO CÁO CÂN HÀNG MỎ SÉT";
        var titleRange = sheet.Range("G1:H2");
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontName = "Times New Roman";
        titleRange.Style.Font.FontSize = 16;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

        sheet.Range("G3:H3").Merge().Value = BuildTimeRangeText(document.FromTime, document.ToTime);
        var timeRange = sheet.Range("G3:H3");
        timeRange.Style.Font.FontName = "Times New Roman";
        timeRange.Style.Font.FontSize = 11;
        timeRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        timeRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
    }

    private static int BuildTable(IXLWorksheet sheet, ClayInboundReportDocument document)
    {
        const int headerRow = 5;
        const int dataStartRow = 6;

        var headers = new[]
        {
            "STT",
            "Số phiếu",
            "Số xe",
            "Ngày cân",
            "Tổng (tấn)",
            "Bì (tấn)",
            "Hàng (tấn)",
            "Khách hàng",
            "Hàng hóa"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(headerRow, i + 1).Value = headers[i];
        }

        var headerRange = sheet.Range(headerRow, 1, headerRow, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontName = "Times New Roman";
        headerRange.Style.Font.FontSize = 11;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = dataStartRow;
        for (var index = 0; index < document.Rows.Count; index++)
        {
            var item = document.Rows[index];
            sheet.Cell(row, 1).Value = item.RowNo;
            sheet.Cell(row, 2).Value = item.SessionNo;
            sheet.Cell(row, 3).Value = item.InternalVehicleNo;
            sheet.Cell(row, 4).Value = item.Weight2Time;
            sheet.Cell(row, 4).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            sheet.Cell(row, 5).Value = item.GrossWeightTon;
            sheet.Cell(row, 6).Value = item.TareWeightTon;
            sheet.Cell(row, 7).Value = item.NetWeightTon;
            sheet.Cell(row, 8).Value = item.CustomerName;
            sheet.Cell(row, 9).Value = item.ProductName;
            row++;
        }

        if (document.Rows.Count > 0)
        {
            sheet.Range(dataStartRow, 5, row - 1, 7).Style.NumberFormat.Format = "#,##0.000";
        }

        var totalRow = row;
        sheet.Range(totalRow, 1, totalRow, 4).Merge().Value = "Cộng tổng:";
        sheet.Range(totalRow, 1, totalRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(totalRow, 1, totalRow, 4).Style.Font.Bold = true;
        sheet.Cell(totalRow, 7).Value = document.TotalNetWeightTon;
        sheet.Cell(totalRow, 7).Style.NumberFormat.Format = "#,##0.000";
        sheet.Cell(totalRow, 7).Style.Font.Bold = true;

        var tableRange = sheet.Range(headerRow, 1, totalRow, 9);
        tableRange.Style.Font.FontName = "Times New Roman";
        tableRange.Style.Font.FontSize = 11;
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        tableRange.Style.Alignment.WrapText = true;

        if (document.Rows.Count > 0)
        {
            sheet.Range(dataStartRow, 1, totalRow - 1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 4, totalRow - 1, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 5, totalRow - 1, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 8, totalRow - 1, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        return totalRow;
    }

    private static void BuildFooter(IXLWorksheet sheet, ClayInboundReportDocument document, int lastTableRow)
    {
        var signatureTitleRow = lastTableRow + 2;
        var signatureNameRow = lastTableRow + 6;
        var footerRow = lastTableRow + 8;

        sheet.Range(signatureTitleRow, 2, signatureTitleRow, 4).Merge().Value = "ĐẠI DIỆN ĐƠN VỊ KHAI THÁC";
        sheet.Range(signatureTitleRow, 2, signatureTitleRow, 4).Style.Font.Bold = true;
        sheet.Range(signatureTitleRow, 2, signatureTitleRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(signatureTitleRow, 2, signatureTitleRow, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

        sheet.Range(signatureTitleRow, 8, signatureTitleRow, 9).Merge().Value = "ĐẠI DIỆN PHÂN XƯỞNG KHAI THÁC";
        sheet.Range(signatureTitleRow, 8, signatureTitleRow, 9).Style.Font.Bold = true;
        sheet.Range(signatureTitleRow, 8, signatureTitleRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(signatureTitleRow, 8, signatureTitleRow, 9).Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

        sheet.Range(signatureNameRow, 8, signatureNameRow, 9).Merge().Value = document.PreparedByDisplayName;
        sheet.Range(signatureNameRow, 8, signatureNameRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var footerRange = sheet.Range(footerRow, 1, footerRow, 9);
        footerRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        footerRange.Style.Font.FontName = "Times New Roman";
        footerRange.Style.Font.FontSize = 11;

        sheet.Cell(footerRow, 1).Value = document.StationName;
        sheet.Cell(footerRow, 1).Style.Font.Bold = true;
        sheet.Cell(footerRow, 5).Value = $"Thời gian in: {DateTime.Now:dd/MM/yyyy HH:mm}";
        sheet.Cell(footerRow, 5).Style.Font.Italic = true;
        sheet.Cell(footerRow, 9).Value = "Trang: 1/1";
    }

    private static void ApplySheetLayout(IXLWorksheet sheet, int lastRelevantRow)
    {
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Margins.Top = 0.3;
        sheet.PageSetup.Margins.Bottom = 0.3;
        sheet.PageSetup.Margins.Left = 0.2;
        sheet.PageSetup.Margins.Right = 0.2;

        sheet.Column(1).Width = 12;
        sheet.Column(2).Width = 15;
        sheet.Column(3).Width = 12;
        sheet.Column(4).Width = 27;
        sheet.Column(5).Width = 12;
        sheet.Column(6).Width = 12;
        sheet.Column(7).Width = 12;
        sheet.Column(8).Width = 37;
        sheet.Column(9).Width = 16;

        sheet.Row(1).Height = 24;
        sheet.Row(3).Height = 20;
        sheet.Row(lastRelevantRow).Height = 16;
        sheet.Rows(1, lastRelevantRow).AdjustToContents();
    }

    private static string BuildTimeRangeText(DateTime fromTime, DateTime toTime)
    {
        if (fromTime.Date == toTime.Date)
        {
            return $"Thời gian: Từ {fromTime:HH:mm} đến {toTime:HH:mm} ngày {fromTime:dd/MM/yyyy}";
        }

        return $"Thời gian: Từ {fromTime:HH:mm dd/MM/yyyy} đến {toTime:HH:mm dd/MM/yyyy}";
    }
}
