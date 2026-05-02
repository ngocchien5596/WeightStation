using StationApp.Application.DTOs;

namespace StationApp.Application.Interfaces;

public interface IVehicleAutocompleteSource
{
    Task<IReadOnlyList<VehicleAutocompleteSource>> SearchVehicleSourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<VehicleAutocompleteSource>> SearchMoocSourcesAsync(string keyword, int limit, CancellationToken ct);
    Task<IReadOnlyList<DriverAutocompleteSource>> SearchDriverSourcesAsync(string keyword, int limit, CancellationToken ct);
}
