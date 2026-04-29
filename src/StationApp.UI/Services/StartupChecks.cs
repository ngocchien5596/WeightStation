using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using System.IO;

namespace StationApp.UI.Services;

/// <summary>
/// Performs startup health checks and reports results.
/// Checks: Database connectivity, required config keys, device readiness.
/// </summary>
public sealed class StartupChecks
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StartupChecks> _logger;

    public List<StartupCheckResult> Results { get; } = new();
    public bool HasCriticalFailure => Results.Any(r => r.IsCritical && !r.IsOk);

    public StartupChecks(IServiceScopeFactory scopeFactory, ILogger<StartupChecks> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunAllAsync(CancellationToken ct)
    {
        Results.Clear();
        _logger.LogInformation("Running startup checks...");

        await CheckDatabaseAsync(ct);
        await CheckRequiredConfigAsync(ct);
        CheckDiskSpace();

        var passed = Results.Count(r => r.IsOk);
        var total = Results.Count;
        _logger.LogInformation("Startup checks: {Passed}/{Total} passed", passed, total);
    }

    private async Task CheckDatabaseAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            await appConfig.GetValueAsync("station_code", ct);
            Results.Add(new StartupCheckResult("Database", true, "SQL Server kết nối thành công"));
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Database check failed");
            Results.Add(new StartupCheckResult("Database", false, $"Lỗi: {ex.Message}", true));
        }
    }

    private async Task CheckRequiredConfigAsync(CancellationToken ct)
    {
        var requiredKeys = new[] { "station_code", "ticket_prefix" };
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var appConfig = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

            var missingKeys = new List<string>();
            foreach (var key in requiredKeys)
            {
                var val = await appConfig.GetValueAsync(key, ct);
                if (string.IsNullOrEmpty(val))
                    missingKeys.Add(key);
            }

            if (missingKeys.Count > 0)
            {
                Results.Add(new StartupCheckResult("Config", false,
                    $"Thiếu cấu hình: {string.Join(", ", missingKeys)}. Hãy vào Cấu hình hệ thống để thiết lập."));
            }
            else
            {
                Results.Add(new StartupCheckResult("Config", true, "Tất cả cấu hình bắt buộc đã có"));
            }
        }
        catch (Exception ex)
        {
            Results.Add(new StartupCheckResult("Config", false, $"Lỗi kiểm tra config: {ex.Message}"));
        }
    }

    private void CheckDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory)!);
            var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            if (freeGb < 1.0)
            {
                Results.Add(new StartupCheckResult("Disk", false,
                    $"Ổ đĩa còn {freeGb:F1} GB. Cần tối thiểu 1 GB.", true));
            }
            else
            {
                Results.Add(new StartupCheckResult("Disk", true, $"Ổ đĩa còn {freeGb:F1} GB"));
            }
        }
        catch (Exception ex)
        {
            Results.Add(new StartupCheckResult("Disk", false, $"Không kiểm tra được: {ex.Message}"));
        }
    }
}

public sealed record StartupCheckResult(string Name, bool IsOk, string Message, bool IsCritical = false);
