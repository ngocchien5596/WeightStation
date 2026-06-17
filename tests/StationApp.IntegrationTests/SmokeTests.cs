using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.Infrastructure.Repositories;
using StationApp.Infrastructure.Services;
using Xunit;

namespace StationApp.IntegrationTests;

public class SmokeTests : IDisposable
{
    private readonly IHost _host;
    private readonly IServiceProvider _services;

    public SmokeTests()
    {
        _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<StationDbContext>(options =>
                    options.UseSqlServer("Server=.;Database=StationApp_SmokeTest;Trusted_Connection=True;TrustServerCertificate=True;",
                        sql => sql.UseCompatibilityLevel(120)));

                services.AddScoped<IDocumentCounterService, DocumentCounterService>();
                services.AddScoped<ITicketRepository, TicketRepository>();
                services.AddScoped<IWeighTicketRepository, TicketRepository>();
                services.AddScoped<ICutOrderRepository, CutOrderRepository>();
                services.AddScoped<IDeliveryTicketRepository, DeliveryTicketRepository>();
                services.AddScoped<ISyncOutboxRepository, SyncOutboxRepository>();
                services.AddScoped<IAuditLogRepository, AuditLogRepository>();
                services.AddScoped<IAppConfigRepository, AppConfigRepository>();
                services.AddScoped<IUserRepository, UserRepository>();
                services.AddScoped<IVehicleRepository, VehicleRepository>();
                services.AddScoped<ICustomerRepository, CustomerRepository>();
                services.AddScoped<IProductRepository, ProductRepository>();
                services.AddScoped<IStationOperationSettingsRepository, StationOperationSettingsRepository>();
                services.AddScoped<IStationAdministrationService, StationAdministrationService>();
                services.AddScoped<IUnitOfWork, EfUnitOfWork>();
                services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();
                services.AddScoped<IDeliveryNumberGenerator, DeliveryNumberGenerator>();
                services.AddScoped<IWeighingSessionNumberGenerator, WeighingSessionNumberGenerator>();
                services.AddSingleton<IAppVersionProvider, AppVersionProvider>();
                services.AddSingleton<IClock, SystemClock>();
                services.AddSingleton<ICurrentUserContext, CurrentUserContext>();
                services.AddScoped<IToleranceProvider, ToleranceProvider>();
                services.AddScoped<IAuditService, AuditService>();
                services.AddScoped<ISyncPayloadFactory, SyncPayloadFactory>();
                services.AddScoped<CreateCutOrderUseCase>();
                services.AddScoped<CaptureWeight1UseCase>();
                services.AddScoped<CaptureWeight2UseCase>();
                services.AddScoped<EnsurePrimaryDeliveryTicketUseCase>();
                services.AddScoped<CompleteTicketUseCase>();
                services.AddScoped<CancelTicketUseCase>();
            })
            .Build();

        _services = _host.Services;
        
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        db.Database.EnsureDeleted();
        StationDatabaseInitializer.InitializeAsync(
            db,
            scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>(),
            CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task EndToEnd_Outbound_Flow_Success()
    {
        using var scope = _services.CreateScope();
        var createUseCase = scope.ServiceProvider.GetRequiredService<CreateCutOrderUseCase>();
        var capture1UseCase = scope.ServiceProvider.GetRequiredService<CaptureWeight1UseCase>();
        var capture2UseCase = scope.ServiceProvider.GetRequiredService<CaptureWeight2UseCase>();
        var completeUseCase = scope.ServiceProvider.GetRequiredService<CompleteTicketUseCase>();
        var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

        // 1. Create cut order
        var createRequest = new CreateCutOrderRequest(
            VehiclePlate: "30A-12345",
            CutOrderSource: CutOrderSource.MANUAL,
            TransactionType: TransactionType.OUTBOUND,
            CustomerName: "Test Customer",
            ProductName: "Test Product",
            PlannedWeight: 25000,
            TransportMethod: TransportMethod.ROAD
        );
        var createResult = await createUseCase.ExecuteAsync(createRequest, default);
        Assert.True(createResult.Success);
        var cutOrderId = createResult.Data!.Id;

        // 2. Capture Weight 1 (Creates Weigh Ticket)
        var w1Result = await capture1UseCase.ExecuteAsync(new CaptureWeightRequest(cutOrderId, 10000, true, WeightMode.MANUAL), default);
        Assert.True(w1Result.Success);
        var ticketId = w1Result.Data!.Id;
        
        var ticket = await db.WeighTickets.FindAsync(ticketId);
        Assert.NotNull(ticket);
        Assert.StartsWith("QN", ticket.TicketNo);
        Assert.Equal(10000, ticket.Weight1);
        Assert.Equal(TicketStatus.LOADING_STARTED, ticket.Status);

        // 3. Capture Weight 2
        var w2Result = await capture2UseCase.ExecuteAsync(new CaptureWeightRequest(cutOrderId, 35500, true, WeightMode.MANUAL), default);
        Assert.True(w2Result.Success);
        
        ticket = await db.WeighTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId);
        Assert.Equal(35500, ticket!.Weight2);

        // 4. Complete
        var completeResult = await completeUseCase.ExecuteAsync(new CompleteTicketRequest(cutOrderId), default);
        Assert.True(completeResult.Success);

        ticket = await db.WeighTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId);
        Assert.Equal(TicketStatus.TICKET_COMPLETED, ticket!.Status);
        Assert.Equal(25500, ticket.NetWeight); 
        
        // 5. Verify Audit Logs & Outbox
        var auditCount = await db.AuditLogs.CountAsync(a => a.EntityId == cutOrderId);
        Assert.True(auditCount >= 3);

        var outboxCount = await db.SyncOutbox.CountAsync(o => o.AggregateId == cutOrderId);
        Assert.True(outboxCount >= 1);
    }

    [Fact]
    public async Task Concurrency_Safe_Generators_Guarantee_No_Duplicates()
    {
        const int concurrentTasksCount = 20;
        var tasks = new Task<string>[concurrentTasksCount];

        for (int i = 0; i < concurrentTasksCount; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var scope = _services.CreateScope();
                var generator = scope.ServiceProvider.GetRequiredService<ITicketNumberGenerator>();
                return await generator.GenerateAsync(default);
            });
        }

        var results = await Task.WhenAll(tasks);

        // Verify that all generated ticket numbers are unique
        var uniqueResults = new HashSet<string>(results);
        Assert.Equal(concurrentTasksCount, uniqueResults.Count);

        // Verify that they are sequential (e.g. QN26060001, QN26060002... up to QN26060020)
        var sortedResults = results.OrderBy(x => x).ToList();
        for (int i = 0; i < concurrentTasksCount; i++)
        {
            var expectedSuffix = $"{i + 1:D4}";
            Assert.EndsWith(expectedSuffix, sortedResults[i]);
        }
    }

    [Fact]
    public async Task MasterData_Partitioning_By_StationCode_Works()
    {
        using var scope = _services.CreateScope();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Set station to QN01
        StationRuntimeScope.Set("QN01", "Trạm QN01");

        var v1 = new Vehicle { Id = Guid.NewGuid(), VehiclePlate = "29A-11111", CreatedBy = "TEST" };
        var c1 = new Customer { Id = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "Cust 1", CreatedBy = "TEST" };
        var p1 = new Product { Id = Guid.NewGuid(), ProductCode = "P1", ProductName = "Prod 1", CreatedBy = "TEST" };

        await vehicleRepo.AddAsync(v1, default);
        await customerRepo.AddAsync(c1, default);
        await productRepo.AddAsync(p1, default);
        await uow.SaveChangesAsync(default);

        // Set station to QN02
        StationRuntimeScope.Set("QN02", "Trạm QN02");

        var v2 = new Vehicle { Id = Guid.NewGuid(), VehiclePlate = "29A-22222", CreatedBy = "TEST" };
        var c2 = new Customer { Id = Guid.NewGuid(), CustomerCode = "C2", CustomerName = "Cust 2", CreatedBy = "TEST" };
        var p2 = new Product { Id = Guid.NewGuid(), ProductCode = "P2", ProductName = "Prod 2", CreatedBy = "TEST" };

        await vehicleRepo.AddAsync(v2, default);
        await customerRepo.AddAsync(c2, default);
        await productRepo.AddAsync(p2, default);
        await uow.SaveChangesAsync(default);

        // Query while at QN02
        var qn02Vehicles = await vehicleRepo.SearchAsync(null, default);
        var qn02Customers = await customerRepo.SearchAsync(null, default);
        var qn02Products = await productRepo.SearchAsync(null, default);

        Assert.Single(qn02Vehicles);
        Assert.Equal("29A-22222", qn02Vehicles[0].VehiclePlate);
        Assert.Single(qn02Customers);
        Assert.Equal("C2", qn02Customers[0].CustomerCode);
        Assert.Single(qn02Products);
        Assert.Equal("P2", qn02Products[0].ProductCode);

        // Query while at QN01
        StationRuntimeScope.Set("QN01", "Trạm QN01");

        var qn01Vehicles = await vehicleRepo.SearchAsync(null, default);
        var qn01Customers = await customerRepo.SearchAsync(null, default);
        var qn01Products = await productRepo.SearchAsync(null, default);

        Assert.Single(qn01Vehicles);
        Assert.Equal("29A-11111", qn01Vehicles[0].VehiclePlate);
        Assert.Single(qn01Customers);
        Assert.Equal("C1", qn01Customers[0].CustomerCode);
        Assert.Single(qn01Products);
        Assert.Equal("P1", qn01Products[0].ProductCode);
    }

    [Fact]
    public async Task Duplicate_MasterData_Keys_Allowed_Across_Different_Stations_But_Blocked_Within_Same_Station()
    {
        using var scope = _services.CreateScope();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // 1. Add vehicle and customer to QN01
        StationRuntimeScope.Set("QN01", "Trạm QN01");
        var v1 = new Vehicle { Id = Guid.NewGuid(), VehiclePlate = "30A-88888", MoocNumber = "", CreatedBy = "TEST" };
        var c1 = new Customer { Id = Guid.NewGuid(), CustomerCode = "CUST-DUP", CustomerName = "Cust QN01", CreatedBy = "TEST" };
        await vehicleRepo.AddAsync(v1, default);
        await customerRepo.AddAsync(c1, default);
        await uow.SaveChangesAsync(default);

        // 2. Add same keys to QN02 -> Should succeed due to partition by StationCode
        StationRuntimeScope.Set("QN02", "Trạm QN02");
        var v2 = new Vehicle { Id = Guid.NewGuid(), VehiclePlate = "30A-88888", MoocNumber = "", CreatedBy = "TEST" };
        var c2 = new Customer { Id = Guid.NewGuid(), CustomerCode = "CUST-DUP", CustomerName = "Cust QN02", CreatedBy = "TEST" };
        await vehicleRepo.AddAsync(v2, default);
        await customerRepo.AddAsync(c2, default);
        await uow.SaveChangesAsync(default);

        // 3. Try to add same keys to QN01 again -> Should fail unique constraint
        StationRuntimeScope.Set("QN01", "Trạm QN01");
        var v3 = new Vehicle { Id = Guid.NewGuid(), VehiclePlate = "30A-88888", MoocNumber = "", CreatedBy = "TEST" };
        await vehicleRepo.AddAsync(v3, default);
        await Assert.ThrowsAnyAsync<Exception>(async () => await uow.SaveChangesAsync(default));
    }

    [Fact]
    public async Task Station_Operation_Settings_Load_And_Save_Success()
    {
        using var scope = _services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IStationAdministrationService>();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserContext>();
        var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();

        // 1. Sign in as Admin
        currentUser.SignIn(Guid.NewGuid(), "admin", "Test Admin", "ADMIN");

        // 2. Prepare database station
        var station = new Station
        {
            Id = Guid.NewGuid(),
            StationCode = "TEST01",
            StationName = "Trạm Test 01",
            IsActive = true,
            SortOrder = 1,
            CreatedAt = DateTime.Now,
            CreatedBy = "TEST"
        };
        await db.Stations.AddAsync(station);
        await db.SaveChangesAsync();

        // 3. Save settings request
        var settingsDto = new StationOperationSettingsDto(
            CrusherSingleWeighEnabled: true,
            CrusherDefaultWeighMode: "SINGLE_WITH_STANDARD_TARE",
            CrusherDefaultProductCode: "ĐV_TEST",
            CrusherDefaultCustomerCode: "KH_TEST",
            ClaySingleWeighEnabled: false,
            ClayDefaultWeighMode: "TWO_WEIGH",
            ClayDefaultProductCode: "SET_TEST",
            ClayDefaultCustomerCode: "KH_CLAY_TEST"
        );
        
        var featuresDto = new StationFeatureSetDto(
            ShowMenuDashboard: true,
            ShowMenuIncomingVehicleList: true,
            ShowMenuWeighing: true,
            ShowMenuCrusherWeighing: true,
            ShowMenuClayWeighing: true,
            ShowMenuExportWeighing: true,
            ShowMenuOutgoingVehicleList: true,
            ShowMenuExportReport: true,
            ShowMenuInboundReport: true,
            ShowMenuCrusherInboundReport: true,
            ShowMenuClayInboundReport: true,
            ShowDashboardInboundKpi: true,
            ShowDashboardOutboundKpi: true,
            DefaultNavigationTarget: "Dashboard"
        );

        var request = new SaveStationRequest(
            StationId: station.Id,
            StationCode: "TEST01",
            StationName: "Trạm Test 01 Updated",
            IsActive: true,
            SortOrder: 2,
            Features: featuresDto,
            Settings: settingsDto
        );

        var saveResult = await adminService.SaveStationAsync(request, default);
        Assert.NotNull(saveResult);
        Assert.Equal("TEST01", saveResult.StationCode);
        Assert.Equal("Trạm Test 01 Updated", saveResult.StationName);
        Assert.True(saveResult.Settings.CrusherSingleWeighEnabled);
        Assert.Equal("SINGLE_WITH_STANDARD_TARE", saveResult.Settings.CrusherDefaultWeighMode);
        Assert.Equal("ĐV_TEST", saveResult.Settings.CrusherDefaultProductCode);
        Assert.Equal("KH_TEST", saveResult.Settings.CrusherDefaultCustomerCode);

        // 4. Load/Search stations to verify loaded settings
        var searchResult = await adminService.SearchStationsAsync("TEST01", null, null, default);
        Assert.Single(searchResult);
        var loadedStation = searchResult[0];
        Assert.Equal("TEST01", loadedStation.StationCode);
        Assert.True(loadedStation.Settings.CrusherSingleWeighEnabled);
        Assert.Equal("SINGLE_WITH_STANDARD_TARE", loadedStation.Settings.CrusherDefaultWeighMode);
        Assert.Equal("ĐV_TEST", loadedStation.Settings.CrusherDefaultProductCode);
        Assert.Equal("KH_TEST", loadedStation.Settings.CrusherDefaultCustomerCode);
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}


