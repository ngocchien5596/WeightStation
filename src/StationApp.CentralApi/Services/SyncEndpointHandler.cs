using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StationApp.CentralApi.Persistence;
using StationApp.Contracts.Sync;

namespace StationApp.CentralApi.Services;

public static class SyncEndpointHandler
{
    public static async Task<IResult> UpsertAsync<TEntity>(
        CentralSyncDbContext db,
        string aggregateType,
        Guid sourceRecordId,
        TEntity payload,
        ILogger logger,
        HttpContext httpContext,
        CancellationToken ct) where TEntity : class
    {
        var now = DateTime.UtcNow;
        var startedAt = Stopwatch.StartNew();
        EntityEntry<TEntity>? entityEntry = null;
        var log = new SyncIngestionLog
        {
            Id = Guid.NewGuid(),
            StationCode = TryGetStationCode(payload),
            AggregateType = aggregateType,
            SourceRecordId = sourceRecordId,
            ReceivedAt = now,
            Status = "RECEIVED"
        };
        db.SyncIngestionLogs.Add(log);

        try
        {
            logger.LogInformation(
                "Sync request started. AggregateType={AggregateType} StationCode={StationCode} SourceRecordId={SourceRecordId} Method={Method} Path={Path} RemoteIp={RemoteIp} TraceId={TraceId}",
                aggregateType,
                log.StationCode ?? "-",
                sourceRecordId,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                httpContext.TraceIdentifier);

            if (RequiresStationCode(payload) && string.IsNullOrWhiteSpace(log.StationCode))
            {
                throw new InvalidOperationException("StationCode is required for business sync payload.");
            }

            var existing = await db.Set<TEntity>().FindAsync([sourceRecordId], ct);
            if (existing == null)
            {
                entityEntry = await db.Set<TEntity>().AddAsync(payload, ct);
            }
            else
            {
                entityEntry = db.Entry(existing);
                entityEntry.CurrentValues.SetValues(payload);
            }

            log.ProcessedAt = DateTime.UtcNow;
            log.Status = existing == null ? "INSERTED" : "UPDATED";

            await db.SaveChangesAsync(ct);
            startedAt.Stop();
            logger.LogInformation(
                "Sync request completed. AggregateType={AggregateType} SourceRecordId={SourceRecordId} Result={Result} DurationMs={DurationMs} TraceId={TraceId}",
                aggregateType,
                sourceRecordId,
                log.Status,
                startedAt.ElapsedMilliseconds,
                httpContext.TraceIdentifier);
            return Results.Ok(new SyncWeighTicketResponse
            {
                Success = true
            });
        }
        catch (Exception ex)
        {
            startedAt.Stop();
            if (entityEntry != null)
            {
                entityEntry.State = entityEntry.State switch
                {
                    EntityState.Added => EntityState.Detached,
                    EntityState.Modified => EntityState.Unchanged,
                    _ => entityEntry.State
                };
            }

            log.ProcessedAt = DateTime.UtcNow;
            log.Status = "FAILED";
            log.ErrorMessage = BuildErrorMessage(ex);

            logger.LogError(
                ex,
                "Sync request failed. AggregateType={AggregateType} SourceRecordId={SourceRecordId} DurationMs={DurationMs} TraceId={TraceId} Error={Error}",
                aggregateType,
                sourceRecordId,
                startedAt.ElapsedMilliseconds,
                httpContext.TraceIdentifier,
                log.ErrorMessage);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception logEx)
            {
                logger.LogError(
                    logEx,
                    "Failed to persist sync ingestion log. AggregateType={AggregateType} SourceRecordId={SourceRecordId} TraceId={TraceId}",
                    aggregateType,
                    sourceRecordId,
                    httpContext.TraceIdentifier);
            }

            return Results.BadRequest(new SyncWeighTicketResponse
            {
                Success = false,
                ErrorCode = "UPSERT_FAILED",
                ErrorMessage = log.ErrorMessage
            });
        }
    }

    private static string BuildErrorMessage(Exception ex)
    {
        var parts = new List<string> { ex.Message };
        var inner = ex.InnerException;
        var depth = 0;
        while (inner != null && depth < 2)
        {
            if (!string.IsNullOrWhiteSpace(inner.Message))
            {
                parts.Add(inner.Message);
            }

            inner = inner.InnerException;
            depth++;
        }

        return string.Join(" | ", parts.Distinct());
    }

    private static bool RequiresStationCode<TEntity>(TEntity payload)
        => payload?.GetType().GetProperty("StationCode") != null;

    private static string? TryGetStationCode<TEntity>(TEntity payload)
    {
        var property = payload?.GetType().GetProperty("StationCode");
        return property?.GetValue(payload) as string;
    }
}
