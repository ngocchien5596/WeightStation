using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
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

public sealed class StationOperationSettingsRepository : IStationOperationSettingsRepository
{
    private readonly StationDbContext _context;

    public StationOperationSettingsRepository(StationDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetValueAsync(string stationCode, string settingKey, CancellationToken ct)
    {
        return await _context.StationOperationSettings.AsNoTracking()
            .Where(x => x.StationCode == stationCode && x.SettingKey == settingKey)
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSettingsByStationAsync(string stationCode, CancellationToken ct)
    {
        var list = await _context.StationOperationSettings.AsNoTracking()
            .Where(x => x.StationCode == stationCode)
            .ToListAsync(ct);
        return list.ToDictionary(x => x.SettingKey, x => x.SettingValue);
    }

    public async Task SaveSettingsAsync(string stationCode, IReadOnlyDictionary<string, string> settings, string actor, CancellationToken ct)
    {
        var existingSettings = await _context.StationOperationSettings
            .Where(x => x.StationCode == stationCode)
            .ToListAsync(ct);

        var now = DateTime.Now;

        foreach (var kvp in settings)
        {
            var existing = existingSettings.FirstOrDefault(x => x.SettingKey == kvp.Key);
            if (existing != null)
            {
                if (existing.SettingValue != kvp.Value)
                {
                    existing.SettingValue = kvp.Value;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = actor;
                }
            }
            else
            {
                var newSetting = new StationOperationSetting
                {
                    Id = Guid.NewGuid(),
                    StationCode = stationCode,
                    SettingKey = kvp.Key,
                    SettingValue = kvp.Value,
                    CreatedAt = now,
                    CreatedBy = actor
                };
                await _context.StationOperationSettings.AddAsync(newSetting, ct);
            }
        }
    }
}

public class CustomerRepository : ICustomerRepository
{
    private readonly StationDbContext _context;

    public CustomerRepository(StationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Customer customer, CancellationToken ct)
    {
        await _context.Customers.AddAsync(customer, ct);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken ct)
    {
        if (_context.Entry(customer).State == EntityState.Detached)
        {
            _context.Customers.Update(customer);
        }
        await Task.CompletedTask;
    }

    public async Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.StationCode == stationCode && c.CustomerCode == customerCode, ct);
    }

    public async Task<IReadOnlyList<Customer>> SearchAsync(string? keyword, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        var query = _context.Customers.Where(c => c.StationCode == stationCode);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(c => c.CustomerCode.Contains(keyword) || c.CustomerName.Contains(keyword));
        }
        var list = await query.ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<CustomerAutocompleteSource>> SearchAutocompleteAsync(string keyword, int limit, CancellationToken ct)
    {
        var normalized = keyword.Trim();
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);

        var list = await _context.Customers.AsNoTracking()
            .Where(c => c.StationCode == stationCode && c.IsActive && (c.CustomerCode.Contains(normalized) || c.CustomerName.Contains(normalized)))
            .OrderByDescending(c => c.CustomerName.StartsWith(normalized) || c.CustomerCode.StartsWith(normalized))
            .ThenBy(c => c.CustomerName)
            .Take(limit)
            .Select(c => new CustomerAutocompleteSource(
                c.CustomerCode,
                c.CustomerName,
                "MASTER"))
            .ToListAsync(ct);

        return list.AsReadOnly();
    }
}

public class ProductRepository : IProductRepository
{
    private readonly StationDbContext _context;

    public ProductRepository(StationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Product product, CancellationToken ct)
    {
        await _context.Products.AddAsync(product, ct);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct)
    {
        if (_context.Entry(product).State == EntityState.Detached)
        {
            _context.Products.Update(product);
        }
        await Task.CompletedTask;
    }

