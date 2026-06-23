using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ClosedXML.Excel;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.Infrastructure.Repositories;
using StationApp.Infrastructure.Services;
using Xunit;

namespace StationApp.IntegrationTests;

public sealed class ExportScaleSummaryReportServiceTests : IDisposable
{
    private readonly DbContextOptions<StationDbContext> _dbOptions;

    public ExportScaleSummaryReportServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<StationDbContext>()
            .UseSqlServer(
                "Server=.;Database=StationApp_ExportScaleReportTests;Trusted_Connection=True;TrustServerCertificate=True;",
                sql => sql.UseCompatibilityLevel(120))
            .Options;

        using var db = new StationDbContext(_dbOptions);
        db.Database.EnsureDeleted();
        StationDatabaseInitializer.InitializeAsync(db, null, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task BuildExportScaleReportAsync_MergesReturnedTripIntoPreviousExportTripWithSameVehiclePlate()
    {
        var cutOrderId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 12, 12, 0, 0, 0);

        await using (var db = new StationDbContext(_dbOptions))
        {
            await SetConfigValueAsync(db, AppConfigKeys.StationCode, "QN01");
            await SetConfigValueAsync(db, AppConfigKeys.ToleranceKgPerBag, "1.75");

            db.CutOrders.Add(new CutOrder
            {
                Id = cutOrderId,
                StationCode = "QN01",
                ErpCutOrderId = "CLXK-0001",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.IN_SESSION,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "51C-12345",
                CustomerName = "OMANCO",
                ProductName = "Xi măng CEM II",
                ProductType = ProductTypes.Bagged,
                PlannedWeight = 122400m,
                TareWeightKg = 1m,
                BagWeightKg = 50m,
                IsExportScale = true,
                ProcessingStage = ProcessingStage.WEIGHING,
                CreatedAt = baseDate,
                CreatedBy = "TEST"
            });

            var sessionA = CreateSession("QN01", "LC26121201", "51C-11111", baseDate.AddHours(10), 51000m);
            var sessionB = CreateSession("QN01", "LC26121202", "51C-22222", baseDate.AddHours(16), 51000m);
            var sessionC = CreateSession("QN01", "LC26121203", "51C-33333", baseDate.AddHours(23), 10200m);
            var sessionReturned = CreateSession("QN01", "LC26121301", "51C-33333", baseDate.AddDays(1).AddHours(2), 10200m);
            db.WeighingSessions.AddRange(sessionA, sessionB, sessionC, sessionReturned);
            var returnedLine = CreateLine("QN01", sessionReturned.Id, cutOrderId, 1, 10200m, 200, true, baseDate.AddDays(1).AddHours(2));
            returnedLine.Note = "Hàng rách vỡ hoàn";

            db.WeighingSessionLines.AddRange(
                CreateLine("QN01", sessionA.Id, cutOrderId, 1, 51000m, 1000, false, baseDate.AddHours(10)),
                CreateLine("QN01", sessionB.Id, cutOrderId, 1, 51000m, 1000, false, baseDate.AddHours(16)),
                CreateLine("QN01", sessionC.Id, cutOrderId, 1, 10200m, 200, false, baseDate.AddHours(23)),
                returnedLine);

            await db.SaveChangesAsync();
        }

        await using (var db = new StationDbContext(_dbOptions))
        {
            var repo = new AppConfigRepository(db, new SystemClock());
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var service = new ExportSummaryReportService(db, repo, configuration);

            var document = await service.BuildExportScaleReportAsync(cutOrderId, null, "Tester", CancellationToken.None);

            Assert.Equal("CLXK-0001", document.CutOrderCode);
            Assert.Equal(122.4m, document.PlannedWeightTon);
            Assert.Equal(2448, document.PlannedBagCount);
            Assert.Equal(1m, document.TareWeightKg);
            Assert.Equal(50m, document.NetCementWeightKg);
            Assert.Equal(51m, document.GrossWeightKg);
            Assert.Equal(new DateTime(2026, 12, 13), document.TargetDateForShiftReport);
            Assert.Equal(3, document.Rows.Count);

            Assert.Collection(document.Rows,
                row =>
                {
                    Assert.Equal("A", row.Shift);
                    Assert.Equal(new DateTime(2026, 12, 12), row.ExportDate);
                    Assert.Equal(51m, row.NetWeightTon);
                    Assert.Equal(1000, row.BagCount);
                    Assert.Equal(50m, row.ActualExportTon);
                    Assert.False(row.IsReturnedBrokenTrip);
                },
                row =>
                {
                    Assert.Equal("B", row.Shift);
                    Assert.Equal(new DateTime(2026, 12, 12), row.ExportDate);
                },
                row =>
                {
                    Assert.Equal("C", row.Shift);
                    Assert.Equal(new DateTime(2026, 12, 12), row.ExportDate);
                    Assert.False(row.IsReturnedBrokenTrip);
                    Assert.Equal(200, row.ReturnedBrokenBagCount);
                    Assert.Equal(10.200m, row.ReturnedBrokenWeightTon);
                    Assert.Equal(0m, row.ActualExportTon);
                    Assert.Equal(0, row.ActualExportBagCount);
                    Assert.Contains("Hàng rách vỡ hoàn", row.Notes);
                });
        }
    }

