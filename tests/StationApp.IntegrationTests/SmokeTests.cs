using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
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

                services.AddScoped<ITicketRepository, TicketRepository>();
                services.AddScoped<IWeighTicketRepository, TicketRepository>();
                services.AddScoped<ICutOrderRepository, CutOrderRepository>();
                services.AddScoped<IDeliveryTicketRepository, DeliveryTicketRepository>();
                services.AddScoped<ISyncOutboxRepository, SyncOutboxRepository>();
                services.AddScoped<IAuditLogRepository, AuditLogRepository>();
                services.AddScoped<IAppConfigRepository, AppConfigRepository>();
                services.AddScoped<IUserRepository, UserRepository>();
                services.AddScoped<IUnitOfWork, EfUnitOfWork>();
                services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();
                services.AddScoped<IDeliveryNumberGenerator, DeliveryNumberGenerator>();
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

    public void Dispose()
    {
        _host.Dispose();
    }
}


