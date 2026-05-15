using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
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
}

public class AppConfigRepository : IAppConfigRepository
{
    private readonly StationDbContext _db;
    private readonly IClock _clock;

    public AppConfigRepository(StationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        var config = await _db.AppConfigs.FindAsync(new object[] { key }, ct);
        return config?.ConfigValue;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken ct)
    {
        var config = await _db.AppConfigs.FindAsync(new object[] { key }, ct);
        if (config != null) { config.ConfigValue = value; config.UpdatedAt = _clock.NowLocal; }
        else await _db.AppConfigs.AddAsync(new AppConfig { ConfigKey = key, ConfigValue = value, UpdatedAt = _clock.NowLocal }, ct);
    }
}

public class DeviceConfigRepository : IDeviceConfigRepository
{
    private readonly StationDbContext _db;
    public DeviceConfigRepository(StationDbContext db) => _db = db;

    public async Task<DeviceConfig?> GetActiveAsync(CancellationToken ct)
        => await _db.DeviceConfigs.FirstOrDefaultAsync(d => d.IsActive, ct);

    public async Task SaveAsync(DeviceConfig config, CancellationToken ct)
    {
        if (await _db.DeviceConfigs.AnyAsync(d => d.Id == config.Id, ct))
            _db.DeviceConfigs.Update(config);
        else
            await _db.DeviceConfigs.AddAsync(config, ct);
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
        _db.Users.Update(user);
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
