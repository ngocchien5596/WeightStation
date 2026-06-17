using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Contracts.Sync;
using StationApp.Application.Security;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Services;

public static class StationRuntimeScope
{
    private static readonly object Gate = new();
    private static string? _stationCode;
    private static string? _stationName;

    public static string? StationCode
    {
        get
        {
            lock (Gate)
            {
                return _stationCode;
            }
        }
    }

    public static void Set(string stationCode, string stationName)
    {
        lock (Gate)
        {
            _stationCode = stationCode.Trim();
            _stationName = stationName.Trim();
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _stationCode = null;
            _stationName = null;
        }
    }
}

public sealed class CurrentStationContext : ICurrentStationContext
{
    public string? StationCode { get; private set; }
    public string? StationName { get; private set; }
    public bool HasStation => !string.IsNullOrWhiteSpace(StationCode);

    public void SetStation(string stationCode, string stationName)
    {
        StationCode = stationCode.Trim();
        StationName = stationName.Trim();
        StationRuntimeScope.Set(StationCode, StationName);
    }

    public void Clear()
    {
        StationCode = null;
        StationName = null;
        StationRuntimeScope.Clear();
    }
}

public sealed class StationScope : IStationScope
{
    private readonly StationDbContext _db;

    public StationScope(StationDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetCurrentStationCodeAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(StationRuntimeScope.StationCode))
        {
            return StationRuntimeScope.StationCode!;
        }

        var defaultStation = await ResolveDefaultStationCodeAsync(ct);
        return defaultStation;
    }

    private async Task<string> ResolveDefaultStationCodeAsync(CancellationToken ct)
    {
        var defaultStation = await _db.AppConfigs.AsNoTracking()
            .Where(x => x.ConfigKey == AppConfigKeys.DefaultStationCode)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(defaultStation))
        {
            return defaultStation.Trim();
        }

        var stationCode = await _db.AppConfigs.AsNoTracking()
            .Where(x => x.ConfigKey == AppConfigKeys.StationCode)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(stationCode) ? "QN01" : stationCode.Trim();
    }
}

public sealed class StationAuthorizationService : IStationAuthorizationService
{
    private readonly StationDbContext _db;

    public StationAuthorizationService(StationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<StationOptionDto>> GetAllowedStationsAsync(Guid userId, CancellationToken ct)
    {
        var stations = await (
                from assignment in _db.UserStationAssignments.AsNoTracking()
                join station in _db.Stations.AsNoTracking()
                    on assignment.StationCode equals station.StationCode
                where assignment.UserId == userId
                    && assignment.IsActive
                    && station.IsActive
                orderby assignment.IsDefault descending, station.SortOrder, station.StationCode
                select new StationOptionDto(station.StationCode, station.StationName, assignment.IsDefault))
            .ToListAsync(ct);

        return stations;
    }

    public async Task<bool> CanAccessStationAsync(Guid userId, string stationCode, CancellationToken ct)
    {
        var normalized = stationCode.Trim();
        return await _db.UserStationAssignments.AsNoTracking()
            .AnyAsync(x => x.UserId == userId
                && x.StationCode == normalized
                && x.IsActive, ct);
    }

    public async Task EnsureCanAccessStationAsync(Guid userId, string stationCode, CancellationToken ct)
    {
        if (!await CanAccessStationAsync(userId, stationCode, ct))
        {
            throw new InvalidOperationException("Tài khoản chưa được phân quyền trạm cân này.");
        }
    }
}

public sealed class StationFeatureService : IStationFeatureService
{
    private readonly StationDbContext _db;

    public StationFeatureService(StationDbContext db)
    {
        _db = db;
    }

