using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace StationApp.Infrastructure.Persistence;

public static class SchemaCompatibilityBootstrapper
{
    private static readonly IReadOnlyList<ColumnPatch> CutOrderColumnPatches =
    [
        new("WeighingSessionId", "uniqueidentifier NULL"),
        new("ProductType", "nvarchar(30) NULL"),
        new("OrderCode", "nvarchar(100) NULL"),
        new("LotNo", "nvarchar(100) NULL"),
        new("RepresentativeName", "nvarchar(150) NULL"),
        new("ConsumptionPlace", "nvarchar(255) NULL"),
        new("LoadingPlace", "nvarchar(255) NULL"),
        new("SealNo", "nvarchar(100) NULL"),
        new("CarryForwardWeight1", "decimal(18,3) NULL"),
        new("CarryForwardWeight1Time", "datetime2 NULL"),
        new("ErpRegistrationCode", "nvarchar(100) NULL"),
        new("IsDeleted", "bit NOT NULL CONSTRAINT [DF_cut_orders_is_deleted_bootstrap] DEFAULT ((0))"),
        new("DeletedAt", "datetime2 NULL"),
        new("DeletedBy", "nvarchar(100) NULL")
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

    private static readonly IReadOnlyList<ColumnPatch> ProductColumnPatches =
    [
        new("ProductType", "nvarchar(30) NULL")
    ];

    public static async Task EnsureAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        await EnsureCutOrderSchemaAsync(db, logger, ct);
        await EnsureTableColumnsAsync(db, logger, "cut_orders", CutOrderColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "weigh_tickets", WeighTicketColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "delivery_tickets", DeliveryTicketColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "users", UserColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "products", ProductColumnPatches, ct);
        await EnsureWeighingSessionTablesAsync(db, logger, ct);
        await EnsurePrintTemplateProfileTableAsync(db, logger, ct);
    }

    private static async Task EnsureCutOrderSchemaAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'[cut_orders]', N'U') IS NULL AND OBJECT_ID(N'[vehicle_registrations]', N'U') IS NOT NULL
BEGIN
    EXEC sp_rename N'vehicle_registrations', N'cut_orders';
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_vehicle_registrations_erp_vehicle_registration_id' AND object_id = OBJECT_ID(N'[cut_orders]'))
BEGIN
    DROP INDEX [UX_vehicle_registrations_erp_vehicle_registration_id] ON [cut_orders];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_cut_orders_erp_cut_order_id' AND object_id = OBJECT_ID(N'[cut_orders]'))
BEGIN
    DROP INDEX [UX_cut_orders_erp_cut_order_id] ON [cut_orders];
END

IF COL_LENGTH('cut_orders', 'ErpCutOrderId') IS NULL AND COL_LENGTH('cut_orders', 'ErpVehicleRegistrationId') IS NOT NULL
BEGIN
    EXEC sp_rename N'cut_orders.ErpVehicleRegistrationId', N'ErpCutOrderId', N'COLUMN';
END

IF COL_LENGTH('cut_orders', 'CutOrderSource') IS NULL AND COL_LENGTH('cut_orders', 'RegistrationSource') IS NOT NULL
BEGIN
    EXEC sp_rename N'cut_orders.RegistrationSource', N'CutOrderSource', N'COLUMN';
END

IF COL_LENGTH('cut_orders', 'CutOrderStatus') IS NULL AND COL_LENGTH('cut_orders', 'RegistrationStatus') IS NOT NULL
BEGIN
    EXEC sp_rename N'cut_orders.RegistrationStatus', N'CutOrderStatus', N'COLUMN';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_erp_cut_order_id_deleted' AND object_id = OBJECT_ID(N'[cut_orders]'))
