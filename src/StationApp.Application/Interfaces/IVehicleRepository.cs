using StationApp.Application.DTOs;
using StationApp.Domain.Entities;

namespace StationApp.Application.Interfaces;

public interface IVehicleRepository : IVehicleAutocompleteSource
{
    Task AddAsync(Vehicle vehicle, CancellationToken ct);
    Task UpdateAsync(Vehicle vehicle, CancellationToken ct);
    Task<Vehicle?> GetByPlateAndMoocAsync(string vehiclePlate, string moocNumber, CancellationToken ct);
    Task<IReadOnlyList<Vehicle>> GetByPlateAsync(string vehiclePlate, CancellationToken ct);
    Task<IReadOnlyList<Vehicle>> GetByMoocAsync(string moocNumber, CancellationToken ct);
    Task<IReadOnlyList<Vehicle>> SearchAsync(string? keyword, CancellationToken ct);
}
