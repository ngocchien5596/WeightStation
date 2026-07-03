using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly StationDbContext _context;

    public VehicleRepository(StationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken ct)
    {
        await _context.Vehicles.AddAsync(vehicle, ct);
    }

    public async Task UpdateAsync(Vehicle vehicle, CancellationToken ct)
    {
        if (_context.Entry(vehicle).State == EntityState.Detached)
        {
            _context.Vehicles.Update(vehicle);
        }
        await Task.CompletedTask;
    }

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task<Vehicle?> GetByPlateAndMoocAsync(string vehiclePlate, string moocNumber, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        return await _context.Vehicles
            .FirstOrDefaultAsync(v => v.StationCode == stationCode && v.VehiclePlate == vehiclePlate && v.MoocNumber == moocNumber, ct);
    }

    public async Task<IReadOnlyList<Vehicle>> GetByPlateAsync(string vehiclePlate, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        var list = await _context.Vehicles
            .Where(v => v.StationCode == stationCode && v.VehiclePlate == vehiclePlate)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Vehicle>> GetByMoocAsync(string moocNumber, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        var list = await _context.Vehicles
            .Where(v => v.StationCode == stationCode && v.MoocNumber == moocNumber)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Vehicle>> SearchAsync(string? keyword, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        var query = _context.Vehicles.Where(v => v.StationCode == stationCode);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(v => v.VehiclePlate.Contains(keyword) || v.MoocNumber.Contains(keyword));
        }
        var list = await query.ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Vehicle>> SearchInternalVehiclesAsync(string? keyword, int limit, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        var query = _context.Vehicles.AsNoTracking()
            .Where(v => v.StationCode == stationCode && v.IsActive && v.IsInternalVehicle);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(v =>
                v.VehiclePlate.Contains(normalized) ||
                (v.DriverName != null && v.DriverName.Contains(normalized)));
        }

        var list = await query
            .OrderBy(v => v.VehiclePlate)
            .Take(Math.Max(1, limit))
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<VehicleAutocompleteSource>> SearchVehicleSourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var normalized = keyword.Trim();
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);

        var list = await _context.Vehicles.AsNoTracking()
            .Where(v => v.StationCode == stationCode && v.IsActive && v.VehiclePlate != null && v.VehiclePlate.Contains(normalized))
            .OrderByDescending(v => v.VehiclePlate.StartsWith(normalized))
            .ThenBy(v => v.VehiclePlate)
            .Take(limit)
            .Select(v => new VehicleAutocompleteSource(
                v.VehiclePlate,
                string.IsNullOrWhiteSpace(v.MoocNumber) ? null : v.MoocNumber,
                v.DriverName,
                v.TtcpWeight,
                v.VehicleRegistrationNo,
                v.VehicleRegistrationExpiryDate,
                v.MoocRegistrationNo,
                v.MoocRegistrationExpiryDate,
                "MASTER"))
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<VehicleAutocompleteSource>> SearchMoocSourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var normalized = keyword.Trim();
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);

        var list = await _context.Vehicles.AsNoTracking()
            .Where(v => v.StationCode == stationCode && v.IsActive && v.MoocNumber != null && v.MoocNumber.Contains(normalized))
            .OrderByDescending(v => v.MoocNumber != null && v.MoocNumber.StartsWith(normalized))
            .ThenBy(v => v.MoocNumber)
            .Take(limit)
            .Select(v => new VehicleAutocompleteSource(
                v.VehiclePlate,
                v.MoocNumber,
                v.DriverName,
                v.TtcpWeight,
                v.VehicleRegistrationNo,
                v.VehicleRegistrationExpiryDate,
                v.MoocRegistrationNo,
                v.MoocRegistrationExpiryDate,
                "MASTER"))
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DriverAutocompleteSource>> SearchDriverSourcesAsync(string keyword, int limit, CancellationToken ct)
    {
        var normalized = keyword.Trim();
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);

        var list = await _context.Vehicles.AsNoTracking()
            .Where(v => v.StationCode == stationCode && v.IsActive && v.DriverName != null && v.DriverName.Contains(normalized))
            .OrderByDescending(v => v.DriverName != null && v.DriverName.StartsWith(normalized))
            .ThenBy(v => v.DriverName)
            .Take(limit)
            .Select(v => new DriverAutocompleteSource(
                v.DriverName!,
                v.VehiclePlate,
                string.IsNullOrWhiteSpace(v.MoocNumber) ? null : v.MoocNumber,
                "MASTER"))
            .ToListAsync(ct);

        return list.AsReadOnly();
    }
}
