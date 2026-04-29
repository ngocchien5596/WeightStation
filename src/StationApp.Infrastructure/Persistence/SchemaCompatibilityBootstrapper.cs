using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace StationApp.Infrastructure.Persistence;

public static class SchemaCompatibilityBootstrapper
{
    private static readonly IReadOnlyList<ColumnPatch> VehicleRegistrationColumnPatches =
    [
        new("CutOrderCode", "nvarchar(100) NULL"),
        new("OrderCode", "nvarchar(100) NULL"),
        new("LotNo", "nvarchar(100) NULL"),
        new("RepresentativeName", "nvarchar(150) NULL"),
        new("ConsumptionPlace", "nvarchar(255) NULL"),
        new("LoadingPlace", "nvarchar(255) NULL"),
        new("SealNo", "nvarchar(100) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> WeighTicketColumnPatches =
    [
        new("IsDeleted", "bit NOT NULL CONSTRAINT [DF_weigh_tickets_is_deleted_bootstrap] DEFAULT ((0))"),
        new("DeletedAt", "datetime2 NULL"),
        new("DeletedBy", "nvarchar(100) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> DeliveryTicketColumnPatches =
    [
        new("IsDeleted", "bit NOT NULL CONSTRAINT [DF_delivery_tickets_is_deleted_bootstrap] DEFAULT ((0))"),
        new("DeletedAt", "datetime2 NULL"),
        new("DeletedBy", "nvarchar(100) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> UserColumnPatches =
    [
        new("PasswordHash", "nvarchar(255) NULL"),
        new("LastLoginAt", "datetime2 NULL"),
        new("CreatedBy", "nvarchar(100) NULL"),
        new("UpdatedBy", "nvarchar(100) NULL")
    ];

    public static async Task EnsureAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        await EnsureTableColumnsAsync(db, logger, "vehicle_registrations", VehicleRegistrationColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "weigh_tickets", WeighTicketColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "delivery_tickets", DeliveryTicketColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "users", UserColumnPatches, ct);
    }

    private static async Task EnsureTableColumnsAsync(
        StationDbContext db,
        ILogger? logger,
        string tableName,
        IReadOnlyList<ColumnPatch> patches,
        CancellationToken ct)
    {
        foreach (var patch in patches)
        {
            var sql = $@"
IF COL_LENGTH('{tableName}', '{patch.ColumnName}') IS NULL
BEGIN
    ALTER TABLE [{tableName}] ADD [{patch.ColumnName}] {patch.SqlDefinition};
END";

            await db.Database.ExecuteSqlRawAsync(sql, ct);
            logger?.LogDebug(
                "Schema compatibility check completed for {TableName}.{ColumnName}.",
                tableName,
                patch.ColumnName);
        }
    }

    private sealed record ColumnPatch(string ColumnName, string SqlDefinition);
}
