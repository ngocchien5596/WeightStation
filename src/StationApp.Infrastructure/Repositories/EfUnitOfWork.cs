using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StationApp.Application.Interfaces;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Repositories;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly StationDbContext _db;
    public EfUnitOfWork(StationDbContext db) => _db = db;

    public async Task<int> SaveChangesAsync(CancellationToken ct)
        => await _db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await action(ct);
                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}
