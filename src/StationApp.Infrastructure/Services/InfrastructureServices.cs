using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Contracts.Sync;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace StationApp.Infrastructure.Services;

public class TicketNumberGenerator : ITicketNumberGenerator
{
    private readonly IAppConfigRepository _configRepo;
    private readonly IDocumentCounterService _counterService;
    private readonly IClock _clock;
    private readonly IStationScope _stationScope;
    private readonly ILogger<TicketNumberGenerator> _logger;

    public TicketNumberGenerator(
        IAppConfigRepository configRepo,
        IDocumentCounterService counterService,
        IClock clock,
        IStationScope stationScope,
        ILogger<TicketNumberGenerator> logger)
    {
        _configRepo = configRepo;
        _counterService = counterService;
        _clock = clock;
        _stationScope = stationScope;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(CancellationToken ct)
    {
        var prefix = await _configRepo.GetValueAsync("ticket_prefix", ct) ?? "QN";
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var now = _clock.NowLocal;
        var yearMonth = now.ToString("yyMM");
        var ticketPrefix = $"{prefix}{yearMonth}";

        var counterKey = BusinessNumberFormatter.BuildCounterKey("WeighTicket", stationCode, ticketPrefix);
        var nextSeq = await _counterService.GetNextSequenceAsync(counterKey, ct);
        var ticketNo = BusinessNumberFormatter.PrefixWithStation(stationCode, $"{ticketPrefix}{nextSeq:D4}");

        _logger.LogInformation(
            "Generated weigh ticket number {TicketNo}. StationCode={StationCode}, Prefix={Prefix}, CounterKey={CounterKey}, Sequence={Sequence}",
            ticketNo,
            stationCode,
            ticketPrefix,
            counterKey,
            nextSeq);

        return ticketNo;
    }
}

public class DeliveryNumberGenerator : IDeliveryNumberGenerator
{
    private readonly IAppConfigRepository _configRepo;
    private readonly IDocumentCounterService _counterService;
    private readonly IClock _clock;
    private readonly IStationScope _stationScope;
    private readonly ILogger<DeliveryNumberGenerator> _logger;

    public DeliveryNumberGenerator(
        IAppConfigRepository configRepo,
        IDocumentCounterService counterService,
        IClock clock,
        IStationScope stationScope,
        ILogger<DeliveryNumberGenerator> logger)
    {
        _configRepo = configRepo;
        _counterService = counterService;
        _clock = clock;
        _stationScope = stationScope;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(CancellationToken ct)
    {
        var prefix = await _configRepo.GetValueAsync("delivery_prefix", ct) ?? "DN";
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var now = _clock.NowLocal;
        var yearMonth = now.ToString("yyMM");
        var deliveryPrefix = $"{prefix}{yearMonth}";

        var counterKey = BusinessNumberFormatter.BuildCounterKey("DeliveryTicket", stationCode, deliveryPrefix);
        var nextSeq = await _counterService.GetNextSequenceAsync(counterKey, ct);
        var deliveryNo = BusinessNumberFormatter.PrefixWithStation(stationCode, $"{deliveryPrefix}{nextSeq:D4}");

        _logger.LogInformation(
            "Generated delivery ticket number {DeliveryNo}. StationCode={StationCode}, Prefix={Prefix}, CounterKey={CounterKey}, Sequence={Sequence}",
            deliveryNo,
            stationCode,
            deliveryPrefix,
            counterKey,
            nextSeq);

        return deliveryNo;
    }
}

public class WeighingSessionNumberGenerator : IWeighingSessionNumberGenerator
{
    private readonly IDocumentCounterService _counterService;
    private readonly IClock _clock;
    private readonly IStationScope _stationScope;
    private readonly ILogger<WeighingSessionNumberGenerator> _logger;

