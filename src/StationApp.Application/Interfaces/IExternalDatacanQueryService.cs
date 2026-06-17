using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IExternalDatacanQueryService
{
    Task<ExternalDatacanQueryResult> GetLatestAsync(
        string source,
        string? vehiclePlateKeyword,
        string? productKeyword,
        string? customerKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
}