    [Fact]
    public async Task GetExportScaleCutOrderOptionsAsync_IncludesCutOrdersByExportFlags()
    {
        var historicalCutOrderId = Guid.NewGuid();
        var activeTemporaryCutOrderId = Guid.NewGuid();
        var temporaryOnlyCutOrderId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 6, 22, 0, 0, 0);

        await using (var db = new StationDbContext(_dbOptions))
        {
            await SetConfigValueAsync(db, AppConfigKeys.StationCode, "QN01");

            db.CutOrders.AddRange(
                new CutOrder
                {
                    Id = historicalCutOrderId,
                    StationCode = "QN01",
                    ErpCutOrderId = "LCXK-HIS-01",
                    CutOrderSource = CutOrderSource.ERP,
                    CutOrderStatus = CutOrderStatus.IN_SESSION,
                    TransactionType = TransactionType.OUTBOUND,
                    VehiclePlate = "14C-11111",
                    CustomerName = "KH lịch sử",
                    ProductName = "XM Bao",
                    ProductType = ProductTypes.Bagged,
                    PlannedWeight = 51000m,
                    TareWeightKg = 1m,
                    BagWeightKg = 50m,
                    IsExportScale = true,
                    ProcessingStage = ProcessingStage.OUT_YARD,
                    CreatedAt = baseDate,
                    CreatedBy = "TEST"
                },
                new CutOrder
                {
                    Id = activeTemporaryCutOrderId,
                    StationCode = "QN01",
                    ErpCutOrderId = "TMP-SRC-01",
                    TemporaryExportDisplayCode = "CL-TAM-0001",
                    CutOrderSource = CutOrderSource.MANUAL,
                    CutOrderStatus = CutOrderStatus.IN_SESSION,
                    TransactionType = TransactionType.OUTBOUND,
                    VehiclePlate = "14C-22222",
                    CustomerName = "KH tạm",
                    ProductName = "XM Bao",
                    ProductType = ProductTypes.Bagged,
                    PlannedWeight = 25500m,
                    TareWeightKg = 1m,
                    BagWeightKg = 50m,
                    IsExportScale = true,
                    IsTemporaryExport = true,
                    ProcessingStage = ProcessingStage.WEIGHING,
                    CreatedAt = baseDate.AddMinutes(1),
                    CreatedBy = "TEST"
                },
                new CutOrder
                {
                    Id = temporaryOnlyCutOrderId,
                    StationCode = "QN01",
                    TemporaryExportDisplayCode = "CL-TAM-0099",
                    CutOrderSource = CutOrderSource.MANUAL,
                    CutOrderStatus = CutOrderStatus.REGISTERED,
                    TransactionType = TransactionType.OUTBOUND,
                    VehiclePlate = "14C-33333",
                    CustomerName = "KH tạm riêng",
                    ProductName = "XM Rời",
                    ProductType = ProductTypes.Bulk,
                    PlannedWeight = 30000m,
                    IsExportScale = false,
                    IsTemporaryExport = true,
                    ProcessingStage = ProcessingStage.IN_YARD,
                    CreatedAt = baseDate.AddMinutes(2),
                    CreatedBy = "TEST"
                });

            var session = CreateSession("QN01", "LC26062299", "14C-11111", baseDate.AddHours(9), 51000m);
            db.WeighingSessions.Add(session);
            db.WeighingSessionLines.Add(CreateLine("QN01", session.Id, historicalCutOrderId, 1, 51000m, 1000, false, baseDate.AddHours(9)));

            await db.SaveChangesAsync();
        }