    public async Task<Product?> GetByCodeAsync(string productCode, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        return await _context.Products
            .FirstOrDefaultAsync(p => p.StationCode == stationCode && p.ProductCode == productCode, ct);
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(string? keyword, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        var query = _context.Products.Where(p => p.StationCode == stationCode);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => p.ProductCode.Contains(keyword) || p.ProductName.Contains(keyword));
        }
        var list = await query.ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ProductAutocompleteSource>> SearchAutocompleteAsync(string keyword, int limit, CancellationToken ct)
    {
        var normalized = keyword.Trim();
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);

        var list = await _context.Products.AsNoTracking()
            .Where(p => p.StationCode == stationCode && p.IsActive && (p.ProductCode.Contains(normalized) || p.ProductName.Contains(normalized)))
            .OrderByDescending(p => p.ProductCode.StartsWith(normalized) || p.ProductName.StartsWith(normalized))
            .ThenBy(p => p.ProductCode)
            .Take(limit)
            .Select(p => new ProductAutocompleteSource(
                p.ProductCode,
                p.ProductName,
                p.ProductType,
                "MASTER"))
            .ToListAsync(ct);

        return list.AsReadOnly();
    }
}

public class DeliveryTicketRepository : IDeliveryTicketRepository
{
    private readonly StationDbContext _context;

    public DeliveryTicketRepository(StationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeliveryTicket ticket, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ticket.StationCode))
        {
            ticket.StationCode = await ResolveDeliveryTicketStationCodeAsync(ticket, ct)
                ?? await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        }

        SyncTrackedEntityUpdateHelper.PrepareForAdd(ticket);
        await _context.DeliveryTickets.AddAsync(ticket, ct);
    }

    public async Task UpdateAsync(DeliveryTicket ticket, CancellationToken ct)
    {
        SyncTrackedEntityUpdateHelper.PrepareForUpdate(_context, ticket);
        if (_context.Entry(ticket).State == EntityState.Detached)
        {
            _context.DeliveryTickets.Update(ticket);
        }
        await Task.CompletedTask;
    }

    public async Task<DeliveryTicket?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.DeliveryTickets.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetByErpCutOrderIdAsync(string erpCutOrderId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.ErpCutOrderId == erpCutOrderId && !d.IsDeleted)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetBySplitGroupIdAsync(Guid splitGroupId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.SplitGroupId == splitGroupId && !d.IsDeleted)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.CutOrderId == cutOrderId && !d.IsDeleted)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetAllByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.CutOrderId == cutOrderId)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetByWeighingSessionIdAsync(Guid weighingSessionId, CancellationToken ct)
    {
        var list = await _context.DeliveryTickets
            .Where(d => d.WeighingSessionId == weighingSessionId && !d.IsDeleted)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<DeliveryTicket>> GetBySyncStatusAsync(SyncStatus syncStatus, int take, CancellationToken ct)
    {
        var stationCode = await StationScopeQuery.GetCurrentStationCodeAsync(_context, ct);
        var list = await _context.DeliveryTickets
            .Where(d => d.StationCode == stationCode && d.SyncStatus == syncStatus && !d.IsDeleted)
            .OrderBy(d => d.UpdatedAt ?? d.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<DeliveryTicket?> GetPrimaryByCutOrderIdAsync(Guid cutOrderId, CancellationToken ct)
    {
        return await _context.DeliveryTickets
            .Where(d => d.CutOrderId == cutOrderId && d.RecordRole == DeliveryTicketRecordRoles.Normal && !d.IsDeleted)
            .OrderBy(d => d.SplitSequence ?? 0)
            .ThenByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string?> ResolveDeliveryTicketStationCodeAsync(DeliveryTicket ticket, CancellationToken ct)
    {
        if (ticket.WeighingSessionId.HasValue)
        {
            var sessionStationCode = await _context.WeighingSessions.AsNoTracking()
                .Where(x => x.Id == ticket.WeighingSessionId.Value)
                .Select(x => x.StationCode)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(sessionStationCode))
            {
                return sessionStationCode;
            }
        }

        return await _context.CutOrders.AsNoTracking()
            .Where(x => x.Id == ticket.CutOrderId)
            .Select(x => x.StationCode)
            .FirstOrDefaultAsync(ct);
    }
}


