using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Domain.Enums;

namespace StationApp.Sync.Services;

public sealed class SyncOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncOutboxWorker> _logger;
    private int _intervalSeconds = 30;

    public SyncOutboxWorker(IServiceScopeFactory scopeFactory, ILogger<SyncOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncOutboxWorker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var appRepo = scope.ServiceProvider.GetService<IAppConfigRepository>();
                    if (appRepo != null)
                    {
                        var intervalStr = await appRepo.GetValueAsync("sync_outbox_interval_seconds", stoppingToken);
                        if (int.TryParse(intervalStr, out var intervalVal) && intervalVal > 0)
                        {
                            _intervalSeconds = intervalVal;
                        }
                    }
                }
            }
            catch { /* fallback to default interval */ }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SyncOutboxWorker cycle.");
            }
            sw.Stop();
            LogPerf("Sync cycle", sw.Elapsed.TotalMilliseconds);

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }


    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<ISyncOutboxRepository>();
        var ticketRepo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var apiClient = scope.ServiceProvider.GetRequiredService<ICentralApiClient>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.NowLocal;
        var swLoad = System.Diagnostics.Stopwatch.StartNew();
        var messages = await outboxRepo.GetPendingAsync(now, 10, ct);
        swLoad.Stop();
        LogPerf("Sync - Load Pending Outbox", swLoad.Elapsed.TotalMilliseconds);

        foreach (var message in messages)
        {
            try
            {
                var swPush = System.Diagnostics.Stopwatch.StartNew();
                await outboxRepo.MarkProcessingAsync(message.Id, ct);
                await uow.SaveChangesAsync(ct);

                var result = await apiClient.PushTicketAsync(message.PayloadJson, message.IdempotencyKey, ct);
                if (result.Success)
                {
                    await outboxRepo.MarkSuccessAsync(message.Id, ct);

                    var ticket = await ticketRepo.GetByIdAsync(message.AggregateId, ct);
                    if (ticket != null)
                    {
                        ticket.SyncStatus = SyncStatus.SYNC_SUCCESS;
                        await ticketRepo.UpdateAsync(ticket, ct);
                    }
                }
                else
                {
                    if (RetryPolicyProvider.IsMaxRetryReached(message.RetryCount))
                    {
                        await outboxRepo.MarkFailedFinalAsync(message.Id, result.ErrorMessage ?? "Max retry reached", ct);
                    }
                    else
                    {
                        var nextRetry = RetryPolicyProvider.GetNextRetryAt(message.RetryCount, now);
                        await outboxRepo.MarkFailedRetryableAsync(message.Id, result.ErrorMessage ?? "API error", nextRetry, ct);
                    }

                    var failedTicket = await ticketRepo.GetByIdAsync(message.AggregateId, ct);
                    if (failedTicket != null)
                    {
                        failedTicket.SyncStatus = SyncStatus.SYNC_FAILED;
                        await ticketRepo.UpdateAsync(failedTicket, ct);
                    }
                }
                swPush.Stop();
                LogPerf("Sync - API Push Ticket", swPush.Elapsed.TotalMilliseconds);

                var swWrite = System.Diagnostics.Stopwatch.StartNew();
                await uow.SaveChangesAsync(ct);
                swWrite.Stop();
                LogPerf("Sync - DB Write-back", swWrite.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync message {Id}", message.Id);
            }
        }
    }


    private void LogPerf(string operation, double durationMs)
    {
        var entry = new
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            MachineName = Environment.MachineName,
            ThreadId = Environment.CurrentManagedThreadId,
            Operation = operation,
            DurationMs = Math.Round(durationMs, 2)
        };
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entry);
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            var filePath = Path.Combine(logDir, "perf_metrics.jsonl");
            lock (this) { File.AppendAllText(filePath, json + Environment.NewLine); }
        }
        catch { }
    }
}