    public async Task<StationFeatureSetDto> GetFeaturesAsync(string stationCode, CancellationToken ct)
    {
        var values = await _db.StationFeatureFlags.AsNoTracking()
            .Where(x => x.StationCode == stationCode)
            .ToDictionaryAsync(x => x.FeatureKey, x => x.FeatureValue, StringComparer.OrdinalIgnoreCase, ct);

        var defaults = StationFeatureSetDto.Defaults;
        return new StationFeatureSetDto(
            Bool(values, StationFeatureKeys.ShowMenuDashboard, defaults.ShowMenuDashboard),
            Bool(values, StationFeatureKeys.ShowMenuIncomingVehicleList, defaults.ShowMenuIncomingVehicleList),
            Bool(values, StationFeatureKeys.ShowMenuWeighing, defaults.ShowMenuWeighing),
            Bool(values, StationFeatureKeys.ShowMenuCrusherWeighing, defaults.ShowMenuCrusherWeighing),
            Bool(values, StationFeatureKeys.ShowMenuClayWeighing, defaults.ShowMenuClayWeighing),
            Bool(values, StationFeatureKeys.ShowMenuExportWeighing, defaults.ShowMenuExportWeighing),
            Bool(values, StationFeatureKeys.ShowMenuOutgoingVehicleList, defaults.ShowMenuOutgoingVehicleList),
            Bool(values, StationFeatureKeys.ShowMenuExportReport, defaults.ShowMenuExportReport),
            Bool(values, StationFeatureKeys.ShowMenuInboundReport, defaults.ShowMenuInboundReport),
            Bool(values, StationFeatureKeys.ShowMenuCrusherInboundReport, defaults.ShowMenuCrusherInboundReport),
            Bool(values, StationFeatureKeys.ShowMenuClayInboundReport, defaults.ShowMenuClayInboundReport),
            Bool(values, StationFeatureKeys.ShowDashboardInboundKpi, defaults.ShowDashboardInboundKpi),
            Bool(values, StationFeatureKeys.ShowDashboardOutboundKpi, defaults.ShowDashboardOutboundKpi),
            Text(values, StationFeatureKeys.DefaultNavigationTarget, defaults.DefaultNavigationTarget));
    }

    public async Task<bool> IsEnabledAsync(string stationCode, string featureKey, CancellationToken ct)
    {
        var features = await GetFeaturesAsync(stationCode, ct);
        return featureKey switch
        {
            StationFeatureKeys.ShowMenuDashboard => features.ShowMenuDashboard,
            StationFeatureKeys.ShowMenuIncomingVehicleList => features.ShowMenuIncomingVehicleList,
            StationFeatureKeys.ShowMenuWeighing => features.ShowMenuWeighing,
            StationFeatureKeys.ShowMenuCrusherWeighing => features.ShowMenuCrusherWeighing,
            StationFeatureKeys.ShowMenuClayWeighing => features.ShowMenuClayWeighing,
            StationFeatureKeys.ShowMenuExportWeighing => features.ShowMenuExportWeighing,
            StationFeatureKeys.ShowMenuOutgoingVehicleList => features.ShowMenuOutgoingVehicleList,
            StationFeatureKeys.ShowMenuExportReport => features.ShowMenuExportReport,
            StationFeatureKeys.ShowMenuInboundReport => features.ShowMenuInboundReport,
            StationFeatureKeys.ShowMenuCrusherInboundReport => features.ShowMenuCrusherInboundReport,
            StationFeatureKeys.ShowMenuClayInboundReport => features.ShowMenuClayInboundReport,
            StationFeatureKeys.ShowDashboardInboundKpi => features.ShowDashboardInboundKpi,
            StationFeatureKeys.ShowDashboardOutboundKpi => features.ShowDashboardOutboundKpi,
            _ => true
        };
    }

    private static bool Bool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
    {
        return values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string Text(IReadOnlyDictionary<string, string> values, string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : defaultValue;
    }
}

public sealed class StationAdministrationService : IStationAdministrationService
{
    private readonly StationDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;
    private readonly IAuditService _auditService;
    private readonly IStationOperationSettingsRepository _operationSettings;
    private readonly ISyncOutboxRepository _syncOutbox;
    private readonly ISyncPayloadFactory _syncPayloadFactory;
    private readonly IStationScope _stationScope;

