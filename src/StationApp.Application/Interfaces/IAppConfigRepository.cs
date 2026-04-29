namespace StationApp.Application.Interfaces;

public interface IAppConfigRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken ct);
    Task SetValueAsync(string key, string value, CancellationToken ct);
}
