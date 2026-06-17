using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StationApp.CentralApi.Persistence;
using StationApp.Contracts.Sync;
using StationApp.Domain.Entities;

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

    public static async Task<IResult> UpsertStationAsync(
        CentralSyncDbContext db,
        SyncStationMasterDataRequest payload,
        ILogger logger,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var startedAt = Stopwatch.StartNew();
        var log = new SyncIngestionLog
        {
            Id = Guid.NewGuid(),
            StationCode = payload.StationCode,
            AggregateType = "Station",
            SourceRecordId = payload.Id,
            ReceivedAt = now,
            Status = "RECEIVED"
        };
        db.SyncIngestionLogs.Add(log);

        try
        {
            logger.LogInformation(
                "Sync request started. AggregateType={AggregateType} StationCode={StationCode} SourceRecordId={SourceRecordId} Method={Method} Path={Path} RemoteIp={RemoteIp} TraceId={TraceId}",
                log.AggregateType,
                log.StationCode ?? "-",
                payload.Id,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                httpContext.TraceIdentifier);

            if (string.IsNullOrWhiteSpace(payload.StationCode))
            {
                throw new InvalidOperationException("StationCode is required for station sync payload.");
            }

            var station = await db.Stations.FindAsync([payload.Id], ct);
            if (station == null)
            {
                station = await db.Stations.FirstOrDefaultAsync(x => x.StationCode == payload.StationCode, ct);
            }

            if (station == null)
            {
                station = new Station { Id = payload.Id };
                await db.Stations.AddAsync(station, ct);
            }

            station.StationCode = payload.StationCode;
            station.StationName = payload.StationName;
            station.IsActive = payload.IsActive;
            station.SortOrder = payload.SortOrder;
            station.CreatedAt = payload.CreatedAt;
            station.CreatedBy = payload.CreatedBy;
            station.UpdatedAt = payload.UpdatedAt;
            station.UpdatedBy = payload.UpdatedBy;

            await UpsertStationFeatureFlagsAsync(db, payload, ct);
            await UpsertStationOperationSettingsAsync(db, payload, ct);

            log.ProcessedAt = DateTime.UtcNow;
            log.Status = "UPSERTED";

            await db.SaveChangesAsync(ct);
            startedAt.Stop();
            logger.LogInformation(
                "Sync request completed. AggregateType={AggregateType} SourceRecordId={SourceRecordId} Result={Result} DurationMs={DurationMs} TraceId={TraceId}",
                log.AggregateType,
                payload.Id,
                log.Status,
                startedAt.ElapsedMilliseconds,
                httpContext.TraceIdentifier);
            return Results.Ok(new SyncWeighTicketResponse { Success = true });
        }
        catch (Exception ex)
        {
            startedAt.Stop();
            log.ProcessedAt = DateTime.UtcNow;
            log.Status = "FAILED";
            log.ErrorMessage = BuildErrorMessage(ex);

            logger.LogError(
                ex,
                "Sync request failed. AggregateType={AggregateType} SourceRecordId={SourceRecordId} DurationMs={DurationMs} TraceId={TraceId} Error={Error}",
                log.AggregateType,
                payload.Id,
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
                    log.AggregateType,
                    payload.Id,
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

    private static async Task UpsertStationFeatureFlagsAsync(
        CentralSyncDbContext db,
        SyncStationMasterDataRequest payload,
        CancellationToken ct)
    {
        var featureFlags = payload.FeatureFlags
            .Where(x => !string.IsNullOrWhiteSpace(x.FeatureKey))
            .GroupBy(x => x.FeatureKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToList();

        var existing = await db.StationFeatureFlags
            .Where(x => x.StationCode == payload.StationCode)
            .ToListAsync(ct);

        var incomingKeys = featureFlags
            .Select(x => x.FeatureKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var removed in existing.Where(x => !incomingKeys.Contains(x.FeatureKey)).ToList())
        {
            db.StationFeatureFlags.Remove(removed);
        }

        foreach (var item in featureFlags)
        {
            var current = existing.FirstOrDefault(x => string.Equals(x.FeatureKey, item.FeatureKey, StringComparison.OrdinalIgnoreCase));
            if (current == null)
            {
                await db.StationFeatureFlags.AddAsync(new StationFeatureFlag
                {
                    Id = Guid.NewGuid(),
                    StationCode = payload.StationCode,
                    FeatureKey = item.FeatureKey,
                    FeatureValue = item.FeatureValue ?? string.Empty,
                    CreatedAt = payload.CreatedAt,
                    CreatedBy = payload.CreatedBy,
                    UpdatedAt = payload.UpdatedAt,
                    UpdatedBy = payload.UpdatedBy
                }, ct);
                continue;
            }

            current.FeatureValue = item.FeatureValue ?? string.Empty;
            current.UpdatedAt = payload.UpdatedAt ?? payload.CreatedAt;
            current.UpdatedBy = payload.UpdatedBy ?? payload.CreatedBy;
        }
    }

    private static async Task UpsertStationOperationSettingsAsync(
        CentralSyncDbContext db,
        SyncStationMasterDataRequest payload,
        CancellationToken ct)
    {
        var settings = payload.OperationSettings
            .Where(x => !string.IsNullOrWhiteSpace(x.SettingKey))
            .GroupBy(x => x.SettingKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToList();

        var existing = await db.StationOperationSettings
            .Where(x => x.StationCode == payload.StationCode)
            .ToListAsync(ct);

        var incomingKeys = settings
            .Select(x => x.SettingKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var removed in existing.Where(x => !incomingKeys.Contains(x.SettingKey)).ToList())
        {
            db.StationOperationSettings.Remove(removed);
        }

        foreach (var item in settings)
        {
            var current = existing.FirstOrDefault(x => string.Equals(x.SettingKey, item.SettingKey, StringComparison.OrdinalIgnoreCase));
            if (current == null)
            {
                await db.StationOperationSettings.AddAsync(new StationOperationSetting
                {
                    Id = Guid.NewGuid(),
                    StationCode = payload.StationCode,
                    SettingKey = item.SettingKey,
                    SettingValue = item.SettingValue ?? string.Empty,
                    CreatedAt = payload.CreatedAt,
                    CreatedBy = payload.CreatedBy ?? "SYSTEM",
                    UpdatedAt = payload.UpdatedAt,
                    UpdatedBy = payload.UpdatedBy
                }, ct);
                continue;
            }

            current.SettingValue = item.SettingValue ?? string.Empty;
            current.UpdatedAt = payload.UpdatedAt ?? payload.CreatedAt;
            current.UpdatedBy = payload.UpdatedBy ?? payload.CreatedBy;
        }
    }
}