    public WeighingSessionNumberGenerator(
        IDocumentCounterService counterService,
        IClock clock,
        IStationScope stationScope,
        ILogger<WeighingSessionNumberGenerator> logger)
    {
        _counterService = counterService;
        _clock = clock;
        _stationScope = stationScope;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(TransactionType transactionType, CancellationToken ct)
    {
        var stationCode = await _stationScope.GetCurrentStationCodeAsync(ct);
        var now = _clock.NowLocal;
        var yearMonth = now.ToString("yyMM");
        const string sessionPrefixBase = "LC";
        var sessionPrefix = $"{sessionPrefixBase}{yearMonth}";

        var counterKey = BusinessNumberFormatter.BuildCounterKey("WeighingSession", stationCode, sessionPrefix);
        var nextSeq = await _counterService.GetNextSequenceAsync(counterKey, ct);
        var sessionNo = BusinessNumberFormatter.PrefixWithStation(stationCode, $"{sessionPrefix}{nextSeq:D4}");

        _logger.LogInformation(
            "Generated weighing session number {SessionNo}. StationCode={StationCode}, Prefix={Prefix}, CounterKey={CounterKey}, Sequence={Sequence}, TransactionType={TransactionType}",
            sessionNo,
            stationCode,
            sessionPrefix,
            counterKey,
            nextSeq,
            transactionType);

        return sessionNo;
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
    public string StationCode { get; private set; } = string.Empty;
    public bool IsAuthenticated { get; private set; }

    public void SignIn(Guid userId, string username, string displayName, string roleCode, string stationCode)
    {
        UserId = userId;
        Username = username;
        DisplayName = displayName;
        RoleCode = roleCode;
        StationCode = stationCode;
        IsAuthenticated = true;
    }

    public void UpdateStationCode(string stationCode)
    {
        StationCode = stationCode;
    }

    public void SignOut()
    {
        UserId = null;
        Username = string.Empty;
        DisplayName = string.Empty;
        RoleCode = string.Empty;
        StationCode = string.Empty;
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
        var profile = CameraStationProfile.Resolve(stationCode);

        var camera1Enabled = ParseBool(await _configRepo.GetValueAsync(profile.Camera1EnabledKey, ct), profile.Camera1EnabledDefault);
        var camera1Name = await _configRepo.GetValueAsync(profile.Camera1NameKey, ct) ?? profile.Camera1NameDefault;
        var camera1Rtsp = await _configRepo.GetValueAsync(profile.Camera1RtspKey, ct) ?? profile.Camera1RtspDefault;
        var camera1PreviewRtsp = await _configRepo.GetValueAsync(profile.Camera1PreviewRtspKey, ct) ?? profile.Camera1PreviewRtspDefault;

        var camera2Enabled = ParseBool(await _configRepo.GetValueAsync(profile.Camera2EnabledKey, ct), profile.Camera2EnabledDefault);
        var camera2Name = await _configRepo.GetValueAsync(profile.Camera2NameKey, ct) ?? profile.Camera2NameDefault;
        var camera2Rtsp = await _configRepo.GetValueAsync(profile.Camera2RtspKey, ct) ?? profile.Camera2RtspDefault;
        var camera2PreviewRtsp = await _configRepo.GetValueAsync(profile.Camera2PreviewRtspKey, ct) ?? profile.Camera2PreviewRtspDefault;

        var previewDefault = await _configRepo.GetValueAsync(AppConfigKeys.CameraPreviewDefault, ct) ?? AppConfigDefaults.DefaultCameraPreview;
        var timeoutMs = ParseInt(await _configRepo.GetValueAsync(AppConfigKeys.CameraCaptureTimeoutMs, ct), AppConfigDefaults.DefaultCameraCaptureTimeoutMs);
        var jpegQuality = ParseInt(await _configRepo.GetValueAsync(AppConfigKeys.CameraCaptureJpegQuality, ct), AppConfigDefaults.DefaultCameraCaptureJpegQuality);
        var maxDimension = ParseInt(await _configRepo.GetValueAsync(AppConfigKeys.CameraCaptureMaxDimension, ct), AppConfigDefaults.DefaultCameraCaptureMaxDimension);
        var warmupFrames = ParseInt(await _configRepo.GetValueAsync(AppConfigKeys.CameraCaptureWarmupFrames, ct), AppConfigDefaults.DefaultCameraCaptureWarmupFrames);

        return new CameraSystemSettings(
            new CameraEndpointSettings("CAM1", camera1Name.Trim(), camera1Rtsp.Trim(), camera1PreviewRtsp.Trim(), camera1Enabled),
            new CameraEndpointSettings("CAM2", camera2Name.Trim(), camera2Rtsp.Trim(), camera2PreviewRtsp.Trim(), camera2Enabled),
            string.IsNullOrWhiteSpace(previewDefault) ? AppConfigDefaults.DefaultCameraPreview : previewDefault.Trim().ToUpperInvariant(),
            Math.Clamp(timeoutMs, 500, 15000),
            Math.Clamp(jpegQuality, 40, 100),
            Math.Clamp(maxDimension, 320, 3840),
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

    private sealed record CameraStationProfile(
        string Camera1EnabledKey,
        string Camera1NameKey,
        string Camera1RtspKey,
        string Camera1PreviewRtspKey,
        string Camera1EnabledDefault,
        string Camera1NameDefault,
        string Camera1RtspDefault,
        string Camera1PreviewRtspDefault,
        string Camera2EnabledKey,
        string Camera2NameKey,
        string Camera2RtspKey,
        string Camera2PreviewRtspKey,
        string Camera2EnabledDefault,
        string Camera2NameDefault,
        string Camera2RtspDefault,
        string Camera2PreviewRtspDefault)
    {
        public static CameraStationProfile Resolve(string? stationCode)
        {
            if (string.Equals(stationCode, "C6", StringComparison.OrdinalIgnoreCase))
            {
                return new CameraStationProfile(
                    AppConfigKeys.CameraC6_1Enabled,
                    AppConfigKeys.CameraC6_1Name,
                    AppConfigKeys.CameraC6_1RtspUrl,
                    AppConfigKeys.CameraC6_1PreviewRtspUrl,
                    AppConfigDefaults.DefaultCameraC6_1Enabled,
                    AppConfigDefaults.DefaultCameraC6_1Name,
                    AppConfigDefaults.DefaultCameraC6_1RtspUrl,
                    AppConfigDefaults.DefaultCameraC6_1PreviewRtspUrl,
                    AppConfigKeys.CameraC6_2Enabled,
                    AppConfigKeys.CameraC6_2Name,
                    AppConfigKeys.CameraC6_2RtspUrl,
                    AppConfigKeys.CameraC6_2PreviewRtspUrl,
                    AppConfigDefaults.DefaultCameraC6_2Enabled,
                    AppConfigDefaults.DefaultCameraC6_2Name,
                    AppConfigDefaults.DefaultCameraC6_2RtspUrl,
                    AppConfigDefaults.DefaultCameraC6_2PreviewRtspUrl);
            }

            if (string.Equals(stationCode, "CRUSHER", StringComparison.OrdinalIgnoreCase))
            {
                return new CameraStationProfile(
                    AppConfigKeys.CameraCrusher_1Enabled,
                    AppConfigKeys.CameraCrusher_1Name,
                    AppConfigKeys.CameraCrusher_1RtspUrl,
                    AppConfigKeys.CameraCrusher_1PreviewRtspUrl,
                    AppConfigDefaults.DefaultCameraCrusher_1Enabled,
                    AppConfigDefaults.DefaultCameraCrusher_1Name,
                    AppConfigDefaults.DefaultCameraCrusher_1RtspUrl,
                    AppConfigDefaults.DefaultCameraCrusher_1PreviewRtspUrl,
                    AppConfigKeys.CameraCrusher_2Enabled,
                    AppConfigKeys.CameraCrusher_2Name,
                    AppConfigKeys.CameraCrusher_2RtspUrl,
                    AppConfigKeys.CameraCrusher_2PreviewRtspUrl,
                    AppConfigDefaults.DefaultCameraCrusher_2Enabled,
                    AppConfigDefaults.DefaultCameraCrusher_2Name,
                    AppConfigDefaults.DefaultCameraCrusher_2RtspUrl,
                    AppConfigDefaults.DefaultCameraCrusher_2PreviewRtspUrl);
            }

            if (string.Equals(stationCode, "CLAY", StringComparison.OrdinalIgnoreCase))
            {
                return new CameraStationProfile(
                    AppConfigKeys.CameraClay_1Enabled,
                    AppConfigKeys.CameraClay_1Name,
                    AppConfigKeys.CameraClay_1RtspUrl,
                    AppConfigKeys.CameraClay_1PreviewRtspUrl,
                    AppConfigDefaults.DefaultCameraClay_1Enabled,
                    AppConfigDefaults.DefaultCameraClay_1Name,
                    AppConfigDefaults.DefaultCameraClay_1RtspUrl,
                    AppConfigDefaults.DefaultCameraClay_1PreviewRtspUrl,
                    AppConfigKeys.CameraClay_2Enabled,
                    AppConfigKeys.CameraClay_2Name,
                    AppConfigKeys.CameraClay_2RtspUrl,
                    AppConfigKeys.CameraClay_2PreviewRtspUrl,
                    AppConfigDefaults.DefaultCameraClay_2Enabled,
                    AppConfigDefaults.DefaultCameraClay_2Name,
                    AppConfigDefaults.DefaultCameraClay_2RtspUrl,
                    AppConfigDefaults.DefaultCameraClay_2PreviewRtspUrl);
            }

            return new CameraStationProfile(
                AppConfigKeys.Camera1Enabled,
                AppConfigKeys.Camera1Name,
                AppConfigKeys.Camera1RtspUrl,
                AppConfigKeys.Camera1PreviewRtspUrl,
                AppConfigDefaults.DefaultCamera1Enabled,
                AppConfigDefaults.DefaultCamera1Name,
                AppConfigDefaults.DefaultCamera1RtspUrl,
                AppConfigDefaults.DefaultCamera1PreviewRtspUrl,
                AppConfigKeys.Camera2Enabled,
                AppConfigKeys.Camera2Name,
                AppConfigKeys.Camera2RtspUrl,
                AppConfigKeys.Camera2PreviewRtspUrl,
                AppConfigDefaults.DefaultCamera2Enabled,
                AppConfigDefaults.DefaultCamera2Name,
                AppConfigDefaults.DefaultCamera2RtspUrl,
                AppConfigDefaults.DefaultCamera2PreviewRtspUrl);
        }
    }
}

public class AuditService : IAuditService
{
    private readonly StationDbContext _db;
    private readonly ICurrentUserContext _userContext;
    private readonly ICurrentStationContext _currentStationContext;
    private readonly IClock _clock;

    public AuditService(StationDbContext db, ICurrentUserContext userContext, ICurrentStationContext currentStationContext, IClock clock)
    {
        _db = db; _userContext = userContext; _currentStationContext = currentStationContext; _clock = clock;
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
            CreatedAt = _clock.NowLocal,
            StationCode = _currentStationContext.StationCode
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

    public string CreatePayload(WeighingSession session)
        => JsonSerializer.Serialize(session, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(WeighingSessionLine line)
        => JsonSerializer.Serialize(line, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(SyncStationMasterDataRequest station)
        => JsonSerializer.Serialize(station, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(Vehicle vehicle)
        => JsonSerializer.Serialize(vehicle, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(Customer customer)
        => JsonSerializer.Serialize(customer, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public string CreatePayload(Product product)
        => JsonSerializer.Serialize(product, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}

