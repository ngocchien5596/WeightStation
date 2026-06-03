namespace StationApp.Application.Interfaces;

public interface ICentralApiHealthChecker
{
    Task<CentralApiHealthCheckResult> CheckAsync(CancellationToken ct);
    Task<CentralApiHealthCheckResult> CheckAsync(string? baseUrl, string? apiKey, CancellationToken ct);
}

public sealed record CentralApiHealthCheckResult(
    bool Success,
    string StatusCode,
    string Message
);
