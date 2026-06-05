using System.Globalization;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;

namespace StationApp.UI.Services;

public sealed class LocalDatabaseBackupWorker : BackgroundService
    , ILocalDatabaseBackupService
{
    private static readonly TimeSpan ScheduledBackupTime = new(3, 0, 0);
    private static readonly TimeSpan RetryDelayOnFailure = TimeSpan.FromMinutes(30);
    private const int RetentionDays = 10;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LocalDatabaseBackupWorker> _logger;

    public LocalDatabaseBackupWorker(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<LocalDatabaseBackupWorker> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Local DB backup worker disabled because DefaultConnection is missing.");
            return;
        }

        var databaseName = ResolveDatabaseName(connectionString);
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            _logger.LogWarning("Local DB backup worker disabled because database name could not be resolved from DefaultConnection.");
            return;
        }

        _logger.LogInformation(
            "Local DB backup worker started. Database={DatabaseName} ScheduledTime={ScheduledTime} RetentionDays={RetentionDays}",
            databaseName,
            ScheduledBackupTime,
            RetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            var backupDirectory = await ResolveBackupDirectoryAsync(stoppingToken);
            Directory.CreateDirectory(backupDirectory);

            var now = DateTime.Now;
            var todayFilePath = BuildBackupFilePath(backupDirectory, databaseName, now.Date);
            var todayTargetTime = now.Date.Add(ScheduledBackupTime);

            if (now >= todayTargetTime && !File.Exists(todayFilePath))
            {
                try
                {
                    var result = await RunBackupCoreAsync(connectionString, databaseName, todayFilePath, stoppingToken);
                    _logger.LogInformation(
                        "Local DB backup completed successfully. Database={DatabaseName} File={BackupFile} Trigger={Trigger}",
                        databaseName,
                        result.BackupFilePath,
                        "Scheduled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Local DB backup failed. Database={DatabaseName} ScheduledDate={ScheduledDate} File={BackupFile}",
                        databaseName,
                        now.Date.ToString("yyyy-MM-dd"),
                        todayFilePath);

                    await Task.Delay(RetryDelayOnFailure, stoppingToken);
                    continue;
                }
            }

            try
            {
                CleanupExpiredBackups(backupDirectory, now.Date.AddDays(-RetentionDays));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to cleanup expired local DB backups. BackupDirectory={BackupDirectory} RetentionDays={RetentionDays}",
                    backupDirectory,
                    RetentionDays);
            }

            var nextRunAt = GetNextRunAt(DateTime.Now);
            var delay = nextRunAt - DateTime.Now;
            if (delay < TimeSpan.FromMinutes(1))
            {
                delay = TimeSpan.FromMinutes(1);
            }

            _logger.LogDebug(
                "Next local DB backup check scheduled. Database={DatabaseName} NextCheckAt={NextCheckAt} BackupDirectory={BackupDirectory}",
                databaseName,
                nextRunAt,
                backupDirectory);

            await Task.Delay(delay, stoppingToken);
        }
    }

    public async Task<LocalDatabaseBackupRunResult> RunBackupNowAsync(CancellationToken cancellationToken)
    {
        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            const string message = "Không tìm thấy cấu hình kết nối DB local để sao lưu.";
            _logger.LogWarning("Manual local DB backup rejected because DefaultConnection is missing.");
            return new LocalDatabaseBackupRunResult(false, message);
        }

        var databaseName = ResolveDatabaseName(connectionString);
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            const string message = "Không xác định được tên DB local để sao lưu.";
            _logger.LogWarning("Manual local DB backup rejected because database name could not be resolved from DefaultConnection.");
            return new LocalDatabaseBackupRunResult(false, message);
        }

        var backupDirectory = await ResolveBackupDirectoryAsync(cancellationToken);
        Directory.CreateDirectory(backupDirectory);
        var backupFilePath = BuildBackupFilePath(backupDirectory, databaseName, DateTime.Today);

        try
        {
            var result = await RunBackupCoreAsync(connectionString, databaseName, backupFilePath, cancellationToken);
            CleanupExpiredBackups(backupDirectory, DateTime.Today.AddDays(-RetentionDays));
            _logger.LogInformation(
                "Manual local DB backup completed successfully. Database={DatabaseName} File={BackupFile}",
                databaseName,
                result.BackupFilePath);
            return result with
            {
                Message = $"Đã sao lưu DB local thành công: {result.BackupFilePath}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Manual local DB backup failed. Database={DatabaseName} File={BackupFile}",
                databaseName,
                backupFilePath);
            return new LocalDatabaseBackupRunResult(false, $"Sao lưu DB local thất bại: {ex.Message}");
        }
    }

    private static DateTime GetNextRunAt(DateTime now)
    {
        var todayTarget = now.Date.Add(ScheduledBackupTime);
        return now < todayTarget ? todayTarget : todayTarget.AddDays(1);
    }

    private string? ResolveConnectionString()
    {
        var configured = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            return null;
        }

        try
        {
            var fallbackConfiguration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var fallbackConnectionString = fallbackConfiguration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(fallbackConnectionString))
            {
                _logger.LogInformation(
                    "Local DB backup worker resolved DefaultConnection from appsettings.json in executable directory. BaseDirectory={BaseDirectory}",
                    AppContext.BaseDirectory);
            }

            return fallbackConnectionString;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Local DB backup worker failed to resolve DefaultConnection from appsettings.json in executable directory. BaseDirectory={BaseDirectory}",
                AppContext.BaseDirectory);
            return null;
        }
    }

    private static string ResolveDatabaseName(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return builder.InitialCatalog;
    }

    private async Task<string> ResolveBackupDirectoryAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
        var configuredPath = await repo.GetValueAsync(AppConfigKeys.LocalDatabaseBackupDirectory, ct);
        var trimmed = configuredPath?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "StationApp", "SqlBackups");
    }

    private static string BuildBackupFilePath(string backupDirectory, string databaseName, DateTime date)
        => Path.Combine(backupDirectory, $"{date:yyyyMMdd}_{databaseName}.bak");

    private async Task<LocalDatabaseBackupRunResult> RunBackupCoreAsync(
        string connectionString,
        string databaseName,
        string backupFilePath,
        CancellationToken ct)
    {
        await _runLock.WaitAsync(ct);
        try
        {
            await BackupDatabaseAsync(connectionString, databaseName, backupFilePath, ct);
            return new LocalDatabaseBackupRunResult(
                true,
                $"Đã sao lưu DB local thành công: {backupFilePath}",
                backupFilePath);
        }
        finally
        {
            _runLock.Release();
        }
    }

    private void CleanupExpiredBackups(string backupDirectory, DateTime cutoffDate)
    {
        foreach (var filePath in Directory.EnumerateFiles(backupDirectory, "*.bak", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Length < 8)
            {
                continue;
            }

            var datePart = fileName[..8];
            if (!DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var backupDate))
            {
                continue;
            }

            if (backupDate >= cutoffDate)
            {
                continue;
            }

            File.Delete(filePath);
            _logger.LogInformation(
                "Deleted expired local DB backup. File={BackupFile} BackupDate={BackupDate:yyyy-MM-dd} CutoffDate={CutoffDate:yyyy-MM-dd}",
                filePath,
                backupDate,
                cutoffDate);
        }
    }

    private static async Task BackupDatabaseAsync(
        string sourceConnectionString,
        string databaseName,
        string backupFilePath,
        CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(sourceConnectionString)
        {
            InitialCatalog = "master"
        };

        var escapedPath = backupFilePath.Replace("'", "''");
        var sql = $"""
BACKUP DATABASE [{databaseName}]
TO DISK = N'{escapedPath}'
WITH INIT, COPY_ONLY, STATS = 10;
""";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
