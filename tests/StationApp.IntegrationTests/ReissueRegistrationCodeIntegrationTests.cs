using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.Infrastructure.Repositories;
using Xunit;

namespace StationApp.IntegrationTests;

public class ReissueRegistrationCodeIntegrationTests : IDisposable
{
    private const string ErpCutOrderPrefix = "TEST-REISSUE-";
    private const string RegistrationCodePrefix = "TEST-DKPT-";
    private const string SessionNoPrefix = "LCTESTREG-";

    private readonly IHost _host;
    private readonly IServiceProvider _services;

    public ReissueRegistrationCodeIntegrationTests()
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

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();
            StationDatabaseInitializer.InitializeAsync(
                db,
                null,
                CancellationToken.None).GetAwaiter().GetResult();
        }

        CleanupTestData();
    }

    [Fact]
    public async Task IncomingList_SuggestsReusableSessionAndCarryForward_FromDeletedCutOrderWithSameRegistrationCode()
    {
        var now = DateTime.Now;
        var sessionId = Guid.NewGuid();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

            db.WeighingSessions.Add(new WeighingSession
            {
                Id = sessionId,
                SessionNo = $"{SessionNoPrefix}001",
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-99999",
                MoocNumber = "99R-99999",
                DriverName = "Hoang Van Vinh",
                Weight1 = 12_345m,
                Weight1Time = now.AddMinutes(-30),
                SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
                CreatedAt = now.AddHours(-1),
                CreatedBy = "tester"
            });

            db.CutOrders.Add(new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = $"{ErpCutOrderPrefix}OLD-001",
                ErpRegistrationCode = $"{RegistrationCodePrefix}001",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.CANCELLED,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                ProductCode = "XM1",
                ProductName = "Xi mang",
                CarryForwardWeight1 = 12_345m,
                CarryForwardWeight1Time = now.AddMinutes(-30),
                WeighingSessionId = sessionId,
                ProcessingStage = ProcessingStage.WEIGHING,
                IsDeleted = true,
                DeletedAt = now.AddMinutes(-5),
                CreatedAt = now.AddHours(-2),
                CreatedBy = "erp"
            });

            db.CutOrders.Add(new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = $"{ErpCutOrderPrefix}NEW-001",
                ErpRegistrationCode = $"{RegistrationCodePrefix}001",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.REGISTERED,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                ReceiverName = "Hoang Van Vinh",
                CustomerCode = "C1",
                CustomerName = "Customer 1",
                ProductCode = "XM1",
                ProductName = "Xi mang",
                PlannedWeight = 36_000m,
                ProcessingStage = ProcessingStage.IN_YARD,
                SyncStatus = SyncStatus.SYNC_QUEUED,
                CreatedAt = now,
                CreatedBy = "erp"
            });

            await db.SaveChangesAsync();
        }

        using (var scope = _services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();

            var list = await repo.GetIncomingListAsync(
                new IncomingVehicleListFilter($"{ErpCutOrderPrefix}NEW-001", null, null, null, null, null, null),
                CancellationToken.None);

            var item = Assert.Single(list);
            Assert.Equal(12_345m, item.CarryForwardWeight1);
            Assert.Equal($"{SessionNoPrefix}001", item.SuggestedSessionNo);
        }
    }

    [Fact]
    public async Task IncomingList_DoesNotSuggestCompletedSession_ButStillKeepsCarryForward_FromDeletedCutOrderWithSameRegistrationCode()
    {
        var now = DateTime.Now;
        var sessionId = Guid.NewGuid();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

            db.WeighingSessions.Add(new WeighingSession
            {
                Id = sessionId,
                SessionNo = $"{SessionNoPrefix}002",
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                Weight1 = 11_111m,
                Weight1Time = now.AddMinutes(-30),
                SessionStatus = WeighingSessionStatus.COMPLETED,
                CreatedAt = now.AddHours(-1),
                CreatedBy = "tester"
            });

            db.CutOrders.Add(new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = $"{ErpCutOrderPrefix}OLD-002",
                ErpRegistrationCode = $"{RegistrationCodePrefix}002",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.CANCELLED,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                CarryForwardWeight1 = 11_111m,
                CarryForwardWeight1Time = now.AddMinutes(-30),
                WeighingSessionId = sessionId,
                ProcessingStage = ProcessingStage.WEIGHING,
                IsDeleted = true,
                DeletedAt = now.AddMinutes(-5),
                CreatedAt = now.AddHours(-2),
                CreatedBy = "erp"
            });

            db.CutOrders.Add(new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = $"{ErpCutOrderPrefix}NEW-002",
                ErpRegistrationCode = $"{RegistrationCodePrefix}002",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.REGISTERED,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                ProcessingStage = ProcessingStage.IN_YARD,
                SyncStatus = SyncStatus.SYNC_QUEUED,
                CreatedAt = now,
                CreatedBy = "erp"
            });

            await db.SaveChangesAsync();
        }

        using (var scope = _services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();

            var list = await repo.GetIncomingListAsync(
                new IncomingVehicleListFilter($"{ErpCutOrderPrefix}NEW-002", null, null, null, null, null, null),
                CancellationToken.None);

            var item = Assert.Single(list);
            Assert.Equal(11_111m, item.CarryForwardWeight1);
            Assert.Null(item.SuggestedSessionNo);
        }
    }

    [Fact]
    public async Task IncomingList_DoesNotSuggestSession_WhenRegistrationCodeHistoryHasNoValidSessionReference()
    {
        var now = DateTime.Now;
        var sessionId = Guid.NewGuid();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

            db.WeighingSessions.Add(new WeighingSession
            {
                Id = sessionId,
                SessionNo = $"{SessionNoPrefix}003",
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                DriverName = "Hoang Van Vinh",
                Weight1 = 13_579m,
                Weight1Time = now.AddMinutes(-25),
                SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
                CreatedAt = now.AddHours(-1),
                CreatedBy = "tester"
            });

            db.CutOrders.Add(new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = $"{ErpCutOrderPrefix}OLD-003",
                ErpRegistrationCode = $"{RegistrationCodePrefix}003",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.CANCELLED,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                CarryForwardWeight1 = 13_579m,
                CarryForwardWeight1Time = now.AddMinutes(-25),
                WeighingSessionId = Guid.NewGuid(),
                ProcessingStage = ProcessingStage.WEIGHING,
                IsDeleted = true,
                DeletedAt = now.AddMinutes(-5),
                CreatedAt = now.AddHours(-2),
                CreatedBy = "erp"
            });

            db.CutOrders.Add(new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = $"{ErpCutOrderPrefix}NEW-003",
                ErpRegistrationCode = $"{RegistrationCodePrefix}003",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.REGISTERED,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                ReceiverName = "Hoang Van Vinh",
                CustomerCode = "C3",
                CustomerName = "Customer 3",
                ProductCode = "XM3",
                ProductName = "Xi mang 3",
                PlannedWeight = 35_000m,
                ProcessingStage = ProcessingStage.IN_YARD,
                SyncStatus = SyncStatus.SYNC_QUEUED,
                CreatedAt = now,
                CreatedBy = "erp"
            });

            await db.SaveChangesAsync();
        }

        using (var scope = _services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();

            var list = await repo.GetIncomingListAsync(
                new IncomingVehicleListFilter($"{ErpCutOrderPrefix}NEW-003", null, null, null, null, null, null),
                CancellationToken.None);

            var item = Assert.Single(list);
            Assert.Equal(13_579m, item.CarryForwardWeight1);
            Assert.Null(item.SuggestedSessionNo);
        }
    }

    [Fact]
    public async Task IncomingList_DoesNotSuggestOrCarryForward_WhenDeletedWeight1IsOlderThan24Hours()
    {
        var now = DateTime.Now;
        var sessionId = Guid.NewGuid();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

            db.WeighingSessions.Add(new WeighingSession
            {
                Id = sessionId,
                SessionNo = $"{SessionNoPrefix}004",
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                Weight1 = 14_222m,
                Weight1Time = now.AddHours(-25),
                SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2,
                CreatedAt = now.AddHours(-26),
                CreatedBy = "tester"
            });

            db.CutOrders.Add(new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = $"{ErpCutOrderPrefix}OLD-004",
                ErpRegistrationCode = $"{RegistrationCodePrefix}004",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.CANCELLED,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                CarryForwardWeight1 = 14_222m,
                CarryForwardWeight1Time = now.AddHours(-25),
                WeighingSessionId = sessionId,
                ProcessingStage = ProcessingStage.WEIGHING,
                IsDeleted = true,
                DeletedAt = now.AddMinutes(-5),
                CreatedAt = now.AddHours(-26),
                CreatedBy = "erp"
            });

            db.CutOrders.Add(new CutOrder
            {
                Id = Guid.NewGuid(),
                ErpCutOrderId = $"{ErpCutOrderPrefix}NEW-004",
                ErpRegistrationCode = $"{RegistrationCodePrefix}004",
                CutOrderSource = CutOrderSource.ERP,
                CutOrderStatus = CutOrderStatus.REGISTERED,
                TransactionType = TransactionType.OUTBOUND,
                VehiclePlate = "14C-18011",
                MoocNumber = "29R-00409",
                ProcessingStage = ProcessingStage.IN_YARD,
                SyncStatus = SyncStatus.SYNC_QUEUED,
                CreatedAt = now,
                CreatedBy = "erp"
            });

            await db.SaveChangesAsync();
        }

        using (var scope = _services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();

            var list = await repo.GetIncomingListAsync(
                new IncomingVehicleListFilter($"{ErpCutOrderPrefix}NEW-004", null, null, null, null, null, null),
                CancellationToken.None);

            var item = Assert.Single(list);
            Assert.Null(item.CarryForwardWeight1);
            Assert.Null(item.CarryForwardWeight1Time);
            Assert.Null(item.SuggestedSessionNo);
        }
    }

    public void Dispose()
    {
        CleanupTestData();
        _host.Dispose();
    }

    private void CleanupTestData()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

        db.Database.ExecuteSqlRaw($@"
DELETE FROM delivery_tickets WHERE DeliveryNo LIKE '{ErpCutOrderPrefix}%';
DELETE FROM weigh_tickets WHERE TicketNo LIKE '{ErpCutOrderPrefix}%';
DELETE FROM weighing_session_lines WHERE WeighingSessionId IN (
    SELECT Id FROM weighing_sessions WHERE SessionNo LIKE '{SessionNoPrefix}%'
);
DELETE FROM cut_orders WHERE ErpCutOrderId LIKE '{ErpCutOrderPrefix}%' OR ErpRegistrationCode LIKE '{RegistrationCodePrefix}%';
DELETE FROM weighing_sessions WHERE SessionNo LIKE '{SessionNoPrefix}%';
");
    }
}
