using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace StationApp.Infrastructure.Persistence;

public static class SchemaCompatibilityBootstrapper
{
    private static readonly IReadOnlyList<ColumnPatch> VehicleRegistrationColumnPatches =
    [
        new("WeighingSessionId", "uniqueidentifier NULL"),
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
        new("WeighingSessionId", "uniqueidentifier NULL"),
        new("IsDeleted", "bit NOT NULL CONSTRAINT [DF_weigh_tickets_is_deleted_bootstrap] DEFAULT ((0))"),
        new("DeletedAt", "datetime2 NULL"),
        new("DeletedBy", "nvarchar(100) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> DeliveryTicketColumnPatches =
    [
        new("WeighingSessionId", "uniqueidentifier NULL"),
        new("WeighingSessionLineId", "uniqueidentifier NULL"),
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
        await EnsureWeighingSessionTablesAsync(db, logger, ct);
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

    private static async Task EnsureWeighingSessionTablesAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'[weighing_sessions]', N'U') IS NULL
BEGIN
    CREATE TABLE [weighing_sessions](
        [Id] uniqueidentifier NOT NULL,
        [SessionNo] nvarchar(50) NOT NULL,
        [TransactionType] nvarchar(30) NOT NULL,
        [VehiclePlate] nvarchar(30) NOT NULL,
        [MoocNumber] nvarchar(30) NULL,
        [DriverName] nvarchar(150) NULL,
        [Weight1] decimal(18,3) NULL,
        [Weight2] decimal(18,3) NULL,
        [NetWeight] decimal(18,3) NULL,
        [Weight1Time] datetime2 NULL,
        [Weight2Time] datetime2 NULL,
        [SessionStatus] nvarchar(30) NOT NULL,
        [IsCancelled] bit NOT NULL CONSTRAINT [DF_weighing_sessions_is_cancelled_bootstrap] DEFAULT ((0)),
        [HasPrintedMasterWeighTicket] bit NOT NULL CONSTRAINT [DF_weighing_sessions_has_printed_master_bootstrap] DEFAULT ((0)),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_weighing_sessions] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NULL
BEGIN
    CREATE TABLE [weighing_session_lines](
        [Id] uniqueidentifier NOT NULL,
        [WeighingSessionId] uniqueidentifier NOT NULL,
        [VehicleRegistrationId] uniqueidentifier NOT NULL,
        [SequenceNo] int NOT NULL,
        [CustomerCode] nvarchar(50) NULL,
        [CustomerName] nvarchar(255) NULL,
        [DistributorCode] nvarchar(50) NULL,
        [DistributorName] nvarchar(255) NULL,
        [ProductCode] nvarchar(50) NULL,
        [ProductName] nvarchar(255) NULL,
        [PlannedWeight] decimal(18,3) NULL,
        [PlannedBagCount] int NULL,
        [ActualAllocatedWeight] decimal(18,3) NULL,
        [ActualAllocatedBagCount] int NULL,
        [LineStatus] nvarchar(30) NOT NULL,
        [HasPrintedDeliveryTicket] bit NOT NULL CONSTRAINT [DF_weighing_session_lines_has_printed_delivery_bootstrap] DEFAULT ((0)),
        [DeliveryTicketId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_weighing_session_lines] PRIMARY KEY ([Id])
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_weighing_sessions_session_no' AND object_id = OBJECT_ID(N'[weighing_sessions]'))
BEGIN
    CREATE UNIQUE INDEX [UX_weighing_sessions_session_no] ON [weighing_sessions]([SessionNo]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_sessions_vehicle_plate' AND object_id = OBJECT_ID(N'[weighing_sessions]'))
BEGIN
    CREATE INDEX [IX_weighing_sessions_vehicle_plate] ON [weighing_sessions]([VehiclePlate]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_sessions_status' AND object_id = OBJECT_ID(N'[weighing_sessions]'))
BEGIN
    CREATE INDEX [IX_weighing_sessions_status] ON [weighing_sessions]([SessionStatus]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_sessions_created_at' AND object_id = OBJECT_ID(N'[weighing_sessions]'))
BEGIN
    CREATE INDEX [IX_weighing_sessions_created_at] ON [weighing_sessions]([CreatedAt]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_lines_session_id' AND object_id = OBJECT_ID(N'[weighing_session_lines]'))
BEGIN
    CREATE INDEX [IX_weighing_session_lines_session_id] ON [weighing_session_lines]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_lines_registration_id' AND object_id = OBJECT_ID(N'[weighing_session_lines]'))
BEGIN
    CREATE INDEX [IX_weighing_session_lines_registration_id] ON [weighing_session_lines]([VehicleRegistrationId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_weighing_session_lines_session_registration' AND object_id = OBJECT_ID(N'[weighing_session_lines]'))
BEGIN
    CREATE UNIQUE INDEX [UX_weighing_session_lines_session_registration] ON [weighing_session_lines]([WeighingSessionId], [VehicleRegistrationId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weigh_tickets_weighing_session_id' AND object_id = OBJECT_ID(N'[weigh_tickets]'))
BEGIN
    CREATE INDEX [IX_weigh_tickets_weighing_session_id] ON [weigh_tickets]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_delivery_tickets_weighing_session_id' AND object_id = OBJECT_ID(N'[delivery_tickets]'))
BEGIN
    CREATE INDEX [IX_delivery_tickets_weighing_session_id] ON [delivery_tickets]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_delivery_tickets_weighing_session_line_id' AND object_id = OBJECT_ID(N'[delivery_tickets]'))
BEGIN
    CREATE INDEX [IX_delivery_tickets_weighing_session_line_id] ON [delivery_tickets]([WeighingSessionLineId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicle_registrations_weighing_session_id' AND object_id = OBJECT_ID(N'[vehicle_registrations]'))
BEGIN
    CREATE INDEX [IX_vehicle_registrations_weighing_session_id] ON [vehicle_registrations]([WeighingSessionId]);
END

UPDATE [weighing_sessions]
SET [SessionStatus] = N'READY_TO_COMPLETE'
WHERE [SessionStatus] = N'READY_TO_PRINT';
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility check completed for weighing session tables and indexes.");
    }

    private sealed record ColumnPatch(string ColumnName, string SqlDefinition);
}
