using Microsoft.EntityFrameworkCore;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public sealed class IncomingSeedVehicleRepository : IIncomingSeedVehicleRepository
{
    private const string Qn01StationCode = "QN01";
    private readonly StationDbContext _context;

    public IncomingSeedVehicleRepository(StationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<IncomingSeedVehicleListItem>> GetQn01Async(CancellationToken ct)
    {
        var query =
            from seed in _context.IncomingSeedVehicles.AsNoTracking()
            join customer in _context.Customers.AsNoTracking()
                on new { seed.StationCode, seed.CustomerCode } equals new { customer.StationCode, customer.CustomerCode }
            join product in _context.Products.AsNoTracking()
                on new { seed.StationCode, seed.ProductCode } equals new { product.StationCode, product.ProductCode }
            where seed.StationCode == Qn01StationCode
            orderby seed.SortOrder, customer.CustomerName, product.ProductName
            select new IncomingSeedVehicleListItem(
                seed.Id,
                customer.CustomerCode,
                customer.CustomerName,
                product.ProductCode,
                product.ProductName,
                product.ProductType,
                seed.SortOrder,
                seed.IsActive,
                seed.CreatedAt,
                seed.CreatedBy,
                seed.UpdatedAt,
                seed.UpdatedBy);

        return await query.ToListAsync(ct);
    }

    public async Task<IncomingSeedVehicle?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _context.IncomingSeedVehicles.FirstOrDefaultAsync(x => x.Id == id && x.StationCode == Qn01StationCode, ct);

    public async Task<IncomingSeedVehicle?> FindActiveDuplicateQn01Async(string customerCode, string productCode, Guid? excludeId, CancellationToken ct)
    {
        var query = _context.IncomingSeedVehicles
            .Where(x => x.StationCode == Qn01StationCode
                && x.IsActive
                && x.CustomerCode == customerCode
                && x.ProductCode == productCode);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetNextSortOrderQn01Async(CancellationToken ct)
    {
        var max = await _context.IncomingSeedVehicles
            .Where(x => x.StationCode == Qn01StationCode && x.IsActive)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        return (max ?? 0) + 10;
    }

    public async Task AddAsync(IncomingSeedVehicle seedVehicle, CancellationToken ct)
    {
        await _context.IncomingSeedVehicles.AddAsync(seedVehicle, ct);
    }

    public Task UpdateAsync(IncomingSeedVehicle seedVehicle, CancellationToken ct)
    {
        if (_context.Entry(seedVehicle).State == EntityState.Detached)
        {
            _context.IncomingSeedVehicles.Update(seedVehicle);
        }

        return Task.CompletedTask;
    }
}
