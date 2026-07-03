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
