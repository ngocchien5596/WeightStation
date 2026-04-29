using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace StationApp.Infrastructure.Services;

public class TicketNumberGenerator : ITicketNumberGenerator
{
    private readonly StationDbContext _db;
    private readonly IAppConfigRepository _configRepo;
    private readonly IClock _clock;

    public TicketNumberGenerator(StationDbContext db, IAppConfigRepository configRepo, IClock clock)
    {
        _db = db; _configRepo = configRepo; _clock = clock;
    }

    public async Task<string> GenerateAsync(CancellationToken ct)
    {
        var prefix = await _configRepo.GetValueAsync("ticket_prefix", ct) ?? "QN";
        var now = _clock.NowLocal;
        var yearMonth = now.ToString("yyMM");
        var ticketPrefix = $"{prefix}{yearMonth}";

        var lastTicket = await _db.WeighTickets
            .Where(t => t.TicketNo.StartsWith(ticketPrefix))
            .OrderByDescending(t => t.TicketNo)
            .Select(t => t.TicketNo)
            .FirstOrDefaultAsync(ct);

        int nextSeq = 1;
        if (lastTicket != null && lastTicket.Length > ticketPrefix.Length)
        {
            if (int.TryParse(lastTicket[ticketPrefix.Length..], out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{ticketPrefix}{nextSeq:D4}";
    }
}

public class DeliveryNumberGenerator : IDeliveryNumberGenerator
{
    private readonly StationDbContext _db;
    private readonly IAppConfigRepository _configRepo;
    private readonly IClock _clock;

    public DeliveryNumberGenerator(StationDbContext db, IAppConfigRepository configRepo, IClock clock)
    {
        _db = db; _configRepo = configRepo; _clock = clock;
    }

    public async Task<string> GenerateAsync(CancellationToken ct)
    {
        var prefix = await _configRepo.GetValueAsync("delivery_prefix", ct) ?? "DN";
        var now = _clock.NowLocal;
        var yearMonth = now.ToString("yyMM");
        var deliveryPrefix = $"{prefix}{yearMonth}";

        var lastTicket = await _db.DeliveryTickets
            .Where(t => t.DeliveryNo.StartsWith(deliveryPrefix))
            .OrderByDescending(t => t.DeliveryNo)
            .Select(t => t.DeliveryNo)
            .FirstOrDefaultAsync(ct);

        int nextSeq = 1;
        if (lastTicket != null && lastTicket.Length > deliveryPrefix.Length)
        {
            if (int.TryParse(lastTicket[deliveryPrefix.Length..], out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{deliveryPrefix}{nextSeq:D4}";
    }
}


public class AppVersionProvider : IAppVersionProvider
{
    public string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "1.0.0";
    }
}

public class SystemClock : IClock
{
    private static readonly TimeZoneInfo VnTimeZone = GetVnTimeZone();

    private static TimeZoneInfo GetVnTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    public DateTime NowLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTimeZone);
    public DateTime TodayLocal => NowLocal.Date;
}

public class CurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string RoleCode { get; private set; } = string.Empty;
    public bool IsAuthenticated { get; private set; }

    public void SignIn(Guid userId, string username, string displayName, string roleCode)
    {
        UserId = userId;
        Username = username;
        DisplayName = displayName;
        RoleCode = roleCode;
        IsAuthenticated = true;
    }

    public void SignOut()
    {
        UserId = null;
        Username = string.Empty;
        DisplayName = string.Empty;
        RoleCode = string.Empty;
        IsAuthenticated = false;
    }
}

public class BcryptUserPasswordHasher : IUserPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}

public class ToleranceProvider : IToleranceProvider
{
    private readonly IAppConfigRepository _configRepo;
    public ToleranceProvider(IAppConfigRepository configRepo) => _configRepo = configRepo;

    public async Task<decimal> GetToleranceKgAsync(CancellationToken ct)
    {
        var val = await _configRepo.GetValueAsync("tolerance_kg", ct);
        return decimal.TryParse(val, out var result) ? result : 500m;
    }
}

public class AuditService : IAuditService
{
    private readonly StationDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly IClock _clock;

    public AuditService(StationDbContext db, ICurrentUserContext userContext, IClock clock)
    {
        _db = db; _userContext = userContext; _clock = clock;
    }

    public async Task LogAsync(string action, string entityType, Guid entityId, object? detail, CancellationToken ct)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            Actor = _userContext.Username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailJson = detail != null ? JsonSerializer.Serialize(detail) : null,
            CreatedAt = _clock.NowLocal
        };
        await _db.AuditLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }
}

public class SyncPayloadFactory : ISyncPayloadFactory
{
    public string CreatePayload(WeighTicket ticket)
        => JsonSerializer.Serialize(ticket, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(VehicleRegistration registration)
        => JsonSerializer.Serialize(registration, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