        await using (var db = new StationDbContext(_dbOptions))
        {
            var repo = new AppConfigRepository(db, new SystemClock());
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var service = new ExportSummaryReportService(db, repo, configuration);

            var options = await service.GetExportScaleCutOrderOptionsAsync(CancellationToken.None);

            Assert.Contains(options, x => x.Code == historicalCutOrderId.ToString("D") && x.DisplayName.Contains("LCXK-HIS-01"));
            Assert.Contains(options, x => x.Code == activeTemporaryCutOrderId.ToString("D") && x.DisplayName.Contains("CL-TAM-0001"));
            Assert.Contains(options, x => x.Code == temporaryOnlyCutOrderId.ToString("D") && x.DisplayName.Contains("CL-TAM-0099"));
        }
    }

    [Fact]
    public async Task ExportExportScaleAsync_BuildsWorkbookStructureMatchingTemplateSections()
    {
        var exporter = new ExportSummaryReportExcelExporter();
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-bao-cao-xuat-xk.xlsx");

        try
        {
            var document = new ExportScaleSummaryReportDocument(
                Guid.NewGuid(),
                "CL-TAM-0004",
                "CÔNG TY CỔ PHẦN THƯƠNG MẠI VIỆT MỸ ANH",
                "Jumbo đóng bao 1,5 tấn",
                1500m,
                42857,
                3.5m,
                50m,
                53.5m,
                new DateTime(2026, 6, 22),
                "Bùi Ngọc Chiến",
                new[]
                {
                    new ExportScaleSummaryReportRow(
                        1,
                        "LC26060095",
                        "A",
                        new DateTime(2026, 6, 21),
                        "14c-40933",
                        3.001m,
                        2,
                        0.007m,
                        0m,
                        0,
                        2.994m,
                        2,
                        -6m,
                        -3m,
                        string.Empty,
                        false),
                    new ExportScaleSummaryReportRow(
                        2,
                        "LC26060096",
                        "B",
                        new DateTime(2026, 6, 22),
                        "14c-40934",
                        1.325m,
                        1,
                        0.0035m,
                        1.3215m,
                        1,
                        0m,
                        0,
                        -178.5m,
                        -178.5m,
                        "Hàng rách vỡ hoàn",
                        true)
                });

            await exporter.ExportExportScaleAsync(document, outputPath, CancellationToken.None);

            using var workbook = new XLWorkbook(outputPath);
            var sheet = workbook.Worksheet("BaoCaoXuatXK");

            Assert.Equal("BÁO CÁO XUẤT - XK", sheet.Cell("A2").GetString());
            Assert.Equal("Khách hàng", sheet.Cell("C4").GetString());
            Assert.Equal("Cắt lệnh", sheet.Cell("J4").GetString());
            Assert.Equal("CL-TAM-0004", sheet.Cell("L4").GetString());
            Assert.Equal("Số lượng giao hàng (tấn)", sheet.Cell("J8").GetString());
            Assert.Equal("+K11*E7/1000", sheet.Cell("L8").FormulaA1);
            Assert.Equal("(Quy đổi từ số bao thực giao)", sheet.Cell("M8").GetString());
            Assert.Equal("Số lượng đặt hàng", sheet.Cell("C9").GetString());
            Assert.Equal("Theo lô (tấn)", sheet.Cell("L10").GetString());
            Assert.Equal("Số lượng qua cân", sheet.Cell("E13").GetString());
            Assert.Equal("Theo chuyến", sheet.Cell("L14").GetString());
            Assert.Equal("Ghi chú", sheet.Cell("N13").GetString());
            Assert.Equal("", sheet.Cell("O15").GetString());
            Assert.Equal("", sheet.Cell("O16").GetString());
            Assert.Equal("", sheet.Cell("P15").GetString());
            Assert.Equal("Hoàn", sheet.Cell("P16").GetString());
            Assert.Equal("Hàng rách vỡ hoàn", sheet.Cell("N16").GetString());
            Assert.Equal("E6+E7", sheet.Cell("E8").FormulaA1);
            Assert.Equal("COUNTIF(P15:P16,\"<>Hoàn\")", sheet.Cell("L6").FormulaA1);
            Assert.Equal("COUNTIF(H15:H16,\">0\")", sheet.Cell("L7").FormulaA1);
            Assert.Equal("SUMIF(P15:P16,\"<>Hoàn\",E15:E16)", sheet.Cell("E11").FormulaA1);
            Assert.Equal("SUMIF(P15:P16,\"<>Hoàn\",F15:F16)", sheet.Cell("F11").FormulaA1);
            Assert.Equal("SUM(H15:H16)", sheet.Cell("H11").FormulaA1);
            Assert.Equal("SUM(I15:I16)", sheet.Cell("I11").FormulaA1);
            Assert.Equal("SUMIFS($E$15:$E$16,$B$15:$B$16,Q7,$C$15:$C$16,$R$7)-SUMIFS($H$15:$H$16,$B$15:$B$16,Q7,$C$15:$C$16,$R$7)", sheet.Cell("S7").FormulaA1);
            Assert.Equal("IF(F15=0,\"-\",L15/F15)", sheet.Cell("M15").FormulaA1);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    public void Dispose()
    {
        using var db = new StationDbContext(_dbOptions);
        db.Database.EnsureDeleted();
    }

    private static WeighingSession CreateSession(string stationCode, string sessionNo, string vehiclePlate, DateTime weight2Time, decimal netWeight)
    {
        return new WeighingSession
        {
            Id = Guid.NewGuid(),
            StationCode = stationCode,
            SessionNo = sessionNo,
            TransactionType = TransactionType.OUTBOUND,
            VehiclePlate = vehiclePlate,
            SessionStatus = WeighingSessionStatus.COMPLETED,
            NetWeight = netWeight,
            Weight2Time = weight2Time,
            CreatedAt = weight2Time.AddMinutes(-30),
            CreatedBy = "TEST"
        };
    }

    private static WeighingSessionLine CreateLine(
        string stationCode,
        Guid sessionId,
        Guid cutOrderId,
        int sequenceNo,
        decimal actualAllocatedWeight,
        int bagCount,
        bool isReturnedBrokenTrip,
        DateTime createdAt)
    {
        return new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            StationCode = stationCode,
            WeighingSessionId = sessionId,
            CutOrderId = cutOrderId,
            SequenceNo = sequenceNo,
            ActualAllocatedWeight = actualAllocatedWeight,
            ActualAllocatedBagCount = bagCount,
            BagCountDisplay = bagCount,
            IsReturnedBrokenTrip = isReturnedBrokenTrip,
            LineStatus = WeighingSessionLineStatus.ALLOCATED,
            CreatedAt = createdAt,
            CreatedBy = "TEST"
        };
    }

    private static async Task SetConfigValueAsync(StationDbContext db, string key, string value)
    {
        var now = DateTime.UtcNow;
        var config = await db.AppConfigs.FirstOrDefaultAsync(x => x.ConfigKey == key);
        if (config is null)
        {
            db.AppConfigs.Add(new AppConfig
            {
                ConfigKey = key,
                ConfigValue = value,
                CreatedAt = now,
                CreatedBy = "TEST",
                UpdatedAt = now,
                UpdatedBy = "TEST"
            });
        }
        else
        {
            config.ConfigValue = value;
            config.UpdatedAt = now;
            config.UpdatedBy = "TEST";
        }

        await db.SaveChangesAsync();
    }
}
