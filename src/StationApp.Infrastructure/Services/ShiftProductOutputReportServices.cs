using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Services;

public sealed class ShiftProductOutputReportService : IShiftProductOutputReportService
{
    private readonly StationDbContext _dbContext;
    private readonly IStationScope _stationScope;

    public ShiftProductOutputReportService(StationDbContext dbContext, IStationScope stationScope)
    {
        _dbContext = dbContext;
        _stationScope = stationScope;
    }

    public async Task<ShiftProductOutputReportDocument> BuildAsync(
        ShiftProductOutputReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var productLookup = await _dbContext.Products.AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.IsActive)
            .ToDictionaryAsync(x => x.ProductCode, x => x, StringComparer.OrdinalIgnoreCase, ct);

        var productSeeds = productLookup.Values
            .Where(x => ProductTransactionScopes.AllowsTransaction(x.TransactionScope, TransactionType.OUTBOUND))
            .Select(x => TryResolveReportGroup(x.ProductType, false, out var groupName)
                ? new ShiftProductOutputReportProductSeed(groupName, x.ProductCode, x.ProductName)
                : null)
            .Where(x => x != null)
            .Cast<ShiftProductOutputReportProductSeed>()
            .Where(x => string.IsNullOrWhiteSpace(filter.ProductCode)
                || string.Equals(x.ProductCode, filter.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var domesticFromTime = filter.ReportDate.Date;
        var exportFromTime = DateTime.MinValue;
        var queryFromTime = exportFromTime;

        var lineRows = await (
            from line in _dbContext.WeighingSessionLines.AsNoTracking()
            join session in _dbContext.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            join cutOrder in _dbContext.CutOrders.AsNoTracking()
                on line.CutOrderId equals cutOrder.Id
            where line.StationCode == stationCode
                && session.StationCode == stationCode
                && cutOrder.StationCode == stationCode
                && !line.IsDeleted
                && !session.IsDeleted
                && !session.IsCancelled
                && !session.IsNoLoad
                && !cutOrder.IsDeleted
                && !cutOrder.IsCancelled
                && line.LineStatus == WeighingSessionLineStatus.ALLOCATED
                && session.TransactionType == TransactionType.OUTBOUND
                && cutOrder.TransactionType == TransactionType.OUTBOUND
                && session.Weight2Time.HasValue
                && session.Weight2Time.Value >= queryFromTime
                && session.Weight2Time.Value <= filter.ToTime
                && (session.SessionStatus == WeighingSessionStatus.READY_TO_COMPLETE
                    || session.SessionStatus == WeighingSessionStatus.COMPLETED)
                && (string.IsNullOrWhiteSpace(filter.ProductCode)
                    || line.ProductCode == filter.ProductCode
                    || cutOrder.ProductCode == filter.ProductCode)
            select new
            {
                Session = session,
                Line = line,
                CutOrder = cutOrder
            })
            .ToListAsync(ct);

        var sourceRows = new List<ShiftProductOutputReportSourceRow>();
        foreach (var item in lineRows)
        {
            var isExport = item.CutOrder.IsExportScale || item.CutOrder.IsTemporaryExport;
            var exportedAt = item.Session.Weight2Time!.Value;

            if (!isExport && exportedAt < domesticFromTime)
            {
                continue;
            }

            if (!isExport && item.Session.IsReturnedBrokenTrip)
            {
                continue;
            }

            if (!TryResolveReportGroup(
                    ResolveProductType(item.Line.ProductCode, item.CutOrder.ProductCode, item.CutOrder.ProductType, productLookup),
                    isExport,
                    out var groupName))
            {
                continue;
            }

            var productCode = FirstNonEmpty(item.Line.ProductCode, item.CutOrder.ProductCode, item.Session.ProductCode);
            if (string.IsNullOrWhiteSpace(productCode))
            {
                continue;
            }

            var productName = FirstNonEmpty(
                item.Line.ProductName,
                item.CutOrder.ProductName,
                item.Session.ProductName,
                productLookup.GetValueOrDefault(productCode)?.ProductName,
                productCode)!;

            var signedWeightKg = isExport
                ? ExportReturnedBrokenTripHelper.ResolveSignedWeight(item.Line.ActualAllocatedWeight ?? item.Session.NetWeight, item.Line.IsReturnedBrokenTrip)
                : Math.Abs(item.Line.ActualAllocatedWeight ?? item.Session.NetWeight ?? 0m);

            sourceRows.Add(new ShiftProductOutputReportSourceRow(
                groupName,
                productCode,
                productName,
                item.Session.Id,
                item.CutOrder.Id,
                exportedAt,
                signedWeightKg,
                item.Line.IsReturnedBrokenTrip || item.Session.IsReturnedBrokenTrip));
        }

        return ShiftProductOutputReportCalculator.Build(filter, preparedByDisplayName, productSeeds, sourceRows);
    }

    public async Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var products = await _dbContext.Products.AsNoTracking()
            .Where(x => x.StationCode == stationCode
                && x.IsActive
                && x.ProductType != ProductTypes.Inbound)
            .OrderBy(x => x.ProductCode)
            .ToListAsync(ct);

        return products
            .Where(x => ProductTransactionScopes.AllowsTransaction(x.TransactionScope, TransactionType.OUTBOUND))
            .Select(x => new ReportLookupOptionDto(x.ProductCode, x.ProductCode + " - " + x.ProductName))
            .ToList();
    }

