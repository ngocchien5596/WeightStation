using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;

namespace StationApp.Infrastructure.Persistence;

public static class StationDatabaseInitializer
{
    public static async Task InitializeAsync(
        StationDbContext db,
        ILoggerFactory? loggerFactory,
        CancellationToken ct,
        bool runBackfill = true,
        bool runPrimaryTicketRepair = true,
        bool deploySqlObjects = false)
    {
        var logger = loggerFactory?.CreateLogger("SchemaCompatibilityBootstrapper");
        
        bool dbExists = await db.Database.CanConnectAsync(ct);
        if (!dbExists)
        {
            logger?.LogInformation("Database does not exist. Creating empty database...");
            var databaseCreator = db.Database.GetService<IRelationalDatabaseCreator>();
            await databaseCreator.CreateAsync(ct);
        }

        await ValidateSqlServerCompatibilityAsync(db, logger, ct);
        await SchemaCompatibilityBootstrapper.EnsureAsync(db, logger, ct);
        await db.Database.MigrateAsync(ct);
        await SchemaCompatibilityBootstrapper.EnsureAsync(db, logger, ct);

        if (deploySqlObjects)
        {
            var sqlObjectLogger = loggerFactory?.CreateLogger("SqlObjectDeploymentService");
            await SqlObjectDeploymentService.DeployRequiredObjectsAsync(db, sqlObjectLogger, ct);
        }

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

    private static async Task ValidateSqlServerCompatibilityAsync(
        StationDbContext db,
        ILogger? logger,
        CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
SELECT
    CAST(SERVERPROPERTY('ProductMajorVersion') AS int) AS ProductMajorVersion,
    CAST(DATABASEPROPERTYEX(DB_NAME(), 'CompatibilityLevel') AS int) AS CompatibilityLevel
""";

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return;
            }

            var productMajorVersion = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            var compatibilityLevel = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);

            logger?.LogInformation(
                "SQL Server compatibility check: ProductMajorVersion={ProductMajorVersion}, CompatibilityLevel={CompatibilityLevel}.",
                productMajorVersion,
                compatibilityLevel);

            if (productMajorVersion > 0 && productMajorVersion < 11)
            {
                throw new InvalidOperationException(
                    $"SQL Server version {productMajorVersion} is not supported. Minimum supported version is SQL Server 2012 (11.x).");
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
