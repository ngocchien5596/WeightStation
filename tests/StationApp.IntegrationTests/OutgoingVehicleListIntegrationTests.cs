using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.Infrastructure.Repositories;
using StationApp.Infrastructure.Services;
using Xunit;

namespace StationApp.IntegrationTests;

public class OutgoingVehicleListIntegrationTests : IDisposable
{
    private const string ErpCutOrderPrefix = "TEST-OUTGOING-DATE-";
    private const string SessionPrefix = "LCTESTOUTDATE-";
    private const string StationCode = "QN01";

    private readonly IHost _host;
    private readonly IServiceProvider _services;

    public OutgoingVehicleListIntegrationTests()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<StationDbContext>(options =>
                    options.UseSqlServer("Server=.;Database=StationAppLocal;Trusted_Connection=True;TrustServerCertificate=True;",
                        sql => sql.UseCompatibilityLevel(120)));

                services.AddScoped<ICutOrderRepository, CutOrderRepository>();
            })
            .Build();

        _services = _host.Services;
        StationRuntimeScope.Set(StationCode, "Tram QN01");

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        StationDatabaseInitializer.InitializeAsync(db, null, CancellationToken.None).GetAwaiter().GetResult();

        CleanupTestData();
    }

    [Fact]
    public async Task GetOutgoingListAsync_ExportFlow_DoesNotUseUpdatedAtAsCompletedDate()
    {
        var selectedDate = new DateTime(2026, 6, 22);
        var previousDate = selectedDate.AddDays(-1);

        var previousSessionId = Guid.NewGuid();
        var previousCutOrderId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var currentCutOrderId = Guid.NewGuid();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

            db.CutOrders.AddRange(
                new CutOrder
                {
                    Id = previousCutOrderId,
                    StationCode = StationCode,
                    ErpCutOrderId = $"{ErpCutOrderPrefix}OLD",
                    CutOrderSource = CutOrderSource.ERP,
                    CutOrderStatus = CutOrderStatus.COMPLETED,
                    TransactionType = TransactionType.OUTBOUND,
                    VehiclePlate = "14C-40933",
                    ProductCode = "XM-B",
                    ProductName = "Xi mang bao",
                    ProductType = "Bao",
                    ProcessingStage = ProcessingStage.OUT_YARD,
                    WeighingSessionId = previousSessionId,
                    IsExportScale = true,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    IdempotencyKey = Guid.NewGuid(),
                    CreatedAt = previousDate.AddHours(-2),
                    UpdatedAt = selectedDate.AddHours(8),
                    CreatedBy = "tester"
                },
                new CutOrder
                {
                    Id = currentCutOrderId,
                    StationCode = StationCode,
                    ErpCutOrderId = $"{ErpCutOrderPrefix}NEW",
                    CutOrderSource = CutOrderSource.ERP,
                    CutOrderStatus = CutOrderStatus.COMPLETED,
                    TransactionType = TransactionType.OUTBOUND,
                    VehiclePlate = "14C-40934",
                    ProductCode = "XM-B",
                    ProductName = "Xi mang bao",
                    ProductType = "Bao",
                    ProcessingStage = ProcessingStage.OUT_YARD,
                    WeighingSessionId = currentSessionId,
                    IsExportScale = true,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    IdempotencyKey = Guid.NewGuid(),
                    CreatedAt = selectedDate.AddHours(-2),
                    UpdatedAt = selectedDate.AddHours(9),
                    CreatedBy = "tester"
                });

            db.WeighingSessions.AddRange(
                new WeighingSession
                {
                    Id = previousSessionId,
                    StationCode = StationCode,
                    SessionNo = $"{SessionPrefix}001",
                    TransactionType = TransactionType.OUTBOUND,
                    VehiclePlate = "14C-40933",
                    ProductCode = "XM-B",
                    ProductName = "Xi mang bao",
                    SessionStatus = WeighingSessionStatus.COMPLETED,
                    Weight1 = 12_000m,
                    Weight1Time = selectedDate.AddHours(6),
                    Weight2 = 32_550m,
                    Weight2Time = previousDate.AddHours(8),
                    NetWeight = 20_550m,
                    CreatedAt = previousDate.AddHours(6),
                    UpdatedAt = selectedDate.AddHours(8),
                    CreatedBy = "tester"
                },
                new WeighingSession
                {
                    Id = currentSessionId,
                    StationCode = StationCode,
                    SessionNo = $"{SessionPrefix}002",
                    TransactionType = TransactionType.OUTBOUND,
                    VehiclePlate = "14C-40934",
                    ProductCode = "XM-B",
                    ProductName = "Xi mang bao",
                    SessionStatus = WeighingSessionStatus.COMPLETED,
                    Weight1 = 13_000m,
                    Weight1Time = selectedDate.AddHours(8),
                    Weight2 = 33_050m,
                    Weight2Time = selectedDate.AddHours(9),
                    NetWeight = 20_050m,
                    CreatedAt = selectedDate.AddHours(7),
                    UpdatedAt = selectedDate.AddHours(9).AddMinutes(30),
                    CreatedBy = "tester"
                });

            db.WeighingSessionLines.AddRange(
                new WeighingSessionLine
                {
                    Id = Guid.NewGuid(),
                    StationCode = StationCode,
                    WeighingSessionId = previousSessionId,
                    CutOrderId = previousCutOrderId,
                    SequenceNo = 1,
                    ProductCode = "XM-B",
                    ProductName = "Xi mang bao",
                    ActualAllocatedWeight = 20_550m,
                    ActualAllocatedBagCount = 411,
                    LineStatus = WeighingSessionLineStatus.ALLOCATED,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = previousDate.AddHours(8),
                    CreatedBy = "tester"
                },
                new WeighingSessionLine
                {
                    Id = Guid.NewGuid(),
                    StationCode = StationCode,
                    WeighingSessionId = currentSessionId,
                    CutOrderId = currentCutOrderId,
                    SequenceNo = 1,
                    ProductCode = "XM-B",
                    ProductName = "Xi mang bao",
                    ActualAllocatedWeight = 20_050m,
                    ActualAllocatedBagCount = 401,
                    LineStatus = WeighingSessionLineStatus.ALLOCATED,
                    SyncStatus = SyncStatus.SYNC_QUEUED,
                    CreatedAt = selectedDate.AddHours(9),
                    CreatedBy = "tester"
                });

            await db.SaveChangesAsync();
        }

        using (var scope = _services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();

            var items = await repo.GetOutgoingListAsync(
                new OutgoingVehicleListFilter(
                    SessionNo: null,
                    ErpCutOrderId: null,
                    VehiclePlate: null,
                    MoocNumber: null,
                    ReceiverName: null,
                    CustomerName: null,
                    CompletedDate: selectedDate,
                    FlowType: OutgoingFlowType.Export),
                CancellationToken.None);

            var erpIds = items.Select(x => x.ErpCutOrderId).ToList();

            Assert.Contains($"{ErpCutOrderPrefix}NEW", erpIds);
            Assert.DoesNotContain($"{ErpCutOrderPrefix}OLD", erpIds);
        }
    }

    public void Dispose()
    {
        CleanupTestData();
        _host.Dispose();
        StationRuntimeScope.Clear();
    }

    private void CleanupTestData()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

        var cutOrderIds = db.CutOrders
            .Where(x => x.ErpCutOrderId != null && x.ErpCutOrderId.StartsWith(ErpCutOrderPrefix))
            .Select(x => x.Id)
            .ToList();

        var sessionIds = db.WeighingSessions
            .Where(x => x.SessionNo.StartsWith(SessionPrefix))
            .Select(x => x.Id)
            .ToList();

        if (cutOrderIds.Count > 0)
        {
            db.WeighingSessionLines.RemoveRange(db.WeighingSessionLines.Where(x => cutOrderIds.Contains(x.CutOrderId)));
            db.DeliveryTickets.RemoveRange(db.DeliveryTickets.Where(x => cutOrderIds.Contains(x.CutOrderId)));
            db.WeighTickets.RemoveRange(db.WeighTickets.Where(x => cutOrderIds.Contains(x.CutOrderId)));
            db.CutOrders.RemoveRange(db.CutOrders.Where(x => cutOrderIds.Contains(x.Id)));
        }

        if (sessionIds.Count > 0)
        {
            db.WeighingSessionLines.RemoveRange(db.WeighingSessionLines.Where(x => sessionIds.Contains(x.WeighingSessionId)));
            db.WeighTickets.RemoveRange(db.WeighTickets.Where(x => x.WeighingSessionId.HasValue && sessionIds.Contains(x.WeighingSessionId.Value)));
            db.DeliveryTickets.RemoveRange(db.DeliveryTickets.Where(x => x.WeighingSessionId.HasValue && sessionIds.Contains(x.WeighingSessionId.Value)));
            db.WeighingSessions.RemoveRange(db.WeighingSessions.Where(x => sessionIds.Contains(x.Id)));
        }

        db.SaveChanges();
    }
}