    private static string? ResolveProductType(
        string? lineProductCode,
        string? cutOrderProductCode,
        string? cutOrderProductType,
        IReadOnlyDictionary<string, Product> productLookup)
    {
        if (!string.IsNullOrWhiteSpace(cutOrderProductType))
        {
            return cutOrderProductType;
        }

        var productCode = FirstNonEmpty(lineProductCode, cutOrderProductCode);
        return !string.IsNullOrWhiteSpace(productCode) && productLookup.TryGetValue(productCode, out var product)
            ? product.ProductType
            : null;
    }

    private static bool TryResolveReportGroup(string? productType, bool isExport, out string groupName)
    {
        if (isExport)
        {
            groupName = ShiftProductOutputReportGroups.Export;
            return true;
        }

        var normalized = ProductTypes.Normalize(productType);
        if (string.Equals(normalized, ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase))
        {
            groupName = ShiftProductOutputReportGroups.Bagged;
            return true;
        }

        if (ProductTypes.IsBulkLike(normalized))
        {
            groupName = ShiftProductOutputReportGroups.Bulk;
            return true;
        }

        groupName = string.Empty;
        return false;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
}

public sealed class ShiftProductOutputReportExcelExporter : IShiftProductOutputReportExporter
{
    public Task ExportAsync(ShiftProductOutputReportDocument document, string outputPath, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bao cao");

        BuildHeader(sheet, document);
        var row = 10;
        foreach (var group in document.Groups)
        {
            if (group.Rows.Count == 0)
            {
                continue;
            }

            foreach (var item in group.Rows)
            {
                sheet.Cell(row, 1).Value = item.Stt;
                sheet.Range(row, 2, row, 4).Merge().Value = item.ProductName;
                SetDisplayNumber(sheet.Cell(row, 5), item.ShiftOutputTon);
                SetDisplayNumber(sheet.Cell(row, 6), item.ReferenceCount);
                row++;
            }

            sheet.Range(row, 2, row, 4).Merge().Value = "Tổng " + group.GroupName;
            SetDisplayNumber(sheet.Cell(row, 5), group.TotalShiftOutputTon);
            SetDisplayNumber(sheet.Cell(row, 6), group.TotalReferenceCount);
            sheet.Range(row, 1, row, 6).Style.Font.Bold = true;
            row++;
        }

        sheet.Range(row, 2, row, 4).Merge().Value = "TỔNG TOÀN BỘ";
        SetDisplayNumber(sheet.Cell(row, 5), document.GrandTotalShiftOutputTon);
        SetDisplayNumber(sheet.Cell(row, 6), document.GrandTotalReferenceCount);
        sheet.Range(row, 1, row, 6).Style.Font.Bold = true;
        var lastTableRow = row;
        BuildFooter(sheet, document, lastTableRow);

        var usedRange = sheet.Range(9, 1, row, 6);
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(9, 1, 9, 6).Style.Font.Bold = true;
        sheet.Range(9, 1, 9, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(9, 1, 9, 6).Style.Alignment.WrapText = true;
        sheet.Range(10, 5, row, 5).Style.NumberFormat.Format = "#,##0.###";
        sheet.Range(10, 6, row, 6).Style.NumberFormat.Format = "#,##0";
        sheet.Range(10, 5, row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(1, 1, Math.Max(row + 6, 18), 6).Style.Font.FontName = "Times New Roman";
        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Margins.Top = 0.3;
        sheet.PageSetup.Margins.Bottom = 0.3;
        sheet.PageSetup.Margins.Left = 0.2;
        sheet.PageSetup.Margins.Right = 0.2;

        sheet.Column(1).Width = 5;
        sheet.Column(2).Width = 9;
        sheet.Column(3).Width = 14;
        sheet.Column(4).Width = 20;
        sheet.Column(5).Width = 12;
        sheet.Column(6).Width = 14;
        sheet.Rows(1, row + 6).AdjustToContents();

        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    private static void SetDisplayNumber(IXLCell cell, decimal value)
    {
        if (value == 0m)
        {
            cell.Value = "-";
            return;
        }

        cell.Value = value;
    }

    private static void SetDisplayNumber(IXLCell cell, int value)
    {
        if (value == 0)
        {
            cell.Value = "-";
            return;
        }

        cell.Value = value;
    }

    private static void BuildHeader(IXLWorksheet sheet, ShiftProductOutputReportDocument document)
    {
        sheet.Range("A1:C2").Merge().Value = "XI MĂNG CẨM PHẢ\nPHÒNG CLKD";
        var leftHeader = sheet.Range("A1:C2");
        leftHeader.Style.Alignment.WrapText = true;
        leftHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        leftHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        leftHeader.Style.Font.Bold = true;
        leftHeader.Style.Font.FontSize = 12;

        sheet.Range("D1:F2").Merge().Value = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc";
        var rightHeader = sheet.Range("D1:F2");
        rightHeader.Style.Alignment.WrapText = true;
        rightHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rightHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rightHeader.Style.Font.Bold = true;
        rightHeader.Style.Font.FontSize = 12;

        sheet.Range("A4:F4").Merge().Value = "BÁO CÁO SẢN LƯỢNG XUẤT HÀNG THEO CA";
        var titleRange = sheet.Range("A4:F4");
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;

        sheet.Range("A6:C6").Merge().Value = $"Ngày báo cáo: {document.ReportDate:dd/MM/yyyy}";
        sheet.Range("D6:F6").Merge().Value = $"Ca báo cáo: {document.ShiftCode}";
        sheet.Range("A7:C7").Merge().Value = $"Từ giờ: {document.FromTime:HH:mm:ss}";
        sheet.Range("D7:F7").Merge().Value = $"Đến giờ: {document.ToTime:HH:mm:ss}";
        sheet.Range("A6:F7").Style.Font.Italic = true;

        sheet.Cell("A9").Value = "STT";
        sheet.Range("B9:D9").Merge().Value = "SẢN PHẨM";
        sheet.Cell("E9").Value = "SỐ TẤN";
        sheet.Cell("F9").Value = "CẮT LỆNH/\nCHUYẾN";
    }

    private static void BuildFooter(IXLWorksheet sheet, ShiftProductOutputReportDocument document, int lastTableRow)
    {
        var footerTitleRow = lastTableRow + 3;
        var footerNameRow = lastTableRow + 6;

        sheet.Range(footerTitleRow, 4, footerTitleRow, 6).Merge().Value = "Người lập";
        sheet.Range(footerTitleRow, 4, footerTitleRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(footerTitleRow, 4, footerTitleRow, 6).Style.Font.Bold = true;

        sheet.Range(footerNameRow, 4, footerNameRow, 6).Merge().Value = document.PreparedByDisplayName;
        sheet.Range(footerNameRow, 4, footerNameRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }
}