    public StationAdministrationService(
        StationDbContext db,
        ICurrentUserContext currentUser,
        IClock clock,
        IAuditService auditService,
        IStationOperationSettingsRepository operationSettings,
        ISyncOutboxRepository syncOutbox,
        ISyncPayloadFactory syncPayloadFactory,
        IStationScope stationScope)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _auditService = auditService;
        _operationSettings = operationSettings;
        _syncOutbox = syncOutbox;
        _syncPayloadFactory = syncPayloadFactory;
        _stationScope = stationScope;
    }

    public async Task<IReadOnlyList<StationManagementDto>> SearchStationsAsync(
        string? stationCode,
        string? stationName,
        bool? isActive,
        CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "manage stations");

        var query = _db.Stations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(stationCode))
        {
            var keyword = stationCode.Trim();
            query = query.Where(x => x.StationCode.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(stationName))
        {
            var keyword = stationName.Trim();
            query = query.Where(x => x.StationName.Contains(keyword));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var stations = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.StationCode)
            .ToListAsync(ct);

        var stationCodes = stations.Select(x => x.StationCode).ToList();
        var featuresByStation = await LoadFeatureSetsAsync(stationCodes, ct);
        var settingsByStation = await LoadOperationSettingsSetsAsync(stationCodes, ct);

        return stations
            .Select(x => new StationManagementDto(
                x.Id,
                x.StationCode,
                x.StationName,
                x.IsActive,
                x.SortOrder,
                featuresByStation.TryGetValue(x.StationCode, out var features) ? features : StationFeatureSetDto.Defaults,
                settingsByStation.TryGetValue(x.StationCode, out var settings) ? settings : StationOperationSettingsDto.Defaults,
                x.CreatedAt,
                x.CreatedBy,
                x.UpdatedAt,
                x.UpdatedBy))
            .ToList();
    }

    public async Task<StationManagementDto> SaveStationAsync(SaveStationRequest request, CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "manage stations");

        var stationCode = request.StationCode.Trim().ToUpperInvariant();
        var stationName = request.StationName.Trim();

