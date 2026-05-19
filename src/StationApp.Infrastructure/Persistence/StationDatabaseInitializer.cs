using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace StationApp.Infrastructure.Persistence;

public static class StationDatabaseInitializer
{
    public static async Task InitializeAsync(
        StationDbContext db,
        ILoggerFactory? loggerFactory,
        CancellationToken ct,
        bool runBackfill = true,
        bool runPrimaryTicketRepair = true)
    {
        var logger = loggerFactory?.CreateLogger("SchemaCompatibilityBootstrapper");
        await SchemaCompatibilityBootstrapper.EnsureAsync(db, logger, ct);
        await db.Database.MigrateAsync(ct);
        await SchemaCompatibilityBootstrapper.EnsureAsync(db, logger, ct);

        if (runBackfill)
        {
            var backfill = new BackfillCutOrdersService(db);
            await backfill.ExecuteAsync(ct);
        }

        if (runPrimaryTicketRepair)
        {
            var repair = new WeighTicketPrimaryRepairService(db);
            await repair.ExecuteAsync(ct);
        }
    }
}
