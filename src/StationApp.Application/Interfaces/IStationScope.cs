using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface ICurrentStationContext
{
    string? StationCode { get; }
    string? StationName { get; }
    bool HasStation { get; }
    void SetStation(string stationCode, string stationName);
    void Clear();
}

public interface IStationScope
{
    Task<string> GetCurrentStationCodeAsync(CancellationToken ct);
}

public interface IStationAuthorizationService
{
    Task<IReadOnlyList<StationOptionDto>> GetAllowedStationsAsync(Guid userId, CancellationToken ct);
    Task<bool> CanAccessStationAsync(Guid userId, string stationCode, CancellationToken ct);
    Task EnsureCanAccessStationAsync(Guid userId, string stationCode, CancellationToken ct);
}

public interface IStationAdministrationService
{
    Task<IReadOnlyList<StationManagementDto>> SearchStationsAsync(string? stationCode, string? stationName, bool? isActive, CancellationToken ct);
    Task<StationManagementDto> SaveStationAsync(SaveStationRequest request, CancellationToken ct);
    Task<IReadOnlyList<UserStationAssignmentDto>> GetAssignableStationsAsync(CancellationToken ct);
    Task<IReadOnlyList<UserStationAssignmentDto>> GetUserStationAssignmentsAsync(Guid userId, CancellationToken ct);
    Task SaveUserStationAssignmentsAsync(Guid userId, IReadOnlyList<SaveUserStationAssignmentDto> assignments, CancellationToken ct);
}

public interface IStationFeatureService
{
    Task<StationFeatureSetDto> GetFeaturesAsync(string stationCode, CancellationToken ct);
    Task<bool> IsEnabledAsync(string stationCode, string featureKey, CancellationToken ct);
}
