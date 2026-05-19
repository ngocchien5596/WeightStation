using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
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
                        var intervalStr = await appRepo.GetValueAsync("sync_interval", stoppingToken)
                            ?? await appRepo.GetValueAsync("sync_outbox_interval_seconds", stoppingToken);
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
        var registrationRepo = scope.ServiceProvider.GetRequiredService<ICutOrderRepository>();
        var weighTicketRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryTicketRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var payloadFactory = scope.ServiceProvider.GetRequiredService<ISyncPayloadFactory>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var apiClient = scope.ServiceProvider.GetRequiredService<ICentralApiClient>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.NowLocal;
        await EnsureQueuedAggregateMessagesAsync(
            outboxRepo,
            registrationRepo,
            weighTicketRepo,
            deliveryTicketRepo,
            payloadFactory,
            uow,
            now,
            ct);

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

                var result = await apiClient.PushAggregateAsync(message.AggregateType, message.PayloadJson, message.IdempotencyKey, ct);
                if (result.Success)
                {
                    await outboxRepo.MarkSuccessAsync(message.Id, ct);
                    await MarkAggregateSyncSuccessAsync(
                        message,
                        now,
                        registrationRepo,
                        weighTicketRepo,
                        deliveryTicketRepo,
                        ct);
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

                    await MarkAggregateSyncFailedAsync(
                        message,
                        now,
                        result.ErrorMessage ?? "API error",
                        registrationRepo,
                        weighTicketRepo,
                        deliveryTicketRepo,
                        ct);
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

    private static async Task EnsureQueuedAggregateMessagesAsync(
        ISyncOutboxRepository outboxRepo,
        ICutOrderRepository registrationRepo,
        IWeighTicketRepository weighTicketRepo,
        IDeliveryTicketRepository deliveryTicketRepo,
        ISyncPayloadFactory payloadFactory,
        IUnitOfWork uow,
        DateTime now,
        CancellationToken ct)
    {
        foreach (var registration in await registrationRepo.GetBySyncStatusAsync(SyncStatus.SYNC_QUEUED, 100, ct))
        {
            await EnsureOutboxMessageAsync(
                outboxRepo,
                payloadFactory.CreatePayload(registration),
                registration.Id,
                SyncAggregateTypes.CutOrder,
                registration.IdempotencyKey,
                now,
                ct);
        }

        foreach (var weighTicket in await weighTicketRepo.GetBySyncStatusAsync(SyncStatus.SYNC_QUEUED, 100, ct))
        {
            await EnsureOutboxMessageAsync(
                outboxRepo,
                payloadFactory.CreatePayload(weighTicket),
                weighTicket.Id,
                SyncAggregateTypes.WeighTicket,
                weighTicket.IdempotencyKey,
                now,
                ct);
        }

        foreach (var deliveryTicket in await deliveryTicketRepo.GetBySyncStatusAsync(SyncStatus.SYNC_QUEUED, 100, ct))
        {
            await EnsureOutboxMessageAsync(
                outboxRepo,
                payloadFactory.CreatePayload(deliveryTicket),
                deliveryTicket.Id,
                SyncAggregateTypes.DeliveryTicket,
                deliveryTicket.Id,
                now,
                ct);
        }

        await uow.SaveChangesAsync(ct);
    }

    private static async Task EnsureOutboxMessageAsync(
        ISyncOutboxRepository outboxRepo,
        string payloadJson,
        Guid aggregateId,
        string aggregateType,
        Guid idempotencyKey,
        DateTime now,
        CancellationToken ct)
    {
        var latest = await outboxRepo.GetLatestByAggregateAsync(aggregateId, aggregateType, ct);
        if (latest == null || latest.Status is OutboxStatus.SUCCESS or OutboxStatus.FAILED_FINAL)
        {
            await outboxRepo.EnqueueAsync(new SyncOutbox
            {
                Id = Guid.NewGuid(),
                AggregateId = aggregateId,
                AggregateType = aggregateType,
                PayloadJson = payloadJson,
                IdempotencyKey = idempotencyKey,
                Status = OutboxStatus.PENDING,
                RetryCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
            return;
        }

        latest.PayloadJson = payloadJson;
        latest.IdempotencyKey = idempotencyKey;
        latest.UpdatedAt = now;
    }

    private static async Task MarkAggregateSyncSuccessAsync(
        SyncOutbox message,
        DateTime now,
        ICutOrderRepository registrationRepo,
        IWeighTicketRepository weighTicketRepo,
        IDeliveryTicketRepository deliveryTicketRepo,
        CancellationToken ct)
    {
        switch (message.AggregateType)
        {
            case SyncAggregateTypes.CutOrder:
            {
                var registration = await registrationRepo.GetByIdAsync(message.AggregateId, ct);
                if (registration != null)
                {
                    registration.SyncStatus = SyncStatus.SYNC_SUCCESS;
                    registration.LastSyncAttemptAt = now;
                    registration.LastSyncError = null;
                    registration.UpdatedAt = now;
                    await registrationRepo.UpdateAsync(registration, ct);
                }
                break;
            }
            case SyncAggregateTypes.WeighTicket:
            {
                var weighTicket = await weighTicketRepo.GetByIdAsync(message.AggregateId, ct);
                if (weighTicket != null)
                {
                    weighTicket.SyncStatus = SyncStatus.SYNC_SUCCESS;
                    weighTicket.UpdatedAt = now;
                    await weighTicketRepo.UpdateAsync(weighTicket, ct);
                }
                break;
            }
            case SyncAggregateTypes.DeliveryTicket:
            {
                var deliveryTicket = await deliveryTicketRepo.GetByIdAsync(message.AggregateId, ct);
                if (deliveryTicket != null)
                {
                    deliveryTicket.SyncStatus = SyncStatus.SYNC_SUCCESS;
                    deliveryTicket.UpdatedAt = now;
                    await deliveryTicketRepo.UpdateAsync(deliveryTicket, ct);
                }
                break;
            }
            case SyncAggregateTypes.Vehicle:
            case SyncAggregateTypes.Customer:
            case SyncAggregateTypes.Product:
                break;
        }
    }

    private static async Task MarkAggregateSyncFailedAsync(
        SyncOutbox message,
        DateTime now,
        string error,
        ICutOrderRepository registrationRepo,
        IWeighTicketRepository weighTicketRepo,
        IDeliveryTicketRepository deliveryTicketRepo,
        CancellationToken ct)
    {
        switch (message.AggregateType)
        {
            case SyncAggregateTypes.CutOrder:
            {
                var registration = await registrationRepo.GetByIdAsync(message.AggregateId, ct);
                if (registration != null)
                {
                    registration.SyncStatus = SyncStatus.SYNC_FAILED;
                    registration.LastSyncAttemptAt = now;
                    registration.LastSyncError = error;
                    registration.UpdatedAt = now;
                    await registrationRepo.UpdateAsync(registration, ct);
                }
                break;
            }
            case SyncAggregateTypes.WeighTicket:
            {
                var weighTicket = await weighTicketRepo.GetByIdAsync(message.AggregateId, ct);
                if (weighTicket != null)
                {
                    weighTicket.SyncStatus = SyncStatus.SYNC_FAILED;
                    weighTicket.UpdatedAt = now;
                    await weighTicketRepo.UpdateAsync(weighTicket, ct);
                }
                break;
            }
            case SyncAggregateTypes.DeliveryTicket:
            {
                var deliveryTicket = await deliveryTicketRepo.GetByIdAsync(message.AggregateId, ct);
                if (deliveryTicket != null)
                {
                    deliveryTicket.SyncStatus = SyncStatus.SYNC_FAILED;
                    deliveryTicket.UpdatedAt = now;
                    await deliveryTicketRepo.UpdateAsync(deliveryTicket, ct);
                }
                break;
            }
            case SyncAggregateTypes.Vehicle:
            case SyncAggregateTypes.Customer:
            case SyncAggregateTypes.Product:
                break;
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

