using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IAppUpdateService
{
    string GetCurrentVersion();
    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct);
    Task<OperationResult<bool>> StartUpdateAsync(AppUpdateManifest manifest, CancellationToken ct);
}
