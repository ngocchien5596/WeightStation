namespace StationApp.Application.Interfaces;

public interface IStationOperationSettingsRepository
{
    Task<string?> GetValueAsync(string stationCode, string settingKey, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> GetSettingsByStationAsync(string stationCode, CancellationToken ct);
    Task SaveSettingsAsync(string stationCode, IReadOnlyDictionary<string, string> settings, string actor, CancellationToken ct);
}
