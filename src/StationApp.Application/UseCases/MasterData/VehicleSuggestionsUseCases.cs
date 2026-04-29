using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;

namespace StationApp.Application.UseCases.MasterData;

public class SearchVehicleSuggestionsUseCase
{
    private readonly IVehicleRepository _vehicleRepo;

    public SearchVehicleSuggestionsUseCase(IVehicleRepository vehicleRepo)
    {
        _vehicleRepo = vehicleRepo;
    }

    public async Task<IReadOnlyList<string>> ExecuteAsync(string? plate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plate)) return Array.Empty<string>();
        
        var vehicles = await _vehicleRepo.SearchAsync(plate, ct);
        return vehicles.Select(v => v.VehiclePlate).Distinct().ToList().AsReadOnly();
    }
}

public class SearchVehicleMoocOptionsUseCase
{
    private readonly IVehicleRepository _vehicleRepo;

    public SearchVehicleMoocOptionsUseCase(IVehicleRepository vehicleRepo)
    {
        _vehicleRepo = vehicleRepo;
    }

    public async Task<IReadOnlyList<Vehicle>> ExecuteAsync(string plate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plate)) return Array.Empty<Vehicle>();
        return await _vehicleRepo.GetByPlateAsync(plate, ct);
    }
}