        if (string.IsNullOrWhiteSpace(stationCode))
        {
            throw new InvalidOperationException("Mã trạm là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(stationName))
        {
            throw new InvalidOperationException("Tên trạm là bắt buộc.");
        }

        var now = _clock.NowLocal;
        var actor = _currentUser.Username;
        Station station;

        if (request.StationId.HasValue)
        {
            station = await _db.Stations.FirstOrDefaultAsync(x => x.Id == request.StationId.Value, ct)
                ?? throw new InvalidOperationException("Không tìm thấy trạm.");

            if (!string.Equals(station.StationCode, stationCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Không cho phép đổi mã trạm sau khi đã tạo.");
            }

            station.StationName = stationName;
            station.IsActive = request.IsActive;
            station.SortOrder = request.SortOrder;
            station.UpdatedAt = now;
            station.UpdatedBy = actor;
        }
        else
        {
            if (await _db.Stations.AnyAsync(x => x.StationCode == stationCode, ct))
            {
                throw new InvalidOperationException("Mã trạm đã tồn tại.");
            }

            station = new Station
            {
                Id = Guid.NewGuid(),
                StationCode = stationCode,
                StationName = stationName,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = now,
                CreatedBy = actor
            };
            await _db.Stations.AddAsync(station, ct);
        }

        await UpsertFeatureFlagsAsync(station.StationCode, request.Features, now, actor, ct);

        var settingsValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StationOperationSettingKeys.CrusherSingleWeighEnabled] = request.Settings.CrusherSingleWeighEnabled.ToString().ToLowerInvariant(),
            [StationOperationSettingKeys.CrusherDefaultWeighMode] = request.Settings.CrusherDefaultWeighMode,
            [StationOperationSettingKeys.CrusherDefaultProductCode] = request.Settings.CrusherDefaultProductCode ?? "",
            [StationOperationSettingKeys.CrusherDefaultCustomerCode] = request.Settings.CrusherDefaultCustomerCode ?? "",
            [ClayStationOperationSettingKeys.ClaySingleWeighEnabled] = request.Settings.ClaySingleWeighEnabled.ToString().ToLowerInvariant(),
            [ClayStationOperationSettingKeys.ClayDefaultWeighMode] = request.Settings.ClayDefaultWeighMode,
            [ClayStationOperationSettingKeys.ClayDefaultProductCode] = request.Settings.ClayDefaultProductCode ?? "",
            [ClayStationOperationSettingKeys.ClayDefaultCustomerCode] = request.Settings.ClayDefaultCustomerCode ?? ""
        };
        await _operationSettings.SaveSettingsAsync(station.StationCode, settingsValues, actor, ct);

        var currentRuntimeStationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var syncPayload = BuildStationSyncPayload(station, request.Features, settingsValues);
        await _syncOutbox.EnqueueAsync(new SyncOutbox
        {
            Id = Guid.NewGuid(),
            AggregateId = station.Id,
            AggregateType = SyncAggregateTypes.Station,
            StationCode = currentRuntimeStationCode,
            PayloadJson = _syncPayloadFactory.CreatePayload(syncPayload),
            IdempotencyKey = station.Id,
            Status = OutboxStatus.PENDING,
            RetryCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync(
            request.StationId.HasValue ? "UPDATE_STATION" : "CREATE_STATION",
            nameof(Station),
            station.Id,
            new { station.StationCode, station.StationName, station.IsActive, station.SortOrder },
            ct);

        return new StationManagementDto(
            station.Id,
            station.StationCode,
            station.StationName,
            station.IsActive,
            station.SortOrder,
            request.Features,
            request.Settings,
            station.CreatedAt,
            station.CreatedBy,
            station.UpdatedAt,
            station.UpdatedBy);
    }

    private static SyncStationMasterDataRequest BuildStationSyncPayload(
        Station station,
        StationFeatureSetDto features,
        IReadOnlyDictionary<string, string> settingsValues)
    {
        return new SyncStationMasterDataRequest
        {
            Id = station.Id,
            StationCode = station.StationCode,
            StationName = station.StationName,
            IsActive = station.IsActive,
            SortOrder = station.SortOrder,
            CreatedAt = station.CreatedAt,
            CreatedBy = station.CreatedBy,
            UpdatedAt = station.UpdatedAt,
            UpdatedBy = station.UpdatedBy,
            FeatureFlags =
            [
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuDashboard, FeatureValue = features.ShowMenuDashboard.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuIncomingVehicleList, FeatureValue = features.ShowMenuIncomingVehicleList.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuWeighing, FeatureValue = features.ShowMenuWeighing.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuCrusherWeighing, FeatureValue = features.ShowMenuCrusherWeighing.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuClayWeighing, FeatureValue = features.ShowMenuClayWeighing.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuExportWeighing, FeatureValue = features.ShowMenuExportWeighing.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuOutgoingVehicleList, FeatureValue = features.ShowMenuOutgoingVehicleList.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuExportReport, FeatureValue = features.ShowMenuExportReport.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuInboundReport, FeatureValue = features.ShowMenuInboundReport.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuCrusherInboundReport, FeatureValue = features.ShowMenuCrusherInboundReport.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowMenuClayInboundReport, FeatureValue = features.ShowMenuClayInboundReport.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowDashboardInboundKpi, FeatureValue = features.ShowDashboardInboundKpi.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.ShowDashboardOutboundKpi, FeatureValue = features.ShowDashboardOutboundKpi.ToString().ToLowerInvariant() },
                new SyncStationFeatureFlagItem { FeatureKey = StationFeatureKeys.DefaultNavigationTarget, FeatureValue = features.DefaultNavigationTarget ?? string.Empty }
            ],
            OperationSettings = settingsValues
                .Select(x => new SyncStationOperationSettingItem
                {
                    SettingKey = x.Key,
                    SettingValue = x.Value
                })
                .ToList()
        };
    }

    private async Task<Dictionary<string, StationFeatureSetDto>> LoadFeatureSetsAsync(IReadOnlyCollection<string> stationCodes, CancellationToken ct)
    {
        if (stationCodes.Count == 0)
        {
            return new Dictionary<string, StationFeatureSetDto>(StringComparer.OrdinalIgnoreCase);
        }

        var flags = await _db.StationFeatureFlags.AsNoTracking()
            .Where(x => stationCodes.Contains(x.StationCode))
            .ToListAsync(ct);

        return flags
            .GroupBy(x => x.StationCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var values = x.ToDictionary(f => f.FeatureKey, f => f.FeatureValue, StringComparer.OrdinalIgnoreCase);
                    var defaults = StationFeatureSetDto.Defaults;
                    return new StationFeatureSetDto(
                        Bool(values, StationFeatureKeys.ShowMenuDashboard, defaults.ShowMenuDashboard),
                        Bool(values, StationFeatureKeys.ShowMenuIncomingVehicleList, defaults.ShowMenuIncomingVehicleList),
                        Bool(values, StationFeatureKeys.ShowMenuWeighing, defaults.ShowMenuWeighing),
                        Bool(values, StationFeatureKeys.ShowMenuCrusherWeighing, defaults.ShowMenuCrusherWeighing),
                        Bool(values, StationFeatureKeys.ShowMenuClayWeighing, defaults.ShowMenuClayWeighing),
                        Bool(values, StationFeatureKeys.ShowMenuExportWeighing, defaults.ShowMenuExportWeighing),
                        Bool(values, StationFeatureKeys.ShowMenuOutgoingVehicleList, defaults.ShowMenuOutgoingVehicleList),
                        Bool(values, StationFeatureKeys.ShowMenuExportReport, defaults.ShowMenuExportReport),
                        Bool(values, StationFeatureKeys.ShowMenuInboundReport, defaults.ShowMenuInboundReport),
                        Bool(values, StationFeatureKeys.ShowMenuCrusherInboundReport, defaults.ShowMenuCrusherInboundReport),
                        Bool(values, StationFeatureKeys.ShowMenuClayInboundReport, defaults.ShowMenuClayInboundReport),
                        Bool(values, StationFeatureKeys.ShowDashboardInboundKpi, defaults.ShowDashboardInboundKpi),
                        Bool(values, StationFeatureKeys.ShowDashboardOutboundKpi, defaults.ShowDashboardOutboundKpi),
                        Text(values, StationFeatureKeys.DefaultNavigationTarget, defaults.DefaultNavigationTarget));
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, StationOperationSettingsDto>> LoadOperationSettingsSetsAsync(IReadOnlyCollection<string> stationCodes, CancellationToken ct)
    {
        if (stationCodes.Count == 0)
        {
            return new Dictionary<string, StationOperationSettingsDto>(StringComparer.OrdinalIgnoreCase);
        }

        var settings = await _db.StationOperationSettings.AsNoTracking()
            .Where(x => stationCodes.Contains(x.StationCode))
            .ToListAsync(ct);

        return settings
            .GroupBy(x => x.StationCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var values = x.ToDictionary(s => s.SettingKey, s => s.SettingValue, StringComparer.OrdinalIgnoreCase);
                    var defaults = StationOperationSettingsDto.Defaults;
                    return new StationOperationSettingsDto(
                        Bool(values, StationOperationSettingKeys.CrusherSingleWeighEnabled, defaults.CrusherSingleWeighEnabled),
                        Text(values, StationOperationSettingKeys.CrusherDefaultWeighMode, defaults.CrusherDefaultWeighMode),
                        Text(values, StationOperationSettingKeys.CrusherDefaultProductCode, defaults.CrusherDefaultProductCode),
                        Text(values, StationOperationSettingKeys.CrusherDefaultCustomerCode, defaults.CrusherDefaultCustomerCode),
                        Bool(values, ClayStationOperationSettingKeys.ClaySingleWeighEnabled, defaults.ClaySingleWeighEnabled),
                        Text(values, ClayStationOperationSettingKeys.ClayDefaultWeighMode, defaults.ClayDefaultWeighMode),
                        Text(values, ClayStationOperationSettingKeys.ClayDefaultProductCode, defaults.ClayDefaultProductCode),
                        Text(values, ClayStationOperationSettingKeys.ClayDefaultCustomerCode, defaults.ClayDefaultCustomerCode));
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task UpsertFeatureFlagsAsync(
        string stationCode,
        StationFeatureSetDto features,
        DateTime now,
        string actor,
        CancellationToken ct)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StationFeatureKeys.ShowMenuDashboard] = features.ShowMenuDashboard.ToString(),
            [StationFeatureKeys.ShowMenuIncomingVehicleList] = features.ShowMenuIncomingVehicleList.ToString(),
            [StationFeatureKeys.ShowMenuWeighing] = features.ShowMenuWeighing.ToString(),
            [StationFeatureKeys.ShowMenuCrusherWeighing] = features.ShowMenuCrusherWeighing.ToString(),
            [StationFeatureKeys.ShowMenuClayWeighing] = features.ShowMenuClayWeighing.ToString(),
            [StationFeatureKeys.ShowMenuExportWeighing] = features.ShowMenuExportWeighing.ToString(),
            [StationFeatureKeys.ShowMenuOutgoingVehicleList] = features.ShowMenuOutgoingVehicleList.ToString(),
            [StationFeatureKeys.ShowMenuExportReport] = features.ShowMenuExportReport.ToString(),
            [StationFeatureKeys.ShowMenuInboundReport] = features.ShowMenuInboundReport.ToString(),
            [StationFeatureKeys.ShowMenuCrusherInboundReport] = features.ShowMenuCrusherInboundReport.ToString(),
            [StationFeatureKeys.ShowMenuClayInboundReport] = features.ShowMenuClayInboundReport.ToString(),
            [StationFeatureKeys.ShowDashboardInboundKpi] = features.ShowDashboardInboundKpi.ToString(),
            [StationFeatureKeys.ShowDashboardOutboundKpi] = features.ShowDashboardOutboundKpi.ToString(),
            [StationFeatureKeys.DefaultNavigationTarget] = string.IsNullOrWhiteSpace(features.DefaultNavigationTarget)
                ? StationFeatureSetDto.Defaults.DefaultNavigationTarget
                : features.DefaultNavigationTarget.Trim()
        };

        var existing = await _db.StationFeatureFlags
            .Where(x => x.StationCode == stationCode)
            .ToListAsync(ct);

        foreach (var item in values)
        {
            var flag = existing.FirstOrDefault(x => string.Equals(x.FeatureKey, item.Key, StringComparison.OrdinalIgnoreCase));
            if (flag == null)
            {
                await _db.StationFeatureFlags.AddAsync(new StationFeatureFlag
                {
                    Id = Guid.NewGuid(),
                    StationCode = stationCode,
                    FeatureKey = item.Key,
                    FeatureValue = item.Value,
                    CreatedAt = now,
                    CreatedBy = actor
                }, ct);
            }
            else
            {
                flag.FeatureValue = item.Value;
                flag.UpdatedAt = now;
                flag.UpdatedBy = actor;
            }
        }
    }

    private static bool Bool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
    {
        return values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string Text(IReadOnlyDictionary<string, string> values, string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : defaultValue;
    }

    public async Task<IReadOnlyList<UserStationAssignmentDto>> GetAssignableStationsAsync(CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "manage user station assignments");

        return await _db.Stations.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.StationCode)
            .Select(x => new UserStationAssignmentDto(x.StationCode, x.StationName, false, false))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserStationAssignmentDto>> GetUserStationAssignmentsAsync(Guid userId, CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "manage user station assignments");

        var stations = await _db.Stations.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.StationCode)
            .Select(x => new { x.StationCode, x.StationName })
            .ToListAsync(ct);

        var assignments = await _db.UserStationAssignments.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.StationCode, StringComparer.OrdinalIgnoreCase, ct);

