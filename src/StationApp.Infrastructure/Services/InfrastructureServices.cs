using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
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

public class WeighingSessionNumberGenerator : IWeighingSessionNumberGenerator
{
    private readonly StationDbContext _db;
    private readonly IAppConfigRepository _configRepo;
    private readonly IClock _clock;

    public WeighingSessionNumberGenerator(StationDbContext db, IAppConfigRepository configRepo, IClock clock)
    {
        _db = db;
        _configRepo = configRepo;
        _clock = clock;
    }

    public async Task<string> GenerateAsync(TransactionType transactionType, CancellationToken ct)
    {
        var now = _clock.NowLocal;
        var yearMonth = now.ToString("yyMM");
        const string sessionPrefixBase = "LC";
        var sessionPrefix = $"{sessionPrefixBase}{yearMonth}";

        var lastSessionNo = await _db.WeighingSessions
            .Where(s => s.SessionNo.StartsWith(sessionPrefix))
            .OrderByDescending(s => s.SessionNo)
            .Select(s => s.SessionNo)
            .FirstOrDefaultAsync(ct);

        var nextSeq = 1;
        if (lastSessionNo != null && lastSessionNo.Length > sessionPrefix.Length)
        {
            if (int.TryParse(lastSessionNo[sessionPrefix.Length..], out var lastSeq))
            {
                nextSeq = lastSeq + 1;
            }
        }

        return $"{sessionPrefix}{nextSeq:D4}";
    }
}


public class AppVersionProvider : IAppVersionProvider
{
    public string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return AppUpdateVersionComparer.NormalizeString(informationalVersion);
        }

        var fileVersion = assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version;
        if (!string.IsNullOrWhiteSpace(fileVersion))
        {
            return AppUpdateVersionComparer.NormalizeString(fileVersion);
        }

        return AppUpdateVersionComparer.NormalizeString(assembly.GetName().Version?.ToString());
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

    public async Task<decimal> GetToleranceKgPerBagAsync(CancellationToken ct)
    {
        var val = await _configRepo.GetValueAsync(AppConfigKeys.ToleranceKgPerBag, ct);
        return decimal.TryParse(val, out var result) ? result : AppConfigDefaults.DefaultToleranceKgPerBag;
    }
}

public class CameraSettingsProvider : ICameraSettingsProvider
{
    private readonly IAppConfigRepository _configRepo;

    public CameraSettingsProvider(IAppConfigRepository configRepo)
    {
        _configRepo = configRepo;
    }

    public Task<CameraSystemSettings> GetAsync(CancellationToken ct)
    {
        return GetForStationAsync("C2", ct);
    }

