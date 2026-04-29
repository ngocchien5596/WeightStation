using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using StationApp.Infrastructure.Repositories;
using StationApp.Infrastructure.Services;
using StationApp.Sync.Services;
using Xunit;

namespace StationApp.IntegrationTests;

public class SyncTests : IDisposable
{
    private readonly string _mockApiUrl = "http://localhost:5001/";
    private readonly HttpListener _listener;
    private readonly IHost _host;

    public SyncTests()
    {
        _listener = new HttpListener();
        if (!HttpListener.IsSupported) throw new NotSupportedException("HttpListener is not supported");
        _listener.Prefixes.Add(_mockApiUrl);
        _listener.Start();

        _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<StationDbContext>(options =>
                    options.UseSqlServer("Server=.;Database=StationApp_SyncTest;Trusted_Connection=True;TrustServerCertificate=True;",
                        sql => sql.UseCompatibilityLevel(120)));

                services.AddScoped<ITicketRepository, TicketRepository>();
                services.AddScoped<ISyncOutboxRepository, SyncOutboxRepository>();
                services.AddScoped<IUnitOfWork, EfUnitOfWork>();
                services.AddSingleton<IClock, SystemClock>();
                services.AddScoped<ISyncPayloadFactory, SyncPayloadFactory>();

                services.AddHttpClient<ICentralApiClient, CentralApiClient>(client =>
                {
                    client.BaseAddress = new Uri(_mockApiUrl);
                    client.Timeout = TimeSpan.FromSeconds(5);
                });

                services.AddLogging(builder => builder.AddConsole());
            })
            .Build();

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        db.Database.EnsureDeleted();
        db.Database.Migrate();
    }

    [Fact]
    public async Task SyncWorker_Processes_Outbox_On_Success()
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
        var client = scope.ServiceProvider.GetRequiredService<ICentralApiClient>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // 1. Setup ticket and outbox
        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            TicketNo = "QN24010001",
            VehiclePlate = "30A-1111",
            IdempotencyKey = Guid.NewGuid(),
            Status = TicketStatus.TICKET_COMPLETED,
            SyncStatus = SyncStatus.SYNC_QUEUED,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        db.WeighTickets.Add(ticket);
        
        var outbox = new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateId = ticket.Id,
            AggregateType = "WeighTicket",
            PayloadJson = "{}",
            Status = OutboxStatus.PENDING,
            IdempotencyKey = ticket.IdempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
        db.SyncOutbox.Add(outbox);
        await uow.SaveChangesAsync(default);

        // 2. Start mock API response in background
        var mockTask = Task.Run(async () =>
        {
            var ctx = await _listener.GetContextAsync();
            var response = ctx.Response;
            var responseString = "{\"Success\":true}";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        });

        // 3. Process manually using worker logic
        var messages = await outboxRepo.GetPendingAsync(DateTime.UtcNow.AddMinutes(1), 10, default);
        Assert.Single(messages);

        foreach (var msg in messages)
        {
            var result = await client.PushTicketAsync(msg.PayloadJson, msg.IdempotencyKey, default);
            if (result.Success)
            {
                await outboxRepo.MarkSuccessAsync(msg.Id, default);
                
                var t = await db.WeighTickets.FindAsync(msg.AggregateId);
                if (t != null)
                {
                    t.SyncStatus = SyncStatus.SYNC_SUCCESS;
                }
            }
        }
        await uow.SaveChangesAsync(default);

        // 4. Verify
        var updatedTicket = await db.WeighTickets.AsNoTracking().FirstAsync(t => t.Id == ticket.Id);
        Assert.Equal(SyncStatus.SYNC_SUCCESS, updatedTicket.SyncStatus);

        var updatedOutbox = await db.SyncOutbox.AsNoTracking().FirstAsync(o => o.Id == outbox.Id);
        Assert.Equal(OutboxStatus.SUCCESS, updatedOutbox.Status);
    }

    public void Dispose()
    {
        _listener.Stop();
        _host.Dispose();
    }
}
