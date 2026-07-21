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

        var tripRows = await (
            from line in _dbContext.WeighingSessionLines.AsNoTracking()
            join session in _dbContext.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            join vessel in _dbContext.CutOrders.AsNoTracking()
                on line.CutOrderId equals vessel.Id
            where line.StationCode == stationCode
                && session.StationCode == stationCode
                && vessel.StationCode == stationCode
                && !line.IsDeleted
                && !session.IsDeleted
                && !session.IsCancelled
                && !session.IsNoLoad
                && !vessel.IsDeleted
                && !vessel.IsCancelled
                && vessel.TransactionType == TransactionType.INBOUND
                && vessel.TransportMethod == TransportMethod.WATERWAY
                && session.TransactionType == TransactionType.INBOUND
                && session.SessionStatus == WeighingSessionStatus.COMPLETED
                && line.LineStatus == WeighingSessionLineStatus.ALLOCATED
                && session.InternalVehicleNo != null
                && session.InternalVehicleNo != string.Empty
                && session.Weight2Time.HasValue
                && (filter.VesselCutOrderId.HasValue
                    || (session.Weight2Time.Value >= filter.FromTime
                        && session.Weight2Time.Value <= filter.ToTime))
                && (filter.VesselCutOrderId.HasValue
                    || string.IsNullOrWhiteSpace(filter.ProductCode)
                    || vessel.ProductCode == filter.ProductCode)
                && (filter.VesselCutOrderId.HasValue
                    || string.IsNullOrWhiteSpace(filter.CarrierCode)
                    || vessel.CustomerCode == filter.CarrierCode
                    || ((vessel.CustomerCode == null || vessel.CustomerCode == string.Empty) && vessel.CustomerName == filter.CarrierCode))
                && (!filter.VesselCutOrderId.HasValue || line.CutOrderId == filter.VesselCutOrderId.Value)
            orderby session.Weight2Time, session.SessionNo
            select new ClayInboundReportTripRow(
                session.SessionNo,
                session.InternalVehicleNo ?? session.VehiclePlate ?? string.Empty,
                line.CustomerName ?? session.CustomerName ?? vessel.CustomerName,
                line.ProductName ?? session.ProductName ?? vessel.ProductName,
                session.Weight2Time,
                session.Weight1,
                session.Weight2,
                session.StandardTareWeightSnapshot,
                line.ActualAllocatedWeight ?? session.NetWeight,
                line.IsReturnedBrokenTrip,
                vessel.Id,
                vessel.VehiclePlate,
                vessel.CustomerName,
                vessel.ProductName))
            .ToListAsync(ct);

        var rows = MergeReturnedBrokenTrips(BuildRows(tripRows, filter));
        var totalNetWeightTon = decimal.Round(rows.Sum(x => x.NetWeightTon), 3, MidpointRounding.AwayFromZero);
        var returnedBrokenWeightTon = decimal.Round(rows.Sum(x => x.ReturnedBrokenWeightTon), 3, MidpointRounding.AwayFromZero);
        var actualInboundWeightTon = decimal.Round(totalNetWeightTon - returnedBrokenWeightTon, 3, MidpointRounding.AwayFromZero);
        var selectedVessel = await ResolveSelectedVesselAsync(filter, tripRows, stationCode, ct);

        return new ClayInboundReportDocument(
            filter.FromTime,
            filter.ToTime,
            filter.ProductCode,
            filter.CarrierCode,
            filter.VesselCutOrderId,
            selectedVessel,
            stationName,
            preparedByDisplayName,
            null,
            rows,
            totalNetWeightTon,
            returnedBrokenWeightTon,
            actualInboundWeightTon);
    }

    public async Task<IReadOnlyList<ReportLookupOptionDto>> GetProductOptionsAsync(CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var products = await _dbContext.CutOrders.AsNoTracking()
            .Where(x => x.StationCode == stationCode
                && !x.IsDeleted
                && !x.IsCancelled
                && x.TransactionType == TransactionType.INBOUND
                && x.TransportMethod == TransportMethod.WATERWAY
                && x.CutOrderSource == CutOrderSource.MANUAL
                && x.ProductCode != null
                && x.ProductCode != string.Empty)
            .GroupBy(x => new { x.ProductCode, x.ProductName })
            .OrderBy(x => x.Key.ProductCode)
            .Select(x => new ReportLookupOptionDto(
                x.Key.ProductCode!,
                x.Key.ProductName == null || x.Key.ProductName == string.Empty
                    ? x.Key.ProductCode!
                    : x.Key.ProductCode + " - " + x.Key.ProductName))
            .ToListAsync(ct);

        return products;
    }

    public async Task<IReadOnlyList<ReportLookupOptionDto>> GetCarrierOptionsAsync(CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var carriers = await _dbContext.CutOrders.AsNoTracking()
            .Where(x => x.StationCode == stationCode
                && !x.IsDeleted
                && !x.IsCancelled
                && x.TransactionType == TransactionType.INBOUND
                && x.TransportMethod == TransportMethod.WATERWAY
                && x.CutOrderSource == CutOrderSource.MANUAL
                && ((x.CustomerCode != null && x.CustomerCode != string.Empty)
                    || (x.CustomerName != null && x.CustomerName != string.Empty)))
            .Select(x => new { x.CustomerCode, x.CustomerName })
            .ToListAsync(ct);

        return carriers
            .Select(x =>
            {
                var code = string.IsNullOrWhiteSpace(x.CustomerCode)
                    ? x.CustomerName?.Trim() ?? string.Empty
                    : x.CustomerCode.Trim();
                var name = x.CustomerName?.Trim();
                var displayName = string.IsNullOrWhiteSpace(name) || string.Equals(code, name, StringComparison.OrdinalIgnoreCase)
                    ? code
                    : $"{code} - {name}";
                return new ReportLookupOptionDto(code, displayName);
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    public async Task<IReadOnlyList<ReportLookupOptionDto>> GetVesselOptionsAsync(
        ClayInboundVesselLookupFilter filter,
        CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var vessels = await _dbContext.CutOrders.AsNoTracking()
            .Where(vessel => vessel.StationCode == stationCode
                && !vessel.IsDeleted
                && !vessel.IsCancelled
                && vessel.TransactionType == TransactionType.INBOUND
                && vessel.TransportMethod == TransportMethod.WATERWAY
                && vessel.CutOrderSource == CutOrderSource.MANUAL
                && (string.IsNullOrWhiteSpace(filter.ProductCode) || vessel.ProductCode == filter.ProductCode)
                && (string.IsNullOrWhiteSpace(filter.CarrierCode)
                    || vessel.CustomerCode == filter.CarrierCode
                    || ((vessel.CustomerCode == null || vessel.CustomerCode == string.Empty) && vessel.CustomerName == filter.CarrierCode))
                && (vessel.CreatedAt >= filter.FromTime
                    && vessel.CreatedAt <= filter.ToTime
                    || vessel.UpdatedAt.HasValue
                        && vessel.UpdatedAt.Value >= filter.FromTime
                        && vessel.UpdatedAt.Value <= filter.ToTime
                    || _dbContext.WeighingSessionLines.AsNoTracking()
                        .Join(
                            _dbContext.WeighingSessions.AsNoTracking(),
                            line => line.WeighingSessionId,
                            session => session.Id,
                            (line, session) => new { line, session })
                        .Any(x => x.line.CutOrderId == vessel.Id
                            && x.line.StationCode == stationCode
                            && x.session.StationCode == stationCode
                            && !x.line.IsDeleted
                            && !x.session.IsDeleted
                            && !x.session.IsCancelled
                            && !x.session.IsNoLoad
                            && x.session.TransactionType == TransactionType.INBOUND
                            && x.session.SessionStatus == WeighingSessionStatus.COMPLETED
                            && x.line.LineStatus == WeighingSessionLineStatus.ALLOCATED
                            && x.session.Weight2Time.HasValue
                            && x.session.Weight2Time.Value >= filter.FromTime
                            && x.session.Weight2Time.Value <= filter.ToTime)))
            .OrderByDescending(vessel => vessel.UpdatedAt ?? vessel.CreatedAt)
            .ThenBy(vessel => vessel.VehiclePlate)
            .Select(vessel => new
            {
                vessel.Id,
                vessel.VehiclePlate,
                vessel.CustomerName,
                vessel.ProductName
            })
            .Take(300)
            .ToListAsync(ct);

        var vesselIds = vessels.Select(x => x.Id).ToList();
        var vesselTripDateRanges = await (
            from line in _dbContext.WeighingSessionLines.AsNoTracking()
            join session in _dbContext.WeighingSessions.AsNoTracking()
                on line.WeighingSessionId equals session.Id
            where vesselIds.Contains(line.CutOrderId)
                && line.StationCode == stationCode
                && session.StationCode == stationCode
                && !line.IsDeleted
                && !session.IsDeleted
                && !session.IsCancelled
                && !session.IsNoLoad
                && session.TransactionType == TransactionType.INBOUND
                && session.SessionStatus == WeighingSessionStatus.COMPLETED
                && line.LineStatus == WeighingSessionLineStatus.ALLOCATED
                && session.Weight2Time.HasValue
            group session by line.CutOrderId
            into vesselTrips
            select new
            {
                VesselId = vesselTrips.Key,
                FirstTripAt = vesselTrips.Min(x => x.Weight2Time),
                LastTripAt = vesselTrips.Max(x => x.Weight2Time)
            })
            .ToDictionaryAsync(x => x.VesselId, x => (x.FirstTripAt, x.LastTripAt), ct);

        return vessels
            .Select(x =>
            {
                var parts = new[] { x.VehiclePlate, x.CustomerName, x.ProductName }
                    .Where(value => !string.IsNullOrWhiteSpace(value));
                var displayName = string.Join(" - ", parts);
                if (vesselTripDateRanges.TryGetValue(x.Id, out var dateRange)
                    && dateRange.FirstTripAt.HasValue
                    && dateRange.LastTripAt.HasValue)
                {
                    displayName = $"{displayName} ({dateRange.FirstTripAt.Value:dd/MM/yyyy} - {dateRange.LastTripAt.Value:dd/MM/yyyy})";
                }

                return new ReportLookupOptionDto(x.Id.ToString("D"), displayName);
            })
            .ToList();
    }

    private static List<ClayInboundReportRow> BuildRows(
        IReadOnlyList<ClayInboundReportTripRow> sessions,
        ClayInboundReportFilter filter)
    {
        var rows = new List<ClayInboundReportRow>();
        foreach (var session in sessions)
        {
            var netWeightTon = ToTon(session.NetWeight);
            var returnedBrokenWeightTon = session.IsReturnedBrokenTrip ? netWeightTon : 0m;
            var actualInboundWeightTon = session.IsReturnedBrokenTrip ? 0m : netWeightTon;

            rows.Add(new ClayInboundReportRow(
                rows.Count + 1,
                NormalizeSessionNo(BusinessNumberFormatter.ToDisplay(session.SessionNo)),
                session.InternalVehicleNo,
                session.CustomerName,
                session.ProductName,
                session.Weight2Time,
                ToTon(session.Weight1),
                ResolveTareWeightTon(session),
                netWeightTon,
                returnedBrokenWeightTon,
                actualInboundWeightTon,
                session.IsReturnedBrokenTrip));
        }

        return rows;
    }

    private static List<ClayInboundReportRow> MergeReturnedBrokenTrips(IReadOnlyList<ClayInboundReportRow> rawRows)
    {
        var mergedRows = new List<ClayInboundReportRow>();

        foreach (var row in rawRows)
        {
            if (!row.IsReturnedBrokenTrip)
            {
                mergedRows.Add(row with { RowNo = mergedRows.Count + 1 });
                continue;
            }

            var matchedIndex = -1;
            for (var i = mergedRows.Count - 1; i >= 0; i--)
            {
                if (!mergedRows[i].IsReturnedBrokenTrip
                    && string.Equals(mergedRows[i].InternalVehicleNo, row.InternalVehicleNo, StringComparison.OrdinalIgnoreCase))
                {
                    matchedIndex = i;
                    break;
                }
            }

            if (matchedIndex < 0)
            {
                mergedRows.Add(row with { RowNo = mergedRows.Count + 1 });
                continue;
            }

            var targetRow = mergedRows[matchedIndex];
            var remainingReturnableWeightTon = Math.Max(
                0m,
                targetRow.NetWeightTon - targetRow.ReturnedBrokenWeightTon);
            var recognizedReturnedWeightTon = Math.Min(
                row.ReturnedBrokenWeightTon,
                remainingReturnableWeightTon);
            var returnedBrokenWeightTon = decimal.Round(
                targetRow.ReturnedBrokenWeightTon + recognizedReturnedWeightTon,
                3,
                MidpointRounding.AwayFromZero);

            mergedRows[matchedIndex] = targetRow with
            {
                ReturnedBrokenWeightTon = returnedBrokenWeightTon,
                ActualInboundWeightTon = decimal.Round(
                    targetRow.NetWeightTon - returnedBrokenWeightTon,
                    3,
                    MidpointRounding.AwayFromZero)
            };
        }

        for (var i = 0; i < mergedRows.Count; i++)
        {
            mergedRows[i] = mergedRows[i] with { RowNo = i + 1 };
        }

        return mergedRows;
    }

    private static decimal ResolveTareWeightTon(ClayInboundReportTripRow session)
    {
        if (session.StandardTareWeightSnapshot.HasValue)
        {
            return ToTon(session.StandardTareWeightSnapshot.Value);
        }

        return ToTon(session.Weight2);
    }

    private async Task<string?> ResolveSelectedVesselAsync(
        ClayInboundReportFilter filter,
        IReadOnlyList<ClayInboundReportTripRow> tripRows,
        string stationCode,
        CancellationToken ct)
    {
        if (!filter.VesselCutOrderId.HasValue)
        {
            return null;
        }

        var vessel = tripRows.FirstOrDefault(x => x.VesselCutOrderId == filter.VesselCutOrderId.Value);
        if (vessel == null)
        {
            var vesselFromDb = await _dbContext.CutOrders.AsNoTracking()
                .Where(x => x.Id == filter.VesselCutOrderId.Value
                    && x.StationCode == stationCode
                    && !x.IsDeleted)
                .Select(x => new
                {
                    x.VehiclePlate,
                    x.CustomerName,
                    x.ProductName
                })
                .FirstOrDefaultAsync(ct);

            if (vesselFromDb == null)
            {
                return null;
            }

            var dbParts = new[] { vesselFromDb.VehiclePlate, vesselFromDb.CustomerName, vesselFromDb.ProductName }
                .Where(value => !string.IsNullOrWhiteSpace(value));
            return string.Join(" - ", dbParts);
        }

        var parts = new[] { vessel.VesselName, vessel.VesselCustomerName, vessel.VesselProductName }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join(" - ", parts);
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

    private sealed record ClayInboundReportTripRow(
        string SessionNo,
        string InternalVehicleNo,
        string? CustomerName,
        string? ProductName,
        DateTime? Weight2Time,
        decimal? Weight1,
        decimal? Weight2,
        decimal? StandardTareWeightSnapshot,
        decimal? NetWeight,
        bool IsReturnedBrokenTrip,
        Guid VesselCutOrderId,
        string VesselName,
        string? VesselCustomerName,
        string? VesselProductName);
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

        sheet.Range("B2:D2").Merge().Value = "Địa chỉ: Km6, Quốc lộ 18A, Quang Hanh, Quảng Ninh";
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
        var titleText = sheet.Cell("G1").Value;
        sheet.Range("G1:H2").Unmerge();
        sheet.Range("G1:J2").Merge().Value = titleText;
        var titleRange = sheet.Range("G1:J2");
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontName = "Times New Roman";
        titleRange.Style.Font.FontSize = 16;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

        sheet.Range("G3:H3").Merge().Value = BuildTimeRangeText(document.FromTime, document.ToTime);
        var timeRangeText = sheet.Cell("G3").Value;
        sheet.Range("G3:H3").Unmerge();
        sheet.Range("G3:J3").Merge().Value = timeRangeText;
        var timeRange = sheet.Range("G3:J3");
        timeRange.Style.Font.FontName = "Times New Roman";
        timeRange.Style.Font.FontSize = 11;
        timeRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        timeRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

        if (!string.IsNullOrWhiteSpace(document.VesselDisplayName))
        {
            sheet.Range("A4:K4").Merge().Value = $"Chuyến tàu: {document.VesselDisplayName}";
            var vesselRange = sheet.Range("A4:K4");
            vesselRange.Style.Font.FontName = "Times New Roman";
            vesselRange.Style.Font.FontSize = 11;
            vesselRange.Style.Font.Bold = true;
            vesselRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            vesselRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
    }

    private static int BuildTable(IXLWorksheet sheet, ClayInboundReportDocument document)
    {
        var headerRow = string.IsNullOrWhiteSpace(document.VesselDisplayName) ? 5 : 6;
        var dataStartRow = headerRow + 1;
        const int columnCount = 11;

        var headers = new[]
        {
            "STT",
            "Số phiếu",
            "Số xe",
            "Ngày cân",
            "Tổng (tấn)",
            "Bì (tấn)",
            "Hàng (tấn)",
            "Hoàn (tấn)",
            "Thực nhập (tấn)",
            "Khách hàng",
            "Hàng hóa"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(headerRow, i + 1).Value = headers[i];
        }

        var headerRange = sheet.Range(headerRow, 1, headerRow, columnCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontName = "Times New Roman";
        headerRange.Style.Font.FontSize = 11;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Cell(headerRow, 8).Style.Font.FontColor = XLColor.Red;

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
            if (item.ReturnedBrokenWeightTon > 0)
            {
                sheet.Cell(row, 8).Value = item.ReturnedBrokenWeightTon;
                sheet.Cell(row, 8).Style.Font.FontColor = XLColor.Red;
            }
            else
            {
                sheet.Cell(row, 8).Value = string.Empty;
            }

            sheet.Cell(row, 9).Value = item.ActualInboundWeightTon;
            sheet.Cell(row, 10).Value = item.CustomerName;
            sheet.Cell(row, 11).Value = item.ProductName;
            row++;
        }

        if (document.Rows.Count > 0)
        {
            sheet.Range(dataStartRow, 5, row - 1, 9).Style.NumberFormat.Format = "#,##0.000";
        }

        var totalRow = row;
        sheet.Range(totalRow, 1, totalRow, 4).Merge().Value = "Cộng tổng:";
        sheet.Range(totalRow, 1, totalRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(totalRow, 1, totalRow, 4).Style.Font.Bold = true;
        sheet.Cell(totalRow, 7).Value = document.TotalNetWeightTon;
        sheet.Cell(totalRow, 8).Value = document.ReturnedBrokenWeightTon;
        sheet.Cell(totalRow, 9).Value = document.ActualInboundWeightTon;
        sheet.Range(totalRow, 7, totalRow, 9).Style.NumberFormat.Format = "#,##0.000";
        sheet.Range(totalRow, 7, totalRow, 9).Style.Font.Bold = true;
        sheet.Cell(totalRow, 8).Style.Font.FontColor = XLColor.Red;

        var tableRange = sheet.Range(headerRow, 1, totalRow, columnCount);
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
            sheet.Range(dataStartRow, 5, totalRow - 1, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(dataStartRow, 10, totalRow - 1, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
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

        sheet.Range(signatureTitleRow, 9, signatureTitleRow, 11).Merge().Value = "ĐẠI DIỆN PHÂN XƯỞNG KHAI THÁC";
        sheet.Range(signatureTitleRow, 9, signatureTitleRow, 11).Style.Font.Bold = true;
        sheet.Range(signatureTitleRow, 9, signatureTitleRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(signatureTitleRow, 9, signatureTitleRow, 11).Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

        sheet.Range(signatureNameRow, 9, signatureNameRow, 11).Merge().Value = document.PreparedByDisplayName;
        sheet.Range(signatureNameRow, 9, signatureNameRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var footerRange = sheet.Range(footerRow, 1, footerRow, 11);
        footerRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        footerRange.Style.Font.FontName = "Times New Roman";
        footerRange.Style.Font.FontSize = 11;

        sheet.Cell(footerRow, 1).Value = document.StationName;
        sheet.Cell(footerRow, 1).Style.Font.Bold = true;
        sheet.Cell(footerRow, 5).Value = $"Thời gian in: {DateTime.Now:dd/MM/yyyy HH:mm}";
        sheet.Cell(footerRow, 5).Style.Font.Italic = true;
        sheet.Cell(footerRow, 11).Value = "Trang: 1/1";
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
        sheet.Column(8).Width = 12;
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
            return $"Ngày: {fromTime:dd/MM/yyyy}";
        }

        return $"Từ ngày {fromTime:dd/MM/yyyy} đến ngày {toTime:dd/MM/yyyy}";
    }
}
