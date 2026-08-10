using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly StationDbContext _db;
    public AuditLogRepository(StationDbContext db) => _db = db;

    public async Task AddAsync(AuditLog log, CancellationToken ct)
        => await _db.AuditLogs.AddAsync(log, ct);

    public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct)
        => await _db.AuditLogs.Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> SearchAsync(AuditLogSearchRequest request, CancellationToken ct)
    {
        var startDateTime = request.FromDate.Date;
        var endDateTime = request.ToDate.Date.AddDays(1).AddTicks(-1);

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.CreatedAt >= startDateTime && l.CreatedAt <= endDateTime);

        if (!string.IsNullOrWhiteSpace(request.StationCode))
        {
            query = query.Where(l => l.StationCode == request.StationCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(l => l.Action == action);
        }

        var logs = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);

        logs = FilterInMemory(logs, request.VehiclePlate);
        logs = FilterInMemory(logs, request.SessionNo);
        logs = FilterInMemory(logs, request.Keyword);

        return logs;
    }

    public async Task<IReadOnlyList<AuditLog>> SearchEditLogsAsync(
        string? vehiclePlate,
        string? sessionNo,
        DateTime fromDate,
        DateTime toDate,
        string? stationCode,
        CancellationToken ct)
    {
        var startDateTime = fromDate.Date;
        var endDateTime = toDate.Date.AddDays(1).AddTicks(-1);

        // Determine which actions to include based on station
        // QN01 (Export Weighing): export trip transfers and temporary cut-order edits
        // Other stations (QN02, QN03): weighing-session edits, returned-trip toggles, and clay vessel edits
        var validActions = stationCode == "QN01"
            ? new[] { "TRANSFER_EXPORT_TRIP", "UPDATE_TEMPORARY_EXPORT_CUT_ORDER" }
            : new[] { "EDIT_WEIGHING_SESSION", "TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP", "UPDATE_CLAY_VESSEL" };

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(l => validActions.Contains(l.Action) && l.CreatedAt >= startDateTime && l.CreatedAt <= endDateTime);

        // Filter by station code if provided
        if (!string.IsNullOrWhiteSpace(stationCode))
        {
            query = query.Where(l => l.StationCode == stationCode);
        }

        var logs = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(vehiclePlate))
        {
            var cleanPlate = vehiclePlate.Trim();
            logs = logs.Where(l => l.DetailJson != null && l.DetailJson.Contains(cleanPlate, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(sessionNo))
        {
            var cleanNo = sessionNo.Trim();
            logs = logs.Where(l => l.DetailJson != null && l.DetailJson.Contains(cleanNo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return logs;
    }

    private static List<AuditLog> FilterInMemory(IEnumerable<AuditLog> logs, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return logs.ToList();
        }

        var cleanKeyword = keyword.Trim();
        return logs.Where(l =>
                Contains(l.Actor, cleanKeyword)
                || Contains(l.Action, cleanKeyword)
                || Contains(l.EntityType, cleanKeyword)
                || Contains(l.DetailJson, cleanKeyword))
            .ToList();
    }

    private static bool Contains(string? source, string keyword)
        => !string.IsNullOrWhiteSpace(source)
           && source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}

public class AppConfigRepository : IAppConfigRepository
{
    private readonly StationDbContext _db;
    private readonly IClock _clock;

    private static readonly string MainConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    private static readonly string BackupConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StationApp");
    private static readonly string BackupConfigPath = Path.Combine(BackupConfigDir, "printersettings.json");

    public AppConfigRepository(StationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        if (IsPrinterConfigKey(key))
        {
            var localVal = GetLocalPrinterConfig(key);
            if (!string.IsNullOrWhiteSpace(localVal))
            {
                return localVal;
            }
        }

        var config = await _db.AppConfigs.FindAsync(new object[] { key }, ct);
        return config?.ConfigValue;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken ct)
    {
        if (IsPrinterConfigKey(key))
        {
            SaveLocalPrinterConfig(key, value);
        }

        var config = await _db.AppConfigs.FindAsync(new object[] { key }, ct);
        if (config != null) { config.ConfigValue = value; config.UpdatedAt = _clock.NowLocal; }
        else await _db.AppConfigs.AddAsync(new AppConfig { ConfigKey = key, ConfigValue = value, UpdatedAt = _clock.NowLocal }, ct);
    }

    private static bool IsPrinterConfigKey(string key)
    {
        return string.Equals(key, AppConfigKeys.DefaultWeighTicketPrinter, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, AppConfigKeys.DefaultDeliveryTicketPrinter, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "DefaultWeighTicketPrinter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "DefaultDeliveryTicketPrinter", StringComparison.OrdinalIgnoreCase);
    }

    private static string MapJsonPropName(string key)
    {
        if (string.Equals(key, AppConfigKeys.DefaultWeighTicketPrinter, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "DefaultWeighTicketPrinter", StringComparison.OrdinalIgnoreCase))
        {
            return "DefaultWeighTicketPrinter";
        }
        return "DefaultDeliveryTicketPrinter";
    }

    private static string? GetLocalPrinterConfig(string key)
    {
        var propName = MapJsonPropName(key);
        try
        {
            if (File.Exists(MainConfigPath))
            {
                var content = File.ReadAllText(MainConfigPath);
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty(propName, out var elem) && elem.ValueKind == JsonValueKind.String)
                {
                    var val = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
                if (doc.RootElement.TryGetProperty(key, out var elem2) && elem2.ValueKind == JsonValueKind.String)
                {
                    var val = elem2.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
        }
        catch { }

        try
        {
            if (File.Exists(BackupConfigPath))
            {
                var content = File.ReadAllText(BackupConfigPath);
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty(propName, out var elem) && elem.ValueKind == JsonValueKind.String)
                {
                    var val = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
                if (doc.RootElement.TryGetProperty(key, out var elem2) && elem2.ValueKind == JsonValueKind.String)
                {
                    var val = elem2.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
        }
        catch { }

        return null;
    }

    private static void SaveLocalPrinterConfig(string key, string value)
    {
        var propName = MapJsonPropName(key);

        try
        {
            if (File.Exists(MainConfigPath))
            {
                var content = File.ReadAllText(MainConfigPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                if (dict != null)
                {
                    dict[propName] = value;
                    var newContent = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(MainConfigPath, newContent);
                }
            }
        }
        catch { }

        try
        {
            if (!Directory.Exists(BackupConfigDir))
            {
                Directory.CreateDirectory(BackupConfigDir);
            }

            var dict = new Dictionary<string, object>();
            if (File.Exists(BackupConfigPath))
            {
                var content = File.ReadAllText(BackupConfigPath);
                dict = JsonSerializer.Deserialize<Dictionary<string, object>>(content) ?? new Dictionary<string, object>();
            }

            dict[propName] = value;
            var newContent = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BackupConfigPath, newContent);
        }
        catch { }
    }
}

public class UserRepository : IUserRepository
{
    private readonly StationDbContext _db;
    public UserRepository(StationDbContext db) => _db = db;

    public async Task AddAsync(User user, CancellationToken ct)
        => await _db.Users.AddAsync(user, ct);

    public Task UpdateAsync(User user, CancellationToken ct)
    {
        if (_db.Entry(user).State == EntityState.Detached)
        {
            _db.Users.Update(user);
        }
        return Task.CompletedTask;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Users.FindAsync(new object[] { id }, ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct)
        => await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct)
        => await _db.Users.AnyAsync(u => u.Username == username, ct);

    public async Task<int> CountActiveAdminsAsync(CancellationToken ct)
        => await _db.Users.CountAsync(
            u => u.IsActive && u.RoleCode == StationRoles.Admin,
            ct);

    public async Task<IReadOnlyList<User>> SearchAsync(
        string? username,
        string? displayName,
        string? roleCode,
        bool? isActive,
        CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(username))
        {
            query = query.Where(u => u.Username.Contains(username));
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            query = query.Where(u => u.DisplayName.Contains(displayName));
        }

        if (!string.IsNullOrWhiteSpace(roleCode))
        {
            query = query.Where(u => u.RoleCode.Contains(roleCode));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(u => u.Username)
            .ToListAsync(ct);
    }
}
