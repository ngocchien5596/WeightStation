using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Services;

public sealed class ExportSummaryReportService : IExportSummaryReportService
{
    private const decimal BagWeightKg = 50m;

    private readonly StationDbContext _dbContext;
    private readonly IAppConfigRepository _appConfigRepository;

    public ExportSummaryReportService(
        StationDbContext dbContext,
        IAppConfigRepository appConfigRepository)
    {
        _dbContext = dbContext;
        _appConfigRepository = appConfigRepository;
    }

    public async Task<ExportSummaryReportDocument> BuildAsync(
        ExportSummaryReportFilter filter,
        string preparedByDisplayName,
        CancellationToken ct)
    {
        var toleranceKgPerBag = await ResolveToleranceAsync(ct);
        var stationCode = await ResolveStationCodeAsync(ct);

        var sessions = await _dbContext.WeighingSessions.AsNoTracking()
            .Where(x => x.StationCode == stationCode && !x.IsDeleted && !x.IsCancelled)
            .Where(x => !x.IsNoLoad)
            .Where(x => x.TransactionType == TransactionType.OUTBOUND)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.SessionNo)
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            return new ExportSummaryReportDocument(
                filter.FromTime,
                filter.ToTime,
                filter.ProductCode,
                filter.CustomerCode,
                toleranceKgPerBag,
                preparedByDisplayName,
                []);
        }

        var sessionIds = sessions.Select(x => x.Id).ToList();

        var lines = await _dbContext.WeighingSessionLines.AsNoTracking()
            .Where(x => x.StationCode == stationCode && sessionIds.Contains(x.WeighingSessionId) && !x.IsDeleted)
            .OrderBy(x => x.SequenceNo)
            .ToListAsync(ct);

        var cutOrderIds = lines.Select(x => x.CutOrderId).Distinct().ToList();
        var lineIds = lines.Select(x => x.Id).Distinct().ToList();

        var cutOrders = await _dbContext.CutOrders.AsNoTracking()
            .Where(x => x.StationCode == stationCode && cutOrderIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(ct);

        var weighTickets = await _dbContext.WeighTickets.AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.WeighingSessionId.HasValue && sessionIds.Contains(x.WeighingSessionId.Value) && !x.IsDeleted)
            .ToListAsync(ct);

        var deliveryTickets = await _dbContext.DeliveryTickets.AsNoTracking()
            .Where(x =>
                x.StationCode == stationCode
                && !x.IsDeleted
                && ((x.WeighingSessionId.HasValue && sessionIds.Contains(x.WeighingSessionId.Value))
                    || (x.WeighingSessionLineId.HasValue && lineIds.Contains(x.WeighingSessionLineId.Value))))
            .ToListAsync(ct);

        var productCodes = cutOrders
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductCode))
            .Select(x => x.ProductCode!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var productLookup = await _dbContext.Products.AsNoTracking()
            .Where(x => productCodes.Contains(x.ProductCode))
            .ToDictionaryAsync(x => x.ProductCode, x => x, StringComparer.OrdinalIgnoreCase, ct);

        var cutOrdersById = cutOrders.ToDictionary(x => x.Id);
        var linesBySession = lines
            .GroupBy(x => x.WeighingSessionId)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.SequenceNo).ToList());

        var masterWeighTicketsBySession = weighTickets
            .Where(x => string.Equals(x.RecordRole, WeighTicketRecordRoles.MasterSession, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.WeighingSessionId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.UpdatedAt ?? y.CreatedAt).First());

        var weighTicketsByCutOrderId = weighTickets
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.CutOrderId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.IsPrimaryDisplay)
                    .ThenByDescending(y => string.Equals(y.RecordRole, WeighTicketRecordRoles.CutOrderDerived, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(y => y.UpdatedAt ?? y.CreatedAt)
                    .First());

        var deliveryTicketsBySession = deliveryTickets
            .Where(x => x.WeighingSessionId.HasValue)
            .GroupBy(x => x.WeighingSessionId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.SplitSequence ?? 0).ThenBy(y => y.CreatedAt).ToList());

        var deliveryTicketsByLineId = deliveryTickets
            .Where(x => x.WeighingSessionLineId.HasValue)
            .GroupBy(x => x.WeighingSessionLineId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.SplitSequence ?? 0).ThenBy(y => y.CreatedAt).ToList());

        var rows = new List<ExportSummaryReportRow>();
        foreach (var session in sessions)
        {
            if (!linesBySession.TryGetValue(session.Id, out var sessionLines) || sessionLines.Count == 0)
            {
                continue;
            }

            var lineCutOrders = sessionLines
                .Select(x => cutOrdersById.GetValueOrDefault(x.CutOrderId))
                .Where(x => x != null)
                .Cast<CutOrder>()
                .ToList();

            if (lineCutOrders.Count == 0 || !MatchesFilter(lineCutOrders, filter))
            {
                continue;
            }

            if (!TryResolveReportedAt(session, lineCutOrders, out var reportedAt))
            {
                continue;
            }

            if (reportedAt < filter.FromTime || reportedAt > filter.ToTime)
            {
                continue;
            }

            var primaryWeighTicket = masterWeighTicketsBySession.GetValueOrDefault(session.Id);

            foreach (var cutOrderLineGroup in sessionLines.GroupBy(x => x.CutOrderId).OrderBy(x => x.Min(y => y.SequenceNo)))
            {
                if (!cutOrdersById.TryGetValue(cutOrderLineGroup.Key, out var cutOrder)
                    || !MatchesFilter([cutOrder], filter))
                {
                    continue;
                }

                var cutOrderLines = cutOrderLineGroup.OrderBy(x => x.SequenceNo).ToList();
                var relatedDeliveries = ResolveRelatedDeliveries(
                    session,
                    cutOrderLines,
                    deliveryTicketsBySession,
                    deliveryTicketsByLineId)
                    .Where(x => x.CutOrderId == cutOrder.Id
                        || (x.ErpCutOrderId.Length > 0
                            && string.Equals(x.ErpCutOrderId, cutOrder.ErpCutOrderId, StringComparison.OrdinalIgnoreCase))
                        || (x.WeighingSessionLineId.HasValue
                            && cutOrderLines.Any(line => line.Id == x.WeighingSessionLineId.Value)))
                    .DistinctBy(x => x.Id)
                    .ToList();

                var cutOrderWeighTicket = weighTicketsByCutOrderId.GetValueOrDefault(cutOrder.Id) ?? primaryWeighTicket;

                rows.Add(BuildRow(
                    reportedAt,
                    session,
                    cutOrderLines,
                    [cutOrder],
                    relatedDeliveries,
                    cutOrderWeighTicket,
                    productLookup,
                    toleranceKgPerBag));
            }
        }

        return new ExportSummaryReportDocument(
            filter.FromTime,
            filter.ToTime,
            filter.ProductCode,
            filter.CustomerCode,
            toleranceKgPerBag,
            preparedByDisplayName,
            rows
                .OrderBy(x => x.ExportedAt)
                .ThenBy(x => x.VehiclePlate)
                .ThenBy(x => x.WeighTicketNo)
                .ToList());
    }

    public async Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct)
    {
        return await _dbContext.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ProductCode)
            .Select(x => new ReportLookupOptionDto(x.ProductCode, $"{x.ProductCode} - {x.ProductName}"))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReportLookupOptionDto>> GetCustomerOptionsAsync(CancellationToken ct)
    {
        return await _dbContext.Customers.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CustomerCode)
            .Select(x => new ReportLookupOptionDto(x.CustomerCode, $"{x.CustomerCode} - {x.CustomerName}"))
            .ToListAsync(ct);
    }

    private async Task<decimal> ResolveToleranceAsync(CancellationToken ct)
    {
        var configured = await _appConfigRepository.GetValueAsync(AppConfigKeys.ToleranceKgPerBag, ct);
        return decimal.TryParse(configured, out var tolerance)
            ? tolerance
            : AppConfigDefaults.DefaultToleranceKgPerBag;
    }

    private async Task<string> ResolveStationCodeAsync(CancellationToken ct)
    {
        var configured = await _appConfigRepository.GetValueAsync(AppConfigKeys.StationCode, ct);
        return string.IsNullOrWhiteSpace(configured) ? "QN01" : configured.Trim();
    }

    private static bool MatchesFilter(IReadOnlyList<CutOrder> cutOrders, ExportSummaryReportFilter filter)
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

    private static bool TryResolveReportedAt(
        WeighingSession session,
        IReadOnlyList<CutOrder> cutOrders,
        out DateTime reportedAt)
    {
        var hasExportScale = cutOrders.Any(x => x.IsExportScale);
        if (hasExportScale)
        {
            if (!session.Weight2Time.HasValue)
            {
                reportedAt = default;
                return false;
            }

            reportedAt = session.Weight2Time.Value;
            return true;
        }

        if (session.SessionStatus != WeighingSessionStatus.COMPLETED)
        {
            reportedAt = default;
            return false;
        }

        if (!cutOrders.Any(x => x.ProcessingStage == ProcessingStage.OUT_YARD))
        {
            reportedAt = default;
            return false;
        }

        reportedAt = session.Weight2Time ?? session.CreatedAt;
        return true;
    }

    private static List<DeliveryTicket> ResolveRelatedDeliveries(
        WeighingSession session,
        IReadOnlyList<WeighingSessionLine> lines,
        IReadOnlyDictionary<Guid, List<DeliveryTicket>> deliveryTicketsBySession,
        IReadOnlyDictionary<Guid, List<DeliveryTicket>> deliveryTicketsByLineId)
    {
        var related = new List<DeliveryTicket>();
        foreach (var line in lines)
        {
            if (deliveryTicketsByLineId.TryGetValue(line.Id, out var lineDeliveries))
            {
                related.AddRange(lineDeliveries);
            }
        }

        if (related.Count > 0)
        {
            return related
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.SplitSequence ?? 0)
                .ThenBy(x => x.CreatedAt)
                .ToList();
        }

        return deliveryTicketsBySession.GetValueOrDefault(session.Id, [])
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.SplitSequence ?? 0)
            .ThenBy(x => x.CreatedAt)
            .ToList();
    }

    private static ExportSummaryReportRow BuildRow(
        DateTime reportedAt,
        WeighingSession session,
        IReadOnlyList<WeighingSessionLine> lines,
        IReadOnlyList<CutOrder> cutOrders,
        IReadOnlyList<DeliveryTicket> deliveries,
        WeighTicket? weighTicket,
        IReadOnlyDictionary<string, Product> productLookup,
        decimal toleranceKgPerBag)
    {
        var cutOrderLookup = cutOrders.ToDictionary(x => x.Id);
        var enrichedLines = lines.Select(line =>
        {
            var cutOrder = cutOrderLookup[line.CutOrderId];
            var productType = ResolveProductType(cutOrder, productLookup);
            var isBagged = string.Equals(productType, ProductTypes.Bagged, StringComparison.OrdinalIgnoreCase);

            return new
            {
                Line = line,
                CutOrder = cutOrder,
                ProductType = productType,
                IsBagged = isBagged
            };
        }).ToList();

        var isExportScaleSession = cutOrders.Any(x => x.IsExportScale);
        var plannedBagCount = enrichedLines.Sum(x => x.IsBagged ? (x.CutOrder.BagCount ?? x.Line.PlannedBagCount ?? 0) : 0);
        var plannedWeightKg = enrichedLines.Sum(x =>
        {
            if (!x.IsBagged && x.Line.ActualAllocatedWeight.HasValue && x.Line.ActualAllocatedWeight.Value > 0m)
            {
                return x.Line.ActualAllocatedWeight.Value;
            }

            return x.CutOrder.PlannedWeight ?? x.Line.PlannedWeight ?? 0m;
        });
        var actualBagCount = enrichedLines.Sum(x => x.IsBagged ? CalculateReportBagCount(x.Line.ActualAllocatedWeight, x.Line.ActualAllocatedBagCount) : 0);
        var actualWeightKg = enrichedLines.Sum(x => x.Line.ActualAllocatedWeight ?? 0m);
        if (actualWeightKg <= 0m)
        {
            actualWeightKg = session.NetWeight ?? 0m;
        }

        if (enrichedLines.Count > 0 && enrichedLines.All(x => !x.IsBagged))
        {
            plannedWeightKg = actualWeightKg;
        }

        if (isExportScaleSession)
        {
            plannedBagCount = actualBagCount;
            plannedWeightKg = actualWeightKg;
        }

        var plannedTon = decimal.Round(plannedWeightKg / 1000m, 3, MidpointRounding.AwayFromZero);
        var actualTon = decimal.Round(actualWeightKg / 1000m, 3, MidpointRounding.AwayFromZero);
        var differenceTon = decimal.Round(actualTon - plannedTon, 3, MidpointRounding.AwayFromZero);

        decimal? standardKgPerBag = plannedBagCount > 0
            ? decimal.Round(plannedWeightKg / plannedBagCount, 2, MidpointRounding.AwayFromZero)
            : null;
        decimal? actualKgPerBag = actualBagCount > 0
            ? decimal.Round(actualWeightKg / actualBagCount, 2, MidpointRounding.AwayFromZero)
            : null;

        var notes = string.Join("; ",
            cutOrders.Select(x => x.Notes)
                .Concat(deliveries.Select(x => x.Notes))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));

        var deliveryNos = string.Join(", ",
            deliveries.Select(x => x.DeliveryNo)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        var customerCodes = string.Join(", ",
            cutOrders.Select(x => x.CustomerCode)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        var customerNames = string.Join(", ",
            cutOrders.Select(x => x.CustomerName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        var cutOrderCodes = string.Join(", ",
            cutOrders.Select(x => x.ErpCutOrderId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        var productDisplayName = string.Join(", ",
            cutOrders.Select(x => x.ProductName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        var status = enrichedLines.Count > 0 && enrichedLines.All(x => !x.IsBagged)
            ? "OK"
            : ResolveBaggedStatus(actualKgPerBag, standardKgPerBag, toleranceKgPerBag);

        return new ExportSummaryReportRow(
            reportedAt,
            weighTicket?.TicketNo,
            string.IsNullOrWhiteSpace(customerCodes) ? null : customerCodes,
            string.IsNullOrWhiteSpace(customerNames) ? null : customerNames,
            string.IsNullOrWhiteSpace(cutOrderCodes) ? null : cutOrderCodes,
            string.IsNullOrWhiteSpace(deliveryNos) ? null : deliveryNos,
            session.VehiclePlate,
            session.DriverName ?? weighTicket?.DriverName,
            plannedBagCount,
            plannedWeightKg,
            plannedTon,
            actualBagCount,
            actualWeightKg,
            actualTon,
            string.IsNullOrWhiteSpace(productDisplayName) ? "Không xác định" : productDisplayName,
            string.IsNullOrWhiteSpace(notes) ? null : notes,
            differenceTon,
            standardKgPerBag,
            actualKgPerBag,
            status);
    }

    private static int CalculateReportBagCount(decimal? actualWeightKg, int? persistedBagCount)
    {
        if (!actualWeightKg.HasValue || actualWeightKg.Value <= 0m)
        {
            return persistedBagCount ?? 0;
        }

        return (int)decimal.Floor(actualWeightKg.Value / BagWeightKg);
    }

    private static string? ResolveProductType(CutOrder cutOrder, IReadOnlyDictionary<string, Product> productLookup)
    {
        var normalized = ProductTypes.Normalize(cutOrder.ProductType);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        if (!string.IsNullOrWhiteSpace(cutOrder.ProductCode)
            && productLookup.TryGetValue(cutOrder.ProductCode.Trim(), out var product))
        {
            return ProductTypes.Normalize(product.ProductType);
        }

        return null;
    }

    private static string ResolveBaggedStatus(decimal? actualKgPerBag, decimal? standardKgPerBag, decimal toleranceKgPerBag)
    {
        if (!actualKgPerBag.HasValue || !standardKgPerBag.HasValue)
        {
            return string.Empty;
        }

        var delta = actualKgPerBag.Value - standardKgPerBag.Value;
        if (Math.Abs(delta) <= toleranceKgPerBag)
        {
            return "OK";
        }

        return delta > 0 ? "Vượt" : "Thiếu";
    }
}

public sealed class ExportSummaryReportExcelExporter : IExportSummaryReportExporter
{
    public Task ExportAsync(ExportSummaryReportDocument document, string outputPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("BaoCaoXuat");

        BuildHeader(sheet, document);
        var lastTableRow = BuildTableV2(sheet, document);
        BuildFooter(sheet, document, lastTableRow);
        ApplySheetLayout(sheet, lastTableRow + 5);

        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    private static void BuildHeader(IXLWorksheet sheet, ExportSummaryReportDocument document)
    {
        sheet.Range("B2:G3").Merge().Value = "XI MĂNG CẨM PHẢ\nPHÒNG CHIẾN LƯỢC KINH DOANH";
        var leftHeader = sheet.Range("B2:G3");
        leftHeader.Style.Alignment.WrapText = true;
        leftHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        leftHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        leftHeader.Style.Font.Bold = true;
        leftHeader.Style.Font.FontSize = 12;

        sheet.Range("N2:T3").Merge().Value = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc";
        var rightHeader = sheet.Range("N2:T3");
        rightHeader.Style.Alignment.WrapText = true;
        rightHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rightHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rightHeader.Style.Font.Bold = true;
        rightHeader.Style.Font.FontSize = 12;

        var title = $"BÁO CÁO XUẤT TỔNG HỢP TỪ {document.FromTime:HH:mm:ss dd/MM/yyyy} ĐẾN {document.ToTime:HH:mm:ss dd/MM/yyyy}";
        sheet.Range("B5:T5").Merge().Value = title;
        var titleRange = sheet.Range("B5:T5");
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;
    }

    private static int BuildTableV2(IXLWorksheet sheet, ExportSummaryReportDocument document)
    {
        const int topHeaderRow = 7;
        const int bottomHeaderRow = 8;
        const int dataStartRow = 9;
        const int lastColumn = 19;

        sheet.Cell(topHeaderRow, 2).Value = "NGÀY XUẤT";
        sheet.Cell(topHeaderRow, 3).Value = "GIỜ XUẤT";
        sheet.Cell(topHeaderRow, 4).Value = "MÃ CẮT LỆNH";
        sheet.Cell(topHeaderRow, 5).Value = "MÃ KH";
        sheet.Cell(topHeaderRow, 6).Value = "TÊN KH";
        sheet.Cell(topHeaderRow, 7).Value = "LOẠI HÀNG";
        sheet.Cell(topHeaderRow, 8).Value = "SỐ PC";
        sheet.Cell(topHeaderRow, 9).Value = "SỐ PGN";
        sheet.Cell(topHeaderRow, 10).Value = "SỐ PTVC";
        sheet.Cell(topHeaderRow, 11).Value = "TÊN TÀI XẾ";
        sheet.Cell(topHeaderRow, 12).Value = "ĐẶT HÀNG";
        sheet.Cell(topHeaderRow, 14).Value = "THỰC XUẤT";
        sheet.Cell(topHeaderRow, 16).Value = "GHI CHÚ";
        sheet.Cell(topHeaderRow, 17).Value = "CHÊNH LỆCH";
        sheet.Cell(topHeaderRow, 18).Value = "KG/BAO";
        sheet.Cell(topHeaderRow, 19).Value = "OK/VƯỢT/THIẾU";

        sheet.Range(topHeaderRow, 12, topHeaderRow, 13).Merge();
        sheet.Range(topHeaderRow, 14, topHeaderRow, 15).Merge();

        sheet.Cell(bottomHeaderRow, 12).Value = "BAO";
        sheet.Cell(bottomHeaderRow, 13).Value = "TẤN";
        sheet.Cell(bottomHeaderRow, 14).Value = "BAO";
        sheet.Cell(bottomHeaderRow, 15).Value = "TẤN";

        foreach (var singleColumn in new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 16, 17, 18, 19 })
        {
            sheet.Range(topHeaderRow, singleColumn, bottomHeaderRow, singleColumn).Merge();
        }

        var headerRange = sheet.Range(topHeaderRow, 2, bottomHeaderRow, lastColumn);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = dataStartRow;
        foreach (var item in document.Rows)
        {
            sheet.Cell(row, 2).Value = item.ExportedAt;
            sheet.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 3).Value = item.ExportedAt;
            sheet.Cell(row, 3).Style.DateFormat.Format = "HH:mm";
            sheet.Cell(row, 4).Value = item.CutOrderCode;
            sheet.Cell(row, 5).Value = item.CustomerCode;
            sheet.Cell(row, 6).Value = item.CustomerName;
            sheet.Cell(row, 7).Value = item.ProductDisplayName;
            sheet.Cell(row, 8).Value = item.WeighTicketNo;
            sheet.Cell(row, 9).Value = item.DeliveryNo;
            sheet.Cell(row, 10).Value = item.VehiclePlate;
            sheet.Cell(row, 11).Value = item.DriverName;
            sheet.Cell(row, 12).Value = item.PlannedBagCount;
            sheet.Cell(row, 13).Value = item.PlannedTon;
            sheet.Cell(row, 14).Value = item.ActualBagCount;
            sheet.Cell(row, 15).Value = item.ActualTon;
            sheet.Cell(row, 16).Value = item.Notes;
            sheet.Cell(row, 17).Value = item.DifferenceTon;
            sheet.Cell(row, 18).Value = item.ActualKgPerBag;
            sheet.Cell(row, 19).Value = item.Status;
            row++;
        }

        if (document.Rows.Count > 0)
        {
            sheet.Range(dataStartRow, 13, row - 1, 13).Style.NumberFormat.Format = "#,##0.000";
            sheet.Range(dataStartRow, 15, row - 1, 15).Style.NumberFormat.Format = "#,##0.000";
            sheet.Range(dataStartRow, 17, row - 1, 17).Style.NumberFormat.Format = "#,##0.000";
            sheet.Range(dataStartRow, 18, row - 1, 18).Style.NumberFormat.Format = "#,##0.00";
        }

        var totalRow = row;
        sheet.Range(totalRow, 2, totalRow, 11).Merge().Value = "TỔNG";
        var totalLabelRange = sheet.Range(totalRow, 2, totalRow, 11);
        totalLabelRange.Style.Font.Bold = true;
        totalLabelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        sheet.Cell(totalRow, 13).Value = decimal.Round(document.Rows.Sum(x => x.PlannedTon), 3, MidpointRounding.AwayFromZero);
        sheet.Cell(totalRow, 15).Value = decimal.Round(document.Rows.Sum(x => x.ActualTon), 3, MidpointRounding.AwayFromZero);
        sheet.Cell(totalRow, 17).Value = decimal.Round(document.Rows.Sum(x => x.DifferenceTon), 3, MidpointRounding.AwayFromZero);
        sheet.Range(totalRow, 13, totalRow, 13).Style.NumberFormat.Format = "#,##0.000";
        sheet.Range(totalRow, 15, totalRow, 15).Style.NumberFormat.Format = "#,##0.000";
        sheet.Range(totalRow, 17, totalRow, 17).Style.NumberFormat.Format = "#,##0.000";
        sheet.Range(totalRow, 18, totalRow, 18).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(totalRow, 12).Value = string.Empty;
        sheet.Cell(totalRow, 14).Value = string.Empty;

        var totalBaggedRows = document.Rows.Where(x => x.ActualBagCount > 0).ToList();
        if (totalBaggedRows.Count > 0)
        {
            var totalActualWeightKg = totalBaggedRows.Sum(x => x.ActualWeightKg);
            var totalActualBagCount = totalBaggedRows.Sum(x => x.ActualBagCount);
            if (totalActualBagCount > 0)
            {
                sheet.Cell(totalRow, 18).Value = decimal.Round(totalActualWeightKg / totalActualBagCount, 2, MidpointRounding.AwayFromZero);
            }
        }

        var bodyRange = sheet.Range(topHeaderRow, 2, totalRow, lastColumn);
        bodyRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        bodyRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        bodyRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        bodyRange.Style.Alignment.WrapText = true;

        ApplyReportAlignmentV2(sheet, document.Rows.Count, dataStartRow, totalRow);

        var totalRange = sheet.Range(totalRow, 2, totalRow, lastColumn);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");

        return totalRow;
    }

    private static void ApplyReportAlignmentV2(
        IXLWorksheet sheet,
        int rowCount,
        int dataStartRow,
        int totalRow)
    {
        if (rowCount > 0)
        {
            sheet.Range(dataStartRow, 2, totalRow - 1, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sheet.Range(dataStartRow, 2, totalRow - 1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 12, totalRow - 1, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 14, totalRow - 1, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 13, totalRow - 1, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 15, totalRow - 1, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 17, totalRow - 1, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 19, totalRow - 1, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        sheet.Range(totalRow, 12, totalRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(totalRow, 14, totalRow, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(totalRow, 13, totalRow, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(totalRow, 15, totalRow, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(totalRow, 17, totalRow, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(totalRow, 19, totalRow, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static int BuildTable(IXLWorksheet sheet, ExportSummaryReportDocument document)
    {
        const int topHeaderRow = 7;
        const int bottomHeaderRow = 8;
        const int dataStartRow = 9;

        sheet.Cell(topHeaderRow, 2).Value = "NGÀY XUẤT";
        sheet.Cell(topHeaderRow, 3).Value = "GIỜ XUẤT";
        sheet.Cell(topHeaderRow, 4).Value = "SỐ PC";
        sheet.Cell(topHeaderRow, 5).Value = "MÃ KH";
        sheet.Cell(topHeaderRow, 6).Value = "TÊN KH";
        sheet.Cell(topHeaderRow, 7).Value = "SỐ PGN";
        sheet.Cell(topHeaderRow, 8).Value = "SỐ PTVC";
        sheet.Cell(topHeaderRow, 9).Value = "TÊN TÀI XẾ";
        sheet.Cell(topHeaderRow, 10).Value = "ĐẶT HÀNG";
        sheet.Cell(topHeaderRow, 12).Value = "THỰC XUẤT";
        sheet.Cell(topHeaderRow, 14).Value = "LOẠI HÀNG";
        sheet.Cell(topHeaderRow, 15).Value = "GHI CHÚ";
        sheet.Cell(topHeaderRow, 16).Value = "CHÊNH LỆCH";
        sheet.Cell(topHeaderRow, 17).Value = "KG/BAO";
        sheet.Cell(topHeaderRow, 18).Value = "OK/VƯỢT/THIẾU";

        sheet.Range(topHeaderRow, 10, topHeaderRow, 11).Merge();
        sheet.Range(topHeaderRow, 12, topHeaderRow, 13).Merge();

        sheet.Cell(bottomHeaderRow, 10).Value = "BAO";
        sheet.Cell(bottomHeaderRow, 11).Value = "TẤN";
        sheet.Cell(bottomHeaderRow, 12).Value = "BAO";
        sheet.Cell(bottomHeaderRow, 13).Value = "TẤN";

        foreach (var singleColumn in new[] { 2, 3, 4, 5, 6, 7, 8, 9, 14, 15, 16, 17, 18 })
        {
            sheet.Range(topHeaderRow, singleColumn, bottomHeaderRow, singleColumn).Merge();
        }

        var headerRange = sheet.Range(topHeaderRow, 2, bottomHeaderRow, 18);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = dataStartRow;
        foreach (var item in document.Rows)
        {
            sheet.Cell(row, 2).Value = item.ExportedAt;
            sheet.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 3).Value = item.ExportedAt;
            sheet.Cell(row, 3).Style.DateFormat.Format = "HH:mm";
            sheet.Cell(row, 4).Value = item.WeighTicketNo;
            sheet.Cell(row, 5).Value = item.CustomerCode;
            sheet.Cell(row, 6).Value = item.CustomerName;
            sheet.Cell(row, 7).Value = item.DeliveryNo;
            sheet.Cell(row, 8).Value = item.VehiclePlate;
            sheet.Cell(row, 9).Value = item.DriverName;
            sheet.Cell(row, 10).Value = item.PlannedBagCount;
            sheet.Cell(row, 11).Value = item.PlannedTon;
            sheet.Cell(row, 12).Value = item.ActualBagCount;
            sheet.Cell(row, 13).Value = item.ActualTon;
            sheet.Cell(row, 14).Value = item.ProductDisplayName;
            sheet.Cell(row, 15).Value = item.Notes;
            sheet.Cell(row, 16).Value = item.DifferenceTon;
            sheet.Cell(row, 17).Value = item.ActualKgPerBag;
            sheet.Cell(row, 18).Value = item.Status;
            row++;
        }

        if (document.Rows.Count > 0)
        {
            sheet.Range(dataStartRow, 11, row - 1, 11).Style.NumberFormat.Format = "#,##0.000";
            sheet.Range(dataStartRow, 13, row - 1, 13).Style.NumberFormat.Format = "#,##0.000";
            sheet.Range(dataStartRow, 16, row - 1, 16).Style.NumberFormat.Format = "#,##0.000";
            sheet.Range(dataStartRow, 17, row - 1, 17).Style.NumberFormat.Format = "#,##0.00";
        }

        var totalRow = row;
        sheet.Range(totalRow, 2, totalRow, 9).Merge().Value = "TỔNG";
        var totalLabelRange = sheet.Range(totalRow, 2, totalRow, 9);
        totalLabelRange.Style.Font.Bold = true;
        totalLabelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        sheet.Cell(totalRow, 11).Value = decimal.Round(document.Rows.Sum(x => x.PlannedTon), 3, MidpointRounding.AwayFromZero);
        sheet.Cell(totalRow, 13).Value = decimal.Round(document.Rows.Sum(x => x.ActualTon), 3, MidpointRounding.AwayFromZero);
        sheet.Cell(totalRow, 16).Value = decimal.Round(document.Rows.Sum(x => x.DifferenceTon), 3, MidpointRounding.AwayFromZero);
        sheet.Range(totalRow, 11, totalRow, 11).Style.NumberFormat.Format = "#,##0.000";
        sheet.Range(totalRow, 13, totalRow, 13).Style.NumberFormat.Format = "#,##0.000";
        sheet.Range(totalRow, 16, totalRow, 16).Style.NumberFormat.Format = "#,##0.000";
        sheet.Range(totalRow, 17, totalRow, 17).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(totalRow, 10).Value = string.Empty;
        sheet.Cell(totalRow, 12).Value = string.Empty;

        var totalBaggedRows = document.Rows.Where(x => x.ActualBagCount > 0).ToList();
        if (totalBaggedRows.Count > 0)
        {
            var totalActualWeightKg = totalBaggedRows.Sum(x => x.ActualWeightKg);
            var totalActualBagCount = totalBaggedRows.Sum(x => x.ActualBagCount);
            if (totalActualBagCount > 0)
            {
                sheet.Cell(totalRow, 17).Value = decimal.Round(totalActualWeightKg / totalActualBagCount, 2, MidpointRounding.AwayFromZero);
            }
        }

        var bodyRange = sheet.Range(topHeaderRow, 2, totalRow, 18);
        bodyRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        bodyRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        bodyRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        if (document.Rows.Count > 0)
        {
            var dataRange = sheet.Range(dataStartRow, 2, totalRow - 1, 18);
            dataRange.Style.Alignment.WrapText = true;
            sheet.Range(dataStartRow, 10, totalRow, 17).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 18, totalRow, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        else
        {
            sheet.Range(totalRow, 10, totalRow, 17).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(totalRow, 18, totalRow, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var totalRange = sheet.Range(totalRow, 2, totalRow, 18);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");

        InsertCutOrderCodeColumn(sheet, document, dataStartRow, totalRow);
        ApplyReportAlignment(sheet, document.Rows.Count, dataStartRow, totalRow);

        return totalRow;
    }

    private static void ApplyReportAlignment(
        IXLWorksheet sheet,
        int rowCount,
        int dataStartRow,
        int totalRow)
    {
        if (rowCount > 0)
        {
            sheet.Range(dataStartRow, 2, totalRow - 1, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sheet.Range(dataStartRow, 2, totalRow - 1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 11, totalRow - 1, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 13, totalRow - 1, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(dataStartRow, 12, totalRow - 1, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 14, totalRow - 1, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 17, totalRow - 1, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 19, totalRow - 1, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        sheet.Range(totalRow, 11, totalRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(totalRow, 13, totalRow, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(totalRow, 12, totalRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(totalRow, 14, totalRow, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(totalRow, 17, totalRow, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(totalRow, 19, totalRow, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void InsertCutOrderCodeColumn(
        IXLWorksheet sheet,
        ExportSummaryReportDocument document,
        int dataStartRow,
        int totalRow)
    {
        const int cutOrderCodeColumn = 7;
        sheet.Column(cutOrderCodeColumn).InsertColumnsBefore(1);

        sheet.Cell(7, cutOrderCodeColumn).Value = "MÃ CẮT LỆNH";
        sheet.Range(7, cutOrderCodeColumn, 8, cutOrderCodeColumn).Merge();
        sheet.Range(7, cutOrderCodeColumn, 8, cutOrderCodeColumn).Style.Font.Bold = true;
        sheet.Range(7, cutOrderCodeColumn, 8, cutOrderCodeColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(7, cutOrderCodeColumn, 8, cutOrderCodeColumn).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(7, cutOrderCodeColumn, 8, cutOrderCodeColumn).Style.Alignment.WrapText = true;
        sheet.Range(7, cutOrderCodeColumn, totalRow, cutOrderCodeColumn).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(7, cutOrderCodeColumn, totalRow, cutOrderCodeColumn).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = dataStartRow;
        foreach (var item in document.Rows)
        {
            sheet.Cell(row, cutOrderCodeColumn).Value = item.CutOrderCode;
            row++;
        }

        sheet.Column(cutOrderCodeColumn).Width = 16;
    }

    private static void BuildFooter(IXLWorksheet sheet, ExportSummaryReportDocument document, int lastTableRow)
    {
        var footerTitleRow = lastTableRow + 3;
        var footerNameRow = lastTableRow + 6;

        sheet.Range(footerTitleRow, 15, footerTitleRow, 18).Merge().Value = "Người lập";
        sheet.Range(footerTitleRow, 16, footerTitleRow, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(footerTitleRow, 16, footerTitleRow, 19).Style.Font.Bold = true;

        sheet.Range(footerNameRow, 16, footerNameRow, 19).Merge().Value = document.PreparedByDisplayName;
        sheet.Range(footerNameRow, 16, footerNameRow, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void ApplySheetLayout(IXLWorksheet sheet, int lastRelevantRow)
    {
        sheet.Column(1).Width = 2;
        sheet.Column(20).Width = 2;
        sheet.Column(21).Width = 2;

        sheet.SheetView.FreezeRows(8);
        sheet.Range(7, 2, Math.Max(8, lastRelevantRow), 19).Style.Font.FontName = "Times New Roman";
        sheet.Range(2, 2, Math.Max(8, lastRelevantRow), 19).Style.Font.FontName = "Times New Roman";
        sheet.Columns(2, 19).AdjustToContents(2, lastRelevantRow);
        ApplyColumnWidthLimits(sheet, new Dictionary<int, (double Min, double Max)>
        {
            [2] = (12, 14),
            [3] = (9, 11),
            [4] = (14, 20),
            [5] = (9, 12),
            [6] = (22, 40),
            [7] = (24, 44),
            [8] = (10, 14),
            [9] = (11, 16),
            [10] = (12, 16),
            [11] = (18, 30),
            [12] = (9, 12),
            [13] = (9, 12),
            [14] = (9, 12),
            [15] = (9, 12),
            [16] = (16, 34),
            [17] = (10, 13),
            [18] = (9, 12),
            [19] = (12, 16)
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