BEGIN
    CREATE INDEX [IX_cut_orders_erp_cut_order_id_deleted]
    ON [cut_orders]([ErpCutOrderId], [IsDeleted]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_erp_registration_code_deleted' AND object_id = OBJECT_ID(N'[cut_orders]'))
BEGIN
    CREATE INDEX [IX_cut_orders_erp_registration_code_deleted]
    ON [cut_orders]([ErpRegistrationCode], [IsDeleted]);
END

IF OBJECT_ID(N'[dbo].[TR_cut_orders_enforce_active_erp_cut_order_id]', N'TR') IS NULL
BEGIN
    EXEC(N'CREATE TRIGGER [dbo].[TR_cut_orders_enforce_active_erp_cut_order_id]
    ON [dbo].[cut_orders]
    AFTER INSERT, UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
    END');
END

EXEC(N'ALTER TRIGGER [dbo].[TR_cut_orders_enforce_active_erp_cut_order_id]
ON [dbo].[cut_orders]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT [ErpCutOrderId]
        FROM [dbo].[cut_orders]
        WHERE [ErpCutOrderId] IS NOT NULL
          AND ISNULL([IsDeleted], 0) = 0
        GROUP BY [ErpCutOrderId]
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51001, N''Da ton tai cat lenh active khac cung ErpCutOrderId.'', 1;
    END
END');

IF COL_LENGTH('weigh_tickets', 'CutOrderId') IS NULL AND COL_LENGTH('weigh_tickets', 'VehicleRegistrationId') IS NOT NULL
BEGIN
    EXEC sp_rename N'weigh_tickets.VehicleRegistrationId', N'CutOrderId', N'COLUMN';
END

IF COL_LENGTH('weigh_tickets', 'ErpCutOrderId') IS NULL AND COL_LENGTH('weigh_tickets', 'ErpVehicleRegistrationId') IS NOT NULL
BEGIN
    EXEC sp_rename N'weigh_tickets.ErpVehicleRegistrationId', N'ErpCutOrderId', N'COLUMN';
END

IF COL_LENGTH('delivery_tickets', 'CutOrderId') IS NULL AND COL_LENGTH('delivery_tickets', 'VehicleRegistrationId') IS NOT NULL
BEGIN
    EXEC sp_rename N'delivery_tickets.VehicleRegistrationId', N'CutOrderId', N'COLUMN';
END

IF COL_LENGTH('delivery_tickets', 'ErpCutOrderId') IS NULL AND COL_LENGTH('delivery_tickets', 'ErpVehicleRegistrationId') IS NOT NULL
BEGIN
    EXEC sp_rename N'delivery_tickets.ErpVehicleRegistrationId', N'ErpCutOrderId', N'COLUMN';
END

IF COL_LENGTH('weighing_session_lines', 'CutOrderId') IS NULL AND COL_LENGTH('weighing_session_lines', 'VehicleRegistrationId') IS NOT NULL
BEGIN
    EXEC sp_rename N'weighing_session_lines.VehicleRegistrationId', N'CutOrderId', N'COLUMN';
END
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility rename completed for cut_orders.");
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
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_weighing_sessions_is_deleted_bootstrap] DEFAULT ((0)),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [HasPrintedMasterWeighTicket] bit NOT NULL CONSTRAINT [DF_weighing_sessions_has_printed_master_bootstrap] DEFAULT ((0)),
        [UseActualWeightForBaggedCutOrders] bit NOT NULL CONSTRAINT [DF_weighing_sessions_bagged_actual_weight_bootstrap] DEFAULT ((0)),
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
        [CutOrderId] uniqueidentifier NOT NULL,
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
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_weighing_session_lines_is_deleted_bootstrap] DEFAULT ((0)),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
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

IF COL_LENGTH(N'[weighing_sessions]', N'UseActualWeightForBaggedCutOrders') IS NULL
BEGIN
    ALTER TABLE [weighing_sessions]
        ADD [UseActualWeightForBaggedCutOrders] bit NOT NULL
            CONSTRAINT [DF_weighing_sessions_bagged_actual_weight_bootstrap] DEFAULT ((0));
END

IF COL_LENGTH(N'[weighing_sessions]', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [weighing_sessions]
        ADD [IsDeleted] bit NOT NULL
            CONSTRAINT [DF_weighing_sessions_is_deleted_bootstrap] DEFAULT ((0));
END

IF COL_LENGTH(N'[weighing_sessions]', N'DeletedAt') IS NULL
BEGIN
    ALTER TABLE [weighing_sessions] ADD [DeletedAt] datetime2 NULL;
END

IF COL_LENGTH(N'[weighing_sessions]', N'DeletedBy') IS NULL
BEGIN
    ALTER TABLE [weighing_sessions] ADD [DeletedBy] nvarchar(100) NULL;
END

IF COL_LENGTH(N'[weighing_session_lines]', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [weighing_session_lines]
        ADD [IsDeleted] bit NOT NULL
            CONSTRAINT [DF_weighing_session_lines_is_deleted_bootstrap] DEFAULT ((0));
END

IF COL_LENGTH(N'[weighing_session_lines]', N'DeletedAt') IS NULL
BEGIN
    ALTER TABLE [weighing_session_lines] ADD [DeletedAt] datetime2 NULL;
END

IF COL_LENGTH(N'[weighing_session_lines]', N'DeletedBy') IS NULL
BEGIN
    ALTER TABLE [weighing_session_lines] ADD [DeletedBy] nvarchar(100) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_lines_session_id' AND object_id = OBJECT_ID(N'[weighing_session_lines]'))
BEGIN
    CREATE INDEX [IX_weighing_session_lines_session_id] ON [weighing_session_lines]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_lines_registration_id' AND object_id = OBJECT_ID(N'[weighing_session_lines]'))
BEGIN
    CREATE INDEX [IX_weighing_session_lines_registration_id] ON [weighing_session_lines]([CutOrderId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_weighing_session_lines_session_registration' AND object_id = OBJECT_ID(N'[weighing_session_lines]'))
BEGIN
    CREATE UNIQUE INDEX [UX_weighing_session_lines_session_registration] ON [weighing_session_lines]([WeighingSessionId], [CutOrderId]);
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_weighing_session_id' AND object_id = OBJECT_ID(N'[cut_orders]'))
BEGIN
    CREATE INDEX [IX_cut_orders_weighing_session_id] ON [cut_orders]([WeighingSessionId]);
END

UPDATE [weighing_sessions]
SET [SessionStatus] = N'READY_TO_COMPLETE'
WHERE [SessionStatus] = N'READY_TO_PRINT';
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility check completed for weighing session tables and indexes.");
    }

    private static async Task EnsurePrintTemplateProfileTableAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'[print_template_profiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [print_template_profiles](
        [Id] uniqueidentifier NOT NULL,
        [TemplateKind] nvarchar(30) NOT NULL,
        [ProfileKey] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(150) NOT NULL,
        [IsDefault] bit NOT NULL CONSTRAINT [DF_print_template_profiles_is_default_bootstrap] DEFAULT ((0)),
        [OffsetXmm] decimal(18,3) NOT NULL CONSTRAINT [DF_print_template_profiles_offset_x_bootstrap] DEFAULT ((0)),
        [OffsetYmm] decimal(18,3) NOT NULL CONSTRAINT [DF_print_template_profiles_offset_y_bootstrap] DEFAULT ((0)),
        [TemplateVersion] int NOT NULL CONSTRAINT [DF_print_template_profiles_version_bootstrap] DEFAULT ((1)),
        [LayoutJson] nvarchar(max) NOT NULL CONSTRAINT [DF_print_template_profiles_layout_bootstrap] DEFAULT (N'[]'),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_print_template_profiles] PRIMARY KEY ([Id])
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_print_template_profiles_kind_key' AND object_id = OBJECT_ID(N'[print_template_profiles]'))
BEGIN
    CREATE UNIQUE INDEX [UX_print_template_profiles_kind_key]
    ON [print_template_profiles]([TemplateKind], [ProfileKey]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_print_template_profiles_kind_default' AND object_id = OBJECT_ID(N'[print_template_profiles]'))
BEGIN
    CREATE INDEX [IX_print_template_profiles_kind_default]
    ON [print_template_profiles]([TemplateKind], [IsDefault]);
END
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility check completed for print template profile table and indexes.");
    }

    private sealed record ColumnPatch(string ColumnName, string SqlDefinition);
}


