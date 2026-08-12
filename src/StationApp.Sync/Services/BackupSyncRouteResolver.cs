using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;

namespace StationApp.Sync.Services;

internal sealed record SyncEndpointRoute(
    Uri? BaseUri,
    string? ApiKey,
    string Channel,
    string? StationCode,
    string? ErrorMessage);

internal sealed class BackupSyncRouteResolver
{
    private const string CentralChannel = "CENTRAL";
    private const string BackupChannel = "BACKUP";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public BackupSyncRouteResolver(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SyncEndpointRoute> ResolveForPayloadAsync(
        string? payloadJson,
        Uri? fallbackCentralBaseUri,
        CancellationToken ct)
    {
        var stationCode = TryExtractStationCode(payloadJson);

        using var scope = _scopeFactory.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        if (IsBackupStation(stationCode, await ReadBackupStationCodesAsync(config, ct)))
        {
            var enabled = await ReadBoolAsync(config, AppConfigKeys.BackupSyncEnabled, AppConfigDefaults.DefaultBackupSyncEnabled, ct);
            if (!enabled)
            {
                return new SyncEndpointRoute(
                    null,
                    null,
                    BackupChannel,
                    stationCode,
                    $"BackupSync is disabled for station {stationCode}.");
            }

            var backupUrl = await config.GetValueAsync(AppConfigKeys.BackupSyncApiUrl, ct);
            var backupKey = await config.GetValueAsync(AppConfigKeys.BackupSyncApiKey, ct);
            var backupUri = TryCreateBaseUri(backupUrl);
            if (backupUri == null)
            {
                return new SyncEndpointRoute(
                    null,
                    backupKey,
                    BackupChannel,
                    stationCode,
                    $"BackupSync API URL chưa được cấu hình hợp lệ cho trạm {stationCode}.");
            }

            return new SyncEndpointRoute(backupUri, backupKey, BackupChannel, stationCode, null);
        }

        var centralUrl = await config.GetValueAsync(AppConfigKeys.CentralApiUrl, ct)
            ?? await config.GetValueAsync("central_api_url", ct);
        var centralKey = await config.GetValueAsync(AppConfigKeys.CentralApiKey, ct)
            ?? await config.GetValueAsync("central_api_key", ct);
        var centralUri = TryCreateBaseUri(centralUrl) ?? EnsureTrailingSlash(fallbackCentralBaseUri);
        if (centralUri == null)
        {
            return new SyncEndpointRoute(
                null,
                centralKey,
                CentralChannel,
                stationCode,
                "Central API URL chưa được cấu hình hợp lệ.");
        }

        return new SyncEndpointRoute(centralUri, centralKey, CentralChannel, stationCode, null);
    }

    public async Task<SyncEndpointRoute> ResolveForImageAsync(
        string? stationCode,
        Uri? fallbackCentralBaseUri,
        CancellationToken ct)
    {
        var payloadJson = string.IsNullOrWhiteSpace(stationCode)
            ? null
            : $"{{\"StationCode\":\"{stationCode.Replace("\"", "\\\"")}\"}}";
        return await ResolveForPayloadAsync(payloadJson, fallbackCentralBaseUri, ct);
    }

    private static async Task<HashSet<string>> ReadBackupStationCodesAsync(IAppConfigRepository config, CancellationToken ct)
    {
        var raw = await config.GetValueAsync(AppConfigKeys.BackupSyncStationCodes, ct)
            ?? AppConfigDefaults.DefaultBackupSyncStationCodes;
        return raw.Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<bool> ReadBoolAsync(
        IAppConfigRepository config,
        string key,
        string defaultValue,
        CancellationToken ct)
    {
        var raw = await config.GetValueAsync(key, ct) ?? defaultValue;
        return bool.TryParse(raw, out var value) ? value : string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBackupStation(string? stationCode, HashSet<string> backupStations)
        => !string.IsNullOrWhiteSpace(stationCode) && backupStations.Contains(stationCode.Trim().ToUpperInvariant());

    private string? TryExtractStationCode(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (TryGetStringProperty(document.RootElement, "StationCode", out var stationCode)
                || TryGetStringProperty(document.RootElement, "stationCode", out stationCode))
            {
                return stationCode?.Trim().ToUpperInvariant();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse sync payload to resolve StationCode.");
        }

        return null;
    }

    private static bool TryGetStringProperty(JsonElement root, string propertyName, out string? value)
    {
        value = null;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        value = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        return true;
    }

    private static Uri? TryCreateBaseUri(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? EnsureTrailingSlash(uri)
            : null;
    }

    private static Uri? EnsureTrailingSlash(Uri? uri)
    {
        if (uri == null)
        {
            return null;
        }

        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri($"{uri.AbsoluteUri}/");
    }
}