    public async Task<CameraSystemSettings> GetForStationAsync(string stationCode, CancellationToken ct)
    {
        bool isC6 = string.Equals(stationCode, "C6", StringComparison.OrdinalIgnoreCase);

        var c1EnabledKey = isC6 ? AppConfigKeys.CameraC6_1Enabled : AppConfigKeys.Camera1Enabled;
        var c1NameKey = isC6 ? AppConfigKeys.CameraC6_1Name : AppConfigKeys.Camera1Name;
        var c1RtspKey = isC6 ? AppConfigKeys.CameraC6_1RtspUrl : AppConfigKeys.Camera1RtspUrl;
        var c1PrevKey = isC6 ? AppConfigKeys.CameraC6_1PreviewRtspUrl : AppConfigKeys.Camera1PreviewRtspUrl;

        var c1EnabledDef = isC6 ? AppConfigDefaults.DefaultCameraC6_1Enabled : AppConfigDefaults.DefaultCamera1Enabled;
        var c1NameDef = isC6 ? AppConfigDefaults.DefaultCameraC6_1Name : AppConfigDefaults.DefaultCamera1Name;
        var c1RtspDef = isC6 ? AppConfigDefaults.DefaultCameraC6_1RtspUrl : AppConfigDefaults.DefaultCamera1RtspUrl;
        var c1PrevDef = isC6 ? AppConfigDefaults.DefaultCameraC6_1PreviewRtspUrl : AppConfigDefaults.DefaultCamera1PreviewRtspUrl;

        var c2EnabledKey = isC6 ? AppConfigKeys.CameraC6_2Enabled : AppConfigKeys.Camera2Enabled;
        var c2NameKey = isC6 ? AppConfigKeys.CameraC6_2Name : AppConfigKeys.Camera2Name;
        var c2RtspKey = isC6 ? AppConfigKeys.CameraC6_2RtspUrl : AppConfigKeys.Camera2RtspUrl;
        var c2PrevKey = isC6 ? AppConfigKeys.CameraC6_2PreviewRtspUrl : AppConfigKeys.Camera2PreviewRtspUrl;

        var c2EnabledDef = isC6 ? AppConfigDefaults.DefaultCameraC6_2Enabled : AppConfigDefaults.DefaultCamera2Enabled;
        var c2NameDef = isC6 ? AppConfigDefaults.DefaultCameraC6_2Name : AppConfigDefaults.DefaultCamera2Name;
        var c2RtspDef = isC6 ? AppConfigDefaults.DefaultCameraC6_2RtspUrl : AppConfigDefaults.DefaultCamera2RtspUrl;
        var c2PrevDef = isC6 ? AppConfigDefaults.DefaultCameraC6_2PreviewRtspUrl : AppConfigDefaults.DefaultCamera2PreviewRtspUrl;

        var camera1Enabled = ParseBool(await _configRepo.GetValueAsync(c1EnabledKey, ct), c1EnabledDef);
        var camera1Name = await _configRepo.GetValueAsync(c1NameKey, ct) ?? c1NameDef;
        var camera1Rtsp = await _configRepo.GetValueAsync(c1RtspKey, ct) ?? c1RtspDef;
        var camera1PreviewRtsp = await _configRepo.GetValueAsync(c1PrevKey, ct) ?? c1PrevDef;

        var camera2Enabled = ParseBool(await _configRepo.GetValueAsync(c2EnabledKey, ct), c2EnabledDef);
        var camera2Name = await _configRepo.GetValueAsync(c2NameKey, ct) ?? c2NameDef;
        var camera2Rtsp = await _configRepo.GetValueAsync(c2RtspKey, ct) ?? c2RtspDef;
        var camera2PreviewRtsp = await _configRepo.GetValueAsync(c2PrevKey, ct) ?? c2PrevDef;

        var previewDefault = await _configRepo.GetValueAsync(AppConfigKeys.CameraPreviewDefault, ct) ?? AppConfigDefaults.DefaultCameraPreview;
        var timeoutMs = ParseInt(await _configRepo.GetValueAsync(AppConfigKeys.CameraCaptureTimeoutMs, ct), AppConfigDefaults.DefaultCameraCaptureTimeoutMs);
        var jpegQuality = ParseInt(await _configRepo.GetValueAsync(AppConfigKeys.CameraCaptureJpegQuality, ct), AppConfigDefaults.DefaultCameraCaptureJpegQuality);
        var warmupFrames = ParseInt(await _configRepo.GetValueAsync(AppConfigKeys.CameraCaptureWarmupFrames, ct), AppConfigDefaults.DefaultCameraCaptureWarmupFrames);

        return new CameraSystemSettings(
            new CameraEndpointSettings("CAM1", camera1Name.Trim(), camera1Rtsp.Trim(), camera1PreviewRtsp.Trim(), camera1Enabled),
            new CameraEndpointSettings("CAM2", camera2Name.Trim(), camera2Rtsp.Trim(), camera2PreviewRtsp.Trim(), camera2Enabled),
            string.IsNullOrWhiteSpace(previewDefault) ? AppConfigDefaults.DefaultCameraPreview : previewDefault.Trim().ToUpperInvariant(),
            Math.Clamp(timeoutMs, 500, 15000),
            Math.Clamp(jpegQuality, 40, 100),
            Math.Clamp(warmupFrames, 0, 30));
    }

    private static bool ParseBool(string? raw, string fallback)
    {
        if (bool.TryParse(raw, out var result))
        {
            return result;
        }

        return bool.TryParse(fallback, out result) && result;
    }

    private static int ParseInt(string? raw, string fallback)
    {
        if (int.TryParse(raw, out var result))
        {
            return result;
        }

        return int.TryParse(fallback, out result) ? result : 0;
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

    public string CreatePayload(CutOrder registration)
        => JsonSerializer.Serialize(registration, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(DeliveryTicket ticket)
        => JsonSerializer.Serialize(ticket, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(Vehicle vehicle)
        => JsonSerializer.Serialize(vehicle, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(Customer customer)
        => JsonSerializer.Serialize(customer, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(Product product)
        => JsonSerializer.Serialize(product, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}

