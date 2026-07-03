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
