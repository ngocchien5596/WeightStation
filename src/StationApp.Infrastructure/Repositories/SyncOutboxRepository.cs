using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public class SyncOutboxRepository : ISyncOutboxRepository
{
    private readonly StationDbContext _db;
    private readonly IClock _clock;

    public SyncOutboxRepository(StationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task EnqueueAsync(SyncOutbox message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.StationCode))
        {
            message.StationCode = await ResolveAggregateStationCodeAsync(message, ct)
                ?? await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        }

        message.PayloadJson = EnsurePayloadStationCode(message.PayloadJson, message.StationCode);
        await _db.SyncOutbox.AddAsync(message, ct);
    }

    public async Task<IReadOnlyList<SyncOutbox>> GetPendingAsync(DateTime now, int batchSize, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        return await _db.SyncOutbox
            .Where(m => m.StationCode == stationCode)
            .Where(m => m.Status == OutboxStatus.PENDING ||
                        (m.Status == OutboxStatus.FAILED_RETRYABLE && m.NextRetryAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<SyncOutbox?> GetLatestByAggregateAsync(Guid aggregateId, string aggregateType, CancellationToken ct)
        => await _db.SyncOutbox
            .Where(m => m.AggregateId == aggregateId && m.AggregateType == aggregateType)
            .OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<int> ForceRetryNowAsync(DateTime now, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_db, ct);
        var messages = await _db.SyncOutbox
            .Where(m => m.StationCode == stationCode)
            .Where(m => m.Status == OutboxStatus.PENDING
                || m.Status == OutboxStatus.FAILED_RETRYABLE)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            message.NextRetryAt = now;
            message.UpdatedAt = now;
        }

        return messages.Count;
    }

    public async Task MarkProcessingAsync(Guid id, CancellationToken ct)
    {
        var msg = await _db.SyncOutbox.FindAsync(new object[] { id }, ct);
        if (msg != null) { msg.Status = OutboxStatus.PROCESSING; msg.UpdatedAt = _clock.NowLocal; }
    }

    public async Task MarkSuccessAsync(Guid id, CancellationToken ct)
    {
        var msg = await _db.SyncOutbox.FindAsync(new object[] { id }, ct);
        if (msg != null) { msg.Status = OutboxStatus.SUCCESS; msg.UpdatedAt = _clock.NowLocal; }
    }

    public async Task MarkFailedRetryableAsync(Guid id, string error, DateTime nextRetryAt, CancellationToken ct)
    {
        var msg = await _db.SyncOutbox.FindAsync(new object[] { id }, ct);
        if (msg != null)
        {
            msg.Status = OutboxStatus.FAILED_RETRYABLE;
            msg.LastError = error;
            msg.RetryCount++;
            msg.NextRetryAt = nextRetryAt;
            msg.UpdatedAt = _clock.NowLocal;
        }
    }

    public async Task MarkFailedFinalAsync(Guid id, string error, CancellationToken ct)
    {
        var msg = await _db.SyncOutbox.FindAsync(new object[] { id }, ct);
        if (msg != null)
        {
            msg.Status = OutboxStatus.FAILED_FINAL;
            msg.LastError = error;
            msg.UpdatedAt = _clock.NowLocal;
        }
    }

    private async Task<string?> ResolveAggregateStationCodeAsync(SyncOutbox message, CancellationToken ct)
    {
        return message.AggregateType switch
        {
            SyncAggregateTypes.CutOrder => await _db.CutOrders.AsNoTracking()
                .Where(x => x.Id == message.AggregateId)
                .Select(x => x.StationCode)
                .FirstOrDefaultAsync(ct),
            SyncAggregateTypes.WeighTicket => await _db.WeighTickets.AsNoTracking()
                .Where(x => x.Id == message.AggregateId)
                .Select(x => x.StationCode)
                .FirstOrDefaultAsync(ct),
            SyncAggregateTypes.DeliveryTicket => await _db.DeliveryTickets.AsNoTracking()
                .Where(x => x.Id == message.AggregateId)
                .Select(x => x.StationCode)
                .FirstOrDefaultAsync(ct),
            SyncAggregateTypes.WeighingSession => await _db.WeighingSessions.AsNoTracking()
                .Where(x => x.Id == message.AggregateId)
                .Select(x => x.StationCode)
                .FirstOrDefaultAsync(ct),
            SyncAggregateTypes.WeighingSessionLine => await _db.WeighingSessionLines.AsNoTracking()
                .Where(x => x.Id == message.AggregateId)
                .Select(x => x.StationCode)
                .FirstOrDefaultAsync(ct),
            _ => null
        };
    }

    private static string EnsurePayloadStationCode(string payloadJson, string stationCode)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || string.IsNullOrWhiteSpace(stationCode))
        {
            return payloadJson;
        }

        try
        {
            var node = JsonNode.Parse(payloadJson) as JsonObject;
            if (node is null || !node.ContainsKey("stationCode"))
            {
                return payloadJson;
            }

            var current = node["stationCode"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(current))
            {
                return payloadJson;
            }

            node["stationCode"] = stationCode;
            return node.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch (JsonException)
        {
            return payloadJson;
        }
    }
}