        return stations
            .Select(station =>
            {
                assignments.TryGetValue(station.StationCode, out var assignment);
                return new UserStationAssignmentDto(
                    station.StationCode,
                    station.StationName,
                    assignment?.IsActive == true,
                    assignment?.IsActive == true && assignment.IsDefault);
            })
            .ToList()
            .AsReadOnly();
    }

    public async Task SaveUserStationAssignmentsAsync(Guid userId, IReadOnlyList<SaveUserStationAssignmentDto> assignments, CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "manage user station assignments");

        if (!await _db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct))
        {
            throw new InvalidOperationException("Không tìm thấy tài khoản.");
        }

        var selected = assignments
            .Where(x => x.IsAssigned)
            .Select(x => x with { StationCode = x.StationCode.Trim() })
            .Where(x => !string.IsNullOrWhiteSpace(x.StationCode))
            .GroupBy(x => x.StationCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        if (selected.Count == 0)
        {
            throw new InvalidOperationException("Vui lòng gán ít nhất một trạm cho tài khoản.");
        }

        if (selected.Count(x => x.IsDefault) == 0)
        {
            selected[0] = selected[0] with { IsDefault = true };
        }
        else if (selected.Count(x => x.IsDefault) > 1)
        {
            var defaultStationCode = selected.First(x => x.IsDefault).StationCode;
            selected = selected
                .Select(x => x with { IsDefault = string.Equals(x.StationCode, defaultStationCode, StringComparison.OrdinalIgnoreCase) })
                .ToList();
        }

        var activeStationCodes = await _db.Stations.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.StationCode)
            .ToListAsync(ct);
        var activeStationSet = activeStationCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalidStation = selected.FirstOrDefault(x => !activeStationSet.Contains(x.StationCode));
        if (invalidStation != null)
        {
            throw new InvalidOperationException($"Trạm {invalidStation.StationCode} không tồn tại hoặc đã ngừng hoạt động.");
        }

        var now = _clock.NowLocal;
        var actor = _currentUser.Username;
        var existing = await _db.UserStationAssignments
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        var selectedSet = selected.Select(x => x.StationCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in existing)
        {
            var match = selected.FirstOrDefault(x => string.Equals(x.StationCode, assignment.StationCode, StringComparison.OrdinalIgnoreCase));
            assignment.IsActive = match != null;
            assignment.IsDefault = match?.IsDefault == true;
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = actor;
        }

        foreach (var assignment in selected)
        {
            if (existing.Any(x => string.Equals(x.StationCode, assignment.StationCode, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            await _db.UserStationAssignments.AddAsync(new UserStationAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StationCode = assignment.StationCode,
                IsActive = true,
                IsDefault = assignment.IsDefault,
                CreatedAt = now,
                CreatedBy = actor
            }, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync(
            "UPDATE_USER_STATION_ASSIGNMENTS",
            nameof(UserStationAssignment),
            userId,
            new { UserId = userId, Stations = selectedSet.ToArray() },
            ct);
    }

    public async Task<IReadOnlyList<ProductAutocompleteSource>> GetProductsByStationAsync(string stationCode, CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "manage stations");
        var list = await _db.Products.AsNoTracking()
            .Where(p => p.StationCode == stationCode && p.IsActive)
            .OrderBy(p => p.ProductCode)
            .Select(p => new ProductAutocompleteSource(p.ProductCode, p.ProductName, p.ProductType, "MASTER"))
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<CustomerAutocompleteSource>> GetCustomersByStationAsync(string stationCode, CancellationToken ct)
    {
        StationAuthorization.EnsureAdmin(_currentUser, "manage stations");
        var list = await _db.Customers.AsNoTracking()
            .Where(c => c.StationCode == stationCode && c.IsActive)
            .OrderBy(c => c.CustomerName)
            .Select(c => new CustomerAutocompleteSource(c.CustomerCode, c.CustomerName, "MASTER"))
            .ToListAsync(ct);
        return list.AsReadOnly();
    }
}
