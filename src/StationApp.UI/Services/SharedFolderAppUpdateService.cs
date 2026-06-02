using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Services;
using StationApp.Domain.Constants;

namespace StationApp.UI.Services;

public sealed class SharedFolderAppUpdateService : IAppUpdateService
{
    private const string DefaultManifestPath = @"\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can\latest.json";
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppVersionProvider _versionProvider;
    private readonly ILogger<SharedFolderAppUpdateService>? _logger;

    public SharedFolderAppUpdateService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IAppVersionProvider versionProvider,
        ILogger<SharedFolderAppUpdateService>? logger = null)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _versionProvider = versionProvider;
        _logger = logger;
    }

    public string GetCurrentVersion() => _versionProvider.GetVersion();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct)
    {
        var currentVersion = GetCurrentVersion();
        try
        {
            var manifestPath = await GetManifestPathAsync(ct);
            if (!File.Exists(manifestPath))
            {
                return new AppUpdateCheckResult(
                    currentVersion,
                    false,
                    ErrorMessage: $"Không tìm thấy file manifest: {manifestPath}");
            }

            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<AppUpdateManifest>(
                stream,
                ManifestJsonOptions,
                ct);

            if (manifest == null)
            {
                return new AppUpdateCheckResult(
                    currentVersion,
                    false,
                    ErrorMessage: "Manifest cập nhật không hợp lệ.");
            }

            var isUpdateAvailable = AppUpdateVersionComparer.Compare(manifest.Version, currentVersion) > 0;

            if (AppUpdateVersionComparer.TryParse(manifest.MinSupportedVersion, out var minVersion) &&
                AppUpdateVersionComparer.TryParse(currentVersion, out var currentParsed) &&
                currentParsed!.CompareTo(minVersion) < 0)
            {
                return new AppUpdateCheckResult(
                    currentVersion,
                    isUpdateAvailable,
                    manifest,
                    IsForceUpdateRequired: true,
                    StatusMessage: "Phiên bản hiện tại đã quá cũ. Vui lòng bấm 'Cập nhật ngay' để cập nhật.");
            }

            return new AppUpdateCheckResult(currentVersion, isUpdateAvailable, manifest);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CheckForUpdatesAsync failed.");
            return new AppUpdateCheckResult(currentVersion, false, ErrorMessage: ex.Message);
        }
    }

    public async Task<OperationResult<bool>> StartUpdateAsync(AppUpdateManifest manifest, CancellationToken ct)
    {
        try
        {
            var updaterInstalledPath = GetUpdaterInstalledPath();
            if (!File.Exists(updaterInstalledPath))
            {
                return OperationResult<bool>.Fail($"Không tìm thấy updater tại: {updaterInstalledPath}");
            }

            if (!File.Exists(manifest.PackagePath))
            {
                return OperationResult<bool>.Fail($"Không tìm thấy gói cập nhật: {manifest.PackagePath}");
            }

            var cacheDirectory = GetCacheDirectory();
            Directory.CreateDirectory(cacheDirectory);

            var localPackagePath = Path.Combine(cacheDirectory, manifest.PackageName);
            File.Copy(manifest.PackagePath, localPackagePath, overwrite: true);

            var actualHash = await ComputeSha256Async(localPackagePath, ct);
            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<bool>.Fail("Hash gói cập nhật không khớp.");
            }

            var tempUpdaterDirectory = Path.Combine(cacheDirectory, "updater-run");
            Directory.CreateDirectory(tempUpdaterDirectory);
            var tempUpdaterPath = Path.Combine(tempUpdaterDirectory, Path.GetFileName(updaterInstalledPath));
            File.Copy(updaterInstalledPath, tempUpdaterPath, overwrite: true);

            var appDirectory = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var appExePath = GetCurrentExecutablePath();
            var configFilePath = Path.Combine(appDirectory, "appsettings.json");
            var migratorExePath = Path.Combine(appDirectory, "Tools", "DbMigrator", "StationApp.DbMigrator.exe");
            var backupDirectory = appDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "._backup";
            var extractDirectory = appDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "._update";
            var logFilePath = Path.Combine(appDirectory, "logs", "updater.log");

            var arguments = string.Join(" ", new[]
            {
                $"--app-dir \"{appDirectory}\"",
                $"--app-exe \"{appExePath}\"",
                $"--package \"{localPackagePath}\"",
                $"--config \"{configFilePath}\"",
                $"--migrator-exe \"{migratorExePath}\"",
                $"--backup-dir \"{backupDirectory}\"",
                $"--extract-dir \"{extractDirectory}\"",
                $"--log-file \"{logFilePath}\"",
                $"--process-id {Environment.ProcessId}",
                $"--run-db-migrator {manifest.DbMigratorRequired.ToString().ToLowerInvariant()}"
            });

            Process.Start(new ProcessStartInfo
            {
                FileName = tempUpdaterPath,
                Arguments = arguments,
                WorkingDirectory = tempUpdaterDirectory,
                UseShellExecute = true
            });

            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "StartUpdateAsync failed.");
            return OperationResult<bool>.Fail(ex.Message);
        }
    }

    public async Task<string> GetResolvedManifestPathAsync(CancellationToken ct)
        => await GetManifestPathAsync(ct);

    public async Task<string> GetConfiguredSharedReleaseRootAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var appConfigValue = (await repo.GetValueAsync(AppConfigKeys.AppUpdateSharedReleaseRoot, ct))?.Trim();
        if (!string.IsNullOrWhiteSpace(appConfigValue))
        {
            return appConfigValue;
        }

        return _configuration["AppUpdate:SharedReleaseRoot"]?.Trim() ?? string.Empty;
    }

    private async Task<string> GetManifestPathAsync(CancellationToken ct)
    {
        var sharedReleaseRoot = await GetConfiguredSharedReleaseRootAsync(ct);
        if (!string.IsNullOrWhiteSpace(sharedReleaseRoot))
        {
            return ResolveManifestPath(sharedReleaseRoot);
        }

        var manifestPath = _configuration["AppUpdate:ManifestPath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            return manifestPath;
        }

        return DefaultManifestPath;
    }

    private static string ResolveManifestPath(string sharedReleaseRootOrManifestPath)
    {
        var candidate = sharedReleaseRootOrManifestPath.Trim();
        if (candidate.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        return Path.Combine(candidate, "latest.json");
    }

    private string GetCacheDirectory()
    {
        var configured = _configuration["AppUpdate:LocalPackageCacheDir"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return Path.Combine(
            Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            "_cache",
            "updates");
    }

    private static string GetUpdaterInstalledPath()
    {
        var appDirectory = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.Combine(appDirectory, "Tools", "Updater", "StationApp.Updater.exe");
    }

    private static string GetCurrentExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Không xác định được đường dẫn app hiện tại.");
        }

        return processPath;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
