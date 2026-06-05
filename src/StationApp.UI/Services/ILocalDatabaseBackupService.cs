namespace StationApp.UI.Services;

public interface ILocalDatabaseBackupService
{
    Task<LocalDatabaseBackupRunResult> RunBackupNowAsync(CancellationToken cancellationToken);
}

public sealed record LocalDatabaseBackupRunResult(
    bool Success,
    string Message,
    string? BackupFilePath = null);
