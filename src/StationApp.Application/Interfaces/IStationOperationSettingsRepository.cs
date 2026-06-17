namespace StationApp.Application.Interfaces;

public interface IStationOperationSettingsRepository
{
    Task<string?> GetValueAsync(string stationCode, string settingKey, CancellationToken ct);
}
