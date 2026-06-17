using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace StationApp.Infrastructure.Persistence;

public static class SchemaCompatibilityBootstrapper
{
    private static readonly IReadOnlyList<ColumnPatch> CutOrderColumnPatches =
    [
        new("StationCode", "nvarchar(50) NULL"),
        new("WeighingSessionId", "uniqueidentifier NULL"),
        new("ProductType", "nvarchar(30) NULL"),
        new("OrderCode", "nvarchar(100) NULL"),
        new("LotNo", "nvarchar(100) NULL"),
        new("RepresentativeName", "nvarchar(150) NULL"),
        new("Market", "nvarchar(255) NULL"),
        new("ConsumptionPlace", "nvarchar(255) NULL"),
        new("LoadingPlace", "nvarchar(255) NULL"),
        new("SealNo", "nvarchar(100) NULL"),
        new("CarryForwardWeight1", "decimal(18,3) NULL"),
        new("CarryForwardWeight1Time", "datetime2 NULL"),
        new("IsExportScale", "bit NOT NULL CONSTRAINT [DF_cut_orders_is_export_scale_bootstrap] DEFAULT ((0))"),
        new("ExportFinalizedWeight", "decimal(18,3) NULL"),
        new("ExportFinalizedAt", "datetime2 NULL"),
        new("ExportFinalizedBy", "nvarchar(100) NULL"),
        new("ExportStartedAt", "datetime2 NULL"),
        new("ExportStartedBy", "nvarchar(100) NULL"),
        new("ErpExportCompleted", "bit NOT NULL CONSTRAINT [DF_cut_orders_erp_export_completed_bootstrap] DEFAULT ((0))"),
        new("IsTemporaryExport", "bit NOT NULL CONSTRAINT [DF_cut_orders_is_temporary_export_bootstrap] DEFAULT ((0))"),
        new("MappedRealCutOrderId", "uniqueidentifier NULL"),
        new("MappedTemporaryCutOrderId", "uniqueidentifier NULL"),
        new("TemporaryExportCreatedReason", "nvarchar(50) NULL"),
        new("TemporaryExportDisplayCode", "nvarchar(100) NULL"),
        new("TemporaryExportSourceErpCutOrderId", "nvarchar(100) NULL"),
        new("MappedAt", "datetime2 NULL"),
        new("MappedBy", "nvarchar(100) NULL"),
        new("ErpRegistrationCode", "nvarchar(100) NULL"),
        new("IsDeleted", "bit NOT NULL CONSTRAINT [DF_cut_orders_is_deleted_bootstrap] DEFAULT ((0))"),
        new("DeletedAt", "datetime2 NULL"),
        new("DeletedBy", "nvarchar(100) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> WeighTicketColumnPatches =
    [
        new("StationCode", "nvarchar(50) NULL"),
        new("WeighingSessionId", "uniqueidentifier NULL"),
        new("IsDeleted", "bit NOT NULL CONSTRAINT [DF_weigh_tickets_is_deleted_bootstrap] DEFAULT ((0))"),
        new("DeletedAt", "datetime2 NULL"),
        new("DeletedBy", "nvarchar(100) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> DeliveryTicketColumnPatches =
    [
        new("StationCode", "nvarchar(50) NULL"),
        new("SyncStatus", "nvarchar(40) NOT NULL CONSTRAINT [DF_delivery_tickets_sync_status_bootstrap] DEFAULT (N'SYNC_QUEUED')"),
        new("WeighingSessionId", "uniqueidentifier NULL"),
        new("WeighingSessionLineId", "uniqueidentifier NULL"),
        new("IsDeleted", "bit NOT NULL CONSTRAINT [DF_delivery_tickets_is_deleted_bootstrap] DEFAULT ((0))"),
        new("DeletedAt", "datetime2 NULL"),
        new("DeletedBy", "nvarchar(100) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> SyncOutboxColumnPatches =
    [
        new("StationCode", "nvarchar(50) NULL")
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
        new("ProductType", "nvarchar(30) NULL"),
        new("StationCode", "nvarchar(50) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> VehicleColumnPatches =
    [
        new("IsInternalVehicle", "bit NOT NULL CONSTRAINT [DF_vehicles_is_internal_vehicle_bootstrap] DEFAULT ((0))"),
        new("StandardTareSource", "nvarchar(50) NULL"),
        new("StandardTareUpdatedAt", "datetime2 NULL"),
        new("StandardTareUpdatedBy", "nvarchar(100) NULL"),
        new("StationCode", "nvarchar(50) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> CustomerColumnPatches =
    [
        new("StationCode", "nvarchar(50) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> WeighTicketCrusherColumnPatches =
    [
        new("WeighingMode", "nvarchar(40) NOT NULL CONSTRAINT [DF_weigh_tickets_weighing_mode_bootstrap] DEFAULT (N'TWO_WEIGH')"),
        new("InternalVehicleNo", "nvarchar(30) NULL"),
        new("StandardTareWeightSnapshot", "decimal(18,3) NULL"),
        new("StandardTareSourceSnapshot", "nvarchar(50) NULL"),
        new("StandardTareVehicleId", "uniqueidentifier NULL"),
        new("NetWeightCalculationMode", "nvarchar(50) NULL CONSTRAINT [DF_weigh_tickets_net_calc_mode_bootstrap] DEFAULT (N'WEIGHT2_DIFF')")
    ];

    private static readonly IReadOnlyList<ColumnPatch> WeighingSessionColumnPatches =
    [
        new("StationCode", "nvarchar(50) NULL"),
        new("SyncStatus", "nvarchar(30) NOT NULL CONSTRAINT [DF_weighing_sessions_sync_status_bootstrap] DEFAULT (N'SYNC_QUEUED')"),
        new("LastSyncAttemptAt", "datetime2 NULL"),
        new("LastSyncError", "nvarchar(1000) NULL"),
        new("WeighingMode", "nvarchar(40) NOT NULL CONSTRAINT [DF_weighing_sessions_weighing_mode_bootstrap] DEFAULT (N'TWO_WEIGH')"),
        new("InternalVehicleNo", "nvarchar(30) NULL"),
        new("StandardTareWeightSnapshot", "decimal(18,3) NULL"),
        new("StandardTareSourceSnapshot", "nvarchar(50) NULL"),
        new("StandardTareVehicleId", "uniqueidentifier NULL"),
        new("NetWeightCalculationMode", "nvarchar(50) NULL CONSTRAINT [DF_weighing_sessions_net_calc_mode_bootstrap] DEFAULT (N'WEIGHT2_DIFF')"),
        // Crusher Weighing: Product and Customer Information
        new("ProductCode", "nvarchar(50) NULL"),
        new("ProductName", "nvarchar(255) NULL"),
        new("CustomerCode", "nvarchar(50) NULL"),
        new("CustomerName", "nvarchar(255) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> WeighingSessionLineColumnPatches =
    [
        new("StationCode", "nvarchar(50) NULL"),
        new("SyncStatus", "nvarchar(30) NOT NULL CONSTRAINT [DF_weighing_session_lines_sync_status_bootstrap] DEFAULT (N'SYNC_QUEUED')"),
        new("LastSyncAttemptAt", "datetime2 NULL"),
        new("LastSyncError", "nvarchar(1000) NULL")
    ];

    private static readonly IReadOnlyList<ColumnPatch> WeighingSessionImageColumnPatches =
    [
        new("StationCode", "nvarchar(50) NULL"),
        new("Id", "uniqueidentifier NOT NULL CONSTRAINT [DF_weighing_session_images_id_bootstrap] DEFAULT (newid())"),
        new("WeighingSessionId", "uniqueidentifier NOT NULL"),
        new("CaptureStage", "nvarchar(20) NOT NULL CONSTRAINT [DF_weighing_session_images_stage_bootstrap] DEFAULT (N'WEIGHT1')"),
        new("CameraCode", "nvarchar(20) NOT NULL CONSTRAINT [DF_weighing_session_images_camera_code_bootstrap] DEFAULT (N'CAM1')"),
        new("CameraName", "nvarchar(100) NOT NULL CONSTRAINT [DF_weighing_session_images_camera_name_bootstrap] DEFAULT (N'Camera 1')"),
        new("RtspUrlSnapshot", "nvarchar(1000) NULL"),
        new("ImageFormat", "nvarchar(20) NOT NULL CONSTRAINT [DF_weighing_session_images_format_bootstrap] DEFAULT (N'jpg')"),
        new("ImageBytes", "varbinary(max) NOT NULL CONSTRAINT [DF_weighing_session_images_bytes_bootstrap] DEFAULT (0x)"),
        new("FileSizeBytes", "bigint NOT NULL CONSTRAINT [DF_weighing_session_images_file_size_bootstrap] DEFAULT ((0))"),
        new("CapturedAt", "datetime2 NOT NULL CONSTRAINT [DF_weighing_session_images_captured_at_bootstrap] DEFAULT (sysdatetime())"),
        new("CapturedBy", "nvarchar(100) NOT NULL CONSTRAINT [DF_weighing_session_images_captured_by_bootstrap] DEFAULT (N'SYSTEM')"),
        new("SyncStatus", "nvarchar(20) NOT NULL CONSTRAINT [DF_weighing_session_images_sync_status_bootstrap] DEFAULT (N'PENDING')"),
        new("LastSyncAttemptAt", "datetime2 NULL"),
        new("LastSyncSuccessAt", "datetime2 NULL"),
        new("LastSyncError", "nvarchar(1000) NULL"),
        new("RetryCount", "int NOT NULL CONSTRAINT [DF_weighing_session_images_retry_count_bootstrap] DEFAULT ((0))"),
        new("IsDeleted", "bit NOT NULL CONSTRAINT [DF_weighing_session_images_is_deleted_bootstrap] DEFAULT ((0))"),
        new("DeletedAt", "datetime2 NULL"),
        new("DeletedBy", "nvarchar(100) NULL"),
        new("CreatedAt", "datetime2 NOT NULL CONSTRAINT [DF_weighing_session_images_created_at_bootstrap] DEFAULT (sysdatetime())"),
        new("CreatedBy", "nvarchar(100) NOT NULL CONSTRAINT [DF_weighing_session_images_created_by_bootstrap] DEFAULT (N'SYSTEM')"),
        new("UpdatedAt", "datetime2 NULL"),
        new("UpdatedBy", "nvarchar(100) NULL")
    ];

    public static async Task EnsureAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        await EnsureCutOrderSchemaAsync(db, logger, ct);
        await EnsureTableColumnsAsync(db, logger, "cut_orders", CutOrderColumnPatches, ct);
        await EnsureCutOrderIndexesAsync(db, logger, ct);
        await EnsureTableColumnsAsync(db, logger, "weigh_tickets", WeighTicketColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "delivery_tickets", DeliveryTicketColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "sync_outbox", SyncOutboxColumnPatches, ct);
        await EnsureDeliveryTicketSyncStatusSchemaAsync(db, logger, ct);
        await EnsureTableColumnsAsync(db, logger, "users", UserColumnPatches, ct);
        await EnsureStationAccessTablesAsync(db, logger, ct);
        await EnsureTableColumnsAsync(db, logger, "products", ProductColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "vehicles", VehicleColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "customers", CustomerColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "weigh_tickets", WeighTicketCrusherColumnPatches, ct);
        await EnsureWeighingSessionTablesAsync(db, logger, ct);
        await EnsureTableColumnsAsync(db, logger, "weighing_sessions", WeighingSessionColumnPatches, ct);
        await EnsureTableColumnsAsync(db, logger, "weighing_session_lines", WeighingSessionLineColumnPatches, ct);
        await EnsureWeighingSessionImagesTableAsync(db, logger, ct);
        await EnsureStationCodeBackfillAndIndexesAsync(db, logger, ct);
        await EnsureStationOperationSettingsTableAsync(db, logger, ct);
        await EnsurePrintTemplateProfileTableAsync(db, logger, ct);
        await EnsureDocumentCountersTableAsync(db, logger, ct);
        await DropLegacyDeviceConfigTableAsync(db, logger, ct);
    }

    private static async Task EnsureCutOrderSchemaAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'[cut_orders]', N'U') IS NULL AND OBJECT_ID(N'[vehicle_registrations]', N'U') IS NOT NULL
BEGIN
    EXEC sp_rename N'vehicle_registrations', N'cut_orders';
END

IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
BEGIN
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
END

IF OBJECT_ID(N'[weigh_tickets]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('weigh_tickets', 'CutOrderId') IS NULL AND COL_LENGTH('weigh_tickets', 'VehicleRegistrationId') IS NOT NULL
    BEGIN
        EXEC sp_rename N'weigh_tickets.VehicleRegistrationId', N'CutOrderId', N'COLUMN';
    END

    IF COL_LENGTH('weigh_tickets', 'ErpCutOrderId') IS NULL AND COL_LENGTH('weigh_tickets', 'ErpVehicleRegistrationId') IS NOT NULL
    BEGIN
        EXEC sp_rename N'weigh_tickets.ErpVehicleRegistrationId', N'ErpCutOrderId', N'COLUMN';
    END
END

IF OBJECT_ID(N'[delivery_tickets]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('delivery_tickets', 'CutOrderId') IS NULL AND COL_LENGTH('delivery_tickets', 'VehicleRegistrationId') IS NOT NULL
    BEGIN
        EXEC sp_rename N'delivery_tickets.VehicleRegistrationId', N'CutOrderId', N'COLUMN';
    END

    IF COL_LENGTH('delivery_tickets', 'ErpCutOrderId') IS NULL AND COL_LENGTH('delivery_tickets', 'ErpVehicleRegistrationId') IS NOT NULL
    BEGIN
        EXEC sp_rename N'delivery_tickets.ErpVehicleRegistrationId', N'ErpCutOrderId', N'COLUMN';
    END
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('weighing_session_lines', 'CutOrderId') IS NULL AND COL_LENGTH('weighing_session_lines', 'VehicleRegistrationId') IS NOT NULL
    BEGIN
        EXEC sp_rename N'weighing_session_lines.VehicleRegistrationId', N'CutOrderId', N'COLUMN';
    END
END
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility rename completed for cut_orders.");

        try
        {
            const string triggerSql = """
IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'IsDeleted') IS NOT NULL AND COL_LENGTH('cut_orders', 'ErpCutOrderId') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'TR_cut_orders_enforce_active_erp_cut_order_id', N'TR') IS NULL
    BEGIN
        EXEC(N'CREATE TRIGGER TR_cut_orders_enforce_active_erp_cut_order_id
        ON [cut_orders]
        AFTER INSERT, UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;

            IF EXISTS (
                SELECT [ErpCutOrderId]
                FROM [cut_orders]
                WHERE [ErpCutOrderId] IS NOT NULL
                  AND ISNULL([IsDeleted], 0) = 0
                GROUP BY [ErpCutOrderId]
                HAVING COUNT(*) > 1
            )
            BEGIN
                THROW 51001, N''Da ton tai cat lenh active khac cung ErpCutOrderId.'', 1;
            END
        END');
    END
END
""";
            await db.Database.ExecuteSqlRawAsync(triggerSql, ct);
            logger?.LogDebug("Schema compatibility check completed for TR_cut_orders_enforce_active_erp_cut_order_id trigger.");
        }
        catch (System.Exception ex)
        {
            logger?.LogWarning(ex, "Failed to ensure cut order trigger. The trigger might already exist or the user lacks permission.");
        }
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
IF OBJECT_ID('{tableName}', 'U') IS NOT NULL AND COL_LENGTH('{tableName}', '{patch.ColumnName}') IS NULL
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

    private static async Task EnsureStationAccessTablesAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
DECLARE @Now datetime2(7) = SYSDATETIME();
DECLARE @StationCode nvarchar(50);

IF OBJECT_ID(N'dbo.app_config', N'U') IS NOT NULL
BEGIN
    SELECT @StationCode = NULLIF(LTRIM(RTRIM(ConfigValue)), N'')
    FROM dbo.app_config
    WHERE ConfigKey = N'default_station_code';

    IF @StationCode IS NULL
    BEGIN
        SELECT @StationCode = NULLIF(LTRIM(RTRIM(ConfigValue)), N'')
        FROM dbo.app_config
        WHERE ConfigKey = N'station_code';
    END;
END;

IF @StationCode IS NULL
    SET @StationCode = N'QN01';

IF OBJECT_ID(N'dbo.app_config', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.app_config WHERE ConfigKey = N'default_station_code')
    BEGIN
        INSERT INTO dbo.app_config(ConfigKey, ConfigValue, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (N'default_station_code', @StationCode, @Now, N'SYSTEM', @Now, N'SYSTEM');
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.app_config WHERE ConfigKey = N'enable_user_station_scope')
    BEGIN
        INSERT INTO dbo.app_config(ConfigKey, ConfigValue, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (N'enable_user_station_scope', N'true', @Now, N'SYSTEM', @Now, N'SYSTEM');
    END;
END;

IF OBJECT_ID(N'dbo.stations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.stations
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_stations PRIMARY KEY,
        StationCode nvarchar(50) NOT NULL,
        StationName nvarchar(255) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_stations_is_active DEFAULT (1),
        SortOrder int NOT NULL CONSTRAINT DF_stations_sort_order DEFAULT (0),
        CreatedAt datetime2(7) NOT NULL,
        CreatedBy nvarchar(100) NULL,
        UpdatedAt datetime2(7) NULL,
        UpdatedBy nvarchar(100) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_stations_station_code' AND object_id = OBJECT_ID(N'dbo.stations'))
BEGIN
    CREATE UNIQUE INDEX UX_stations_station_code ON dbo.stations(StationCode);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.stations WHERE StationCode = @StationCode)
BEGIN
    INSERT INTO dbo.stations(Id, StationCode, StationName, IsActive, SortOrder, CreatedAt, CreatedBy)
    VALUES (NEWID(), @StationCode, CONCAT(N'Trạm ', @StationCode), 1, 0, @Now, N'SYSTEM');
END;

IF OBJECT_ID(N'dbo.user_station_assignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.user_station_assignments
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_user_station_assignments PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        StationCode nvarchar(50) NOT NULL,
        IsDefault bit NOT NULL CONSTRAINT DF_user_station_default DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_user_station_active DEFAULT (1),
        CreatedAt datetime2(7) NOT NULL,
        CreatedBy nvarchar(100) NULL,
        UpdatedAt datetime2(7) NULL,
        UpdatedBy nvarchar(100) NULL
    );
END;

IF OBJECT_ID(N'dbo.users', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_user_station_assignments_users')
    BEGIN
        ALTER TABLE dbo.user_station_assignments
        ADD CONSTRAINT FK_user_station_assignments_users
        FOREIGN KEY (UserId) REFERENCES dbo.users(Id);
    END;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_user_station_assignments_user_station' AND object_id = OBJECT_ID(N'dbo.user_station_assignments'))
BEGIN
    CREATE UNIQUE INDEX UX_user_station_assignments_user_station
    ON dbo.user_station_assignments(UserId, StationCode);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_user_station_assignments_user_active' AND object_id = OBJECT_ID(N'dbo.user_station_assignments'))
BEGIN
    CREATE INDEX IX_user_station_assignments_user_active
    ON dbo.user_station_assignments(UserId, IsActive);
END;

IF OBJECT_ID(N'dbo.users', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.user_station_assignments(Id, UserId, StationCode, IsDefault, IsActive, CreatedAt, CreatedBy)
    SELECT NEWID(), u.Id, @StationCode, 1, 1, @Now, N'SYSTEM'
    FROM dbo.users u
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.user_station_assignments usa
        WHERE usa.UserId = u.Id
    );
END;

IF OBJECT_ID(N'dbo.station_feature_flags', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.station_feature_flags
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_station_feature_flags PRIMARY KEY,
        StationCode nvarchar(50) NOT NULL,
        FeatureKey nvarchar(100) NOT NULL,
        FeatureValue nvarchar(50) NOT NULL,
        CreatedAt datetime2(7) NOT NULL,
        CreatedBy nvarchar(100) NULL,
        UpdatedAt datetime2(7) NULL,
        UpdatedBy nvarchar(100) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_station_feature_flags_station_key' AND object_id = OBJECT_ID(N'dbo.station_feature_flags'))
BEGIN
    CREATE UNIQUE INDEX UX_station_feature_flags_station_key
    ON dbo.station_feature_flags(StationCode, FeatureKey);
END;

DECLARE @DefaultFeatures TABLE(FeatureKey nvarchar(100) NOT NULL, FeatureValue nvarchar(50) NOT NULL);
INSERT INTO @DefaultFeatures(FeatureKey, FeatureValue)
VALUES
    (N'show_menu_dashboard', N'true'),
    (N'show_menu_incoming_vehicle_list', N'true'),
    (N'show_menu_weighing', N'true'),
    (N'show_menu_crusher_weighing', N'false'),
    (N'show_menu_clay_weighing', N'false'),
    (N'show_menu_export_weighing', N'true'),
    (N'show_menu_outgoing_vehicle_list', N'true'),
    (N'show_menu_export_report', N'true'),
    (N'show_menu_inbound_report', N'true'),
    (N'show_menu_crusher_inbound_report', N'false'),
    (N'show_menu_clay_inbound_report', N'false'),
    (N'show_dashboard_inbound_kpi', N'true'),
    (N'show_dashboard_outbound_kpi', N'true'),
    (N'default_navigation_target', N'Dashboard');

INSERT INTO dbo.station_feature_flags(Id, StationCode, FeatureKey, FeatureValue, CreatedAt, CreatedBy)
SELECT NEWID(), @StationCode, f.FeatureKey, f.FeatureValue, @Now, N'SYSTEM'
FROM @DefaultFeatures f
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.station_feature_flags existing
    WHERE existing.StationCode = @StationCode
      AND existing.FeatureKey = f.FeatureKey
);
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogInformation("Ensured station access and feature flag tables.");
    }

    private static async Task EnsureDeliveryTicketSyncStatusSchemaAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'[delivery_tickets]', N'U') IS NOT NULL
   AND EXISTS (
       SELECT 1
       FROM sys.columns c
       INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
       WHERE c.object_id = OBJECT_ID(N'[delivery_tickets]')
         AND c.name = N'SyncStatus'
         AND t.name = N'int'
   )
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_delivery_tickets_sync_status' AND object_id = OBJECT_ID(N'[delivery_tickets]'))
    BEGIN
        DROP INDEX [IX_delivery_tickets_sync_status] ON [delivery_tickets];
    END

    DECLARE @DefaultConstraintName sysname;
    SELECT @DefaultConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[delivery_tickets]')
      AND c.name = N'SyncStatus';

    IF @DefaultConstraintName IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [delivery_tickets] DROP CONSTRAINT [' + @DefaultConstraintName + N']');
    END

    ALTER TABLE [delivery_tickets] ADD [SyncStatusText] nvarchar(40) NULL;

    EXEC(N'UPDATE [delivery_tickets]
    SET [SyncStatusText] =
        CASE [SyncStatus]
            WHEN 1 THEN N''SYNC_QUEUED''
            WHEN 2 THEN N''SYNC_SUCCESS''
            WHEN 3 THEN N''SYNC_FAILED''
            ELSE N''SYNC_QUEUED''
        END');

    EXEC(N'ALTER TABLE [delivery_tickets] DROP COLUMN [SyncStatus]');
    EXEC sp_rename N'delivery_tickets.SyncStatusText', N'SyncStatus', N'COLUMN';
    EXEC(N'ALTER TABLE [delivery_tickets] ALTER COLUMN [SyncStatus] nvarchar(40) NOT NULL');
END

IF OBJECT_ID(N'[delivery_tickets]', N'U') IS NOT NULL
   AND COL_LENGTH('delivery_tickets', 'SyncStatus') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_delivery_tickets_sync_status' AND object_id = OBJECT_ID(N'[delivery_tickets]'))
BEGIN
    CREATE INDEX [IX_delivery_tickets_sync_status] ON [delivery_tickets]([SyncStatus]);
END
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility check completed for delivery_tickets.SyncStatus.");
    }

    private static async Task EnsureCutOrderIndexesAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('cut_orders', 'IsExportScale') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_is_export_scale_status' AND object_id = OBJECT_ID(N'[cut_orders]'))
    BEGIN
        CREATE INDEX [IX_cut_orders_is_export_scale_status]
        ON [cut_orders]([IsExportScale], [CutOrderStatus], [ProcessingStage], [IsDeleted]);
    END

    IF COL_LENGTH('cut_orders', 'IsTemporaryExport') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_temp_export' AND object_id = OBJECT_ID(N'[cut_orders]'))
    BEGIN
        CREATE INDEX [IX_cut_orders_temp_export]
        ON [cut_orders]([IsTemporaryExport], [IsExportScale], [ProcessingStage], [IsDeleted]);
    END

    IF COL_LENGTH('cut_orders', 'MappedRealCutOrderId') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_mapped_real' AND object_id = OBJECT_ID(N'[cut_orders]'))
    BEGIN
        CREATE INDEX [IX_cut_orders_mapped_real]
        ON [cut_orders]([MappedRealCutOrderId], [IsDeleted]);
    END

    IF COL_LENGTH('cut_orders', 'TemporaryExportSourceErpCutOrderId') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_temp_source_erp' AND object_id = OBJECT_ID(N'[cut_orders]'))
    BEGIN
        CREATE INDEX [IX_cut_orders_temp_source_erp]
        ON [cut_orders]([TemporaryExportSourceErpCutOrderId], [IsDeleted]);
    END

    IF COL_LENGTH('cut_orders', 'ErpCutOrderId') IS NOT NULL
       AND COL_LENGTH('cut_orders', 'IsDeleted') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_erp_cut_order_id_deleted' AND object_id = OBJECT_ID(N'[cut_orders]'))
    BEGIN
        CREATE INDEX [IX_cut_orders_erp_cut_order_id_deleted]
        ON [cut_orders]([ErpCutOrderId], [IsDeleted]);
    END

    IF COL_LENGTH('cut_orders', 'ErpRegistrationCode') IS NOT NULL
       AND COL_LENGTH('cut_orders', 'IsDeleted') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_erp_registration_code_deleted' AND object_id = OBJECT_ID(N'[cut_orders]'))
    BEGIN
        CREATE INDEX [IX_cut_orders_erp_registration_code_deleted]
        ON [cut_orders]([ErpRegistrationCode], [IsDeleted]);
    END
END
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility check completed for export scale cut order indexes.");
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
        [IsNoLoad] bit NOT NULL CONSTRAINT [DF_weighing_sessions_is_no_load_bootstrap] DEFAULT ((0)),
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

IF COL_LENGTH(N'[weighing_sessions]', N'IsNoLoad') IS NULL
BEGIN
    ALTER TABLE [weighing_sessions]
        ADD [IsNoLoad] bit NOT NULL
            CONSTRAINT [DF_weighing_sessions_is_no_load_bootstrap] DEFAULT ((0));
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

IF OBJECT_ID(N'[weigh_tickets]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weigh_tickets_weighing_session_id' AND object_id = OBJECT_ID(N'[weigh_tickets]'))
BEGIN
    CREATE INDEX [IX_weigh_tickets_weighing_session_id] ON [weigh_tickets]([WeighingSessionId]);
END

IF OBJECT_ID(N'[delivery_tickets]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_delivery_tickets_weighing_session_id' AND object_id = OBJECT_ID(N'[delivery_tickets]'))
    BEGIN
        CREATE INDEX [IX_delivery_tickets_weighing_session_id] ON [delivery_tickets]([WeighingSessionId]);
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_delivery_tickets_weighing_session_line_id' AND object_id = OBJECT_ID(N'[delivery_tickets]'))
    BEGIN
        CREATE INDEX [IX_delivery_tickets_weighing_session_line_id] ON [delivery_tickets]([WeighingSessionLineId]);
    END
END

IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_weighing_session_id' AND object_id = OBJECT_ID(N'[cut_orders]'))
BEGIN
    CREATE INDEX [IX_cut_orders_weighing_session_id] ON [cut_orders]([WeighingSessionId]);
END

UPDATE [weighing_sessions]
SET [SessionStatus] = N'READY_TO_COMPLETE'
WHERE [SessionStatus] = N'READY_TO_PRINT';
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);

        // Tách riêng DROP + CREATE UNIQUE INDEX vì lệnh DROP INDEX yêu cầu quyền DDL cao hơn.
        // Nếu user DB không đủ quyền, chỉ bỏ qua bước này (index cũ vẫn hoạt động được).
        try
        {
            const string uniqueIndexSql = """
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_weighing_session_lines_session_registration' AND object_id = OBJECT_ID(N'[dbo].[weighing_session_lines]'))
BEGIN
    DECLARE @weighingSessionLinesIndexFilter NVARCHAR(MAX);
    SELECT @weighingSessionLinesIndexFilter = i.filter_definition
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'[dbo].[weighing_session_lines]')
      AND i.name = 'UX_weighing_session_lines_session_registration';

    IF @weighingSessionLinesIndexFilter IS NULL OR @weighingSessionLinesIndexFilter <> N'([IsDeleted]=(0))'
    BEGIN
        DROP INDEX [UX_weighing_session_lines_session_registration] ON [dbo].[weighing_session_lines];
    END
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_weighing_session_lines_session_registration' AND object_id = OBJECT_ID(N'[dbo].[weighing_session_lines]'))
BEGIN
    CREATE UNIQUE INDEX [UX_weighing_session_lines_session_registration]
        ON [dbo].[weighing_session_lines]([WeighingSessionId], [CutOrderId])
        WHERE [IsDeleted] = 0;
END
""";
            await db.Database.ExecuteSqlRawAsync(uniqueIndexSql, ct);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to ensure UX_weighing_session_lines_session_registration index. The index might already exist or the user lacks DDL permission.");
        }

        logger?.LogDebug("Schema compatibility check completed for weighing session tables and indexes.");
    }

    private static async Task EnsureStationCodeBackfillAndIndexesAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
DECLARE @StationCode nvarchar(50);

IF OBJECT_ID(N'dbo.app_config', N'U') IS NOT NULL
BEGIN
    SELECT @StationCode = NULLIF(LTRIM(RTRIM([ConfigValue])), N'')
    FROM [app_config]
    WHERE [ConfigKey] = N'station_code';
END;

IF @StationCode IS NULL
    SET @StationCode = N'QN01';

IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'StationCode') IS NOT NULL
BEGIN
    UPDATE [cut_orders]
    SET [StationCode] = @StationCode
    WHERE [StationCode] IS NULL OR LTRIM(RTRIM([StationCode])) = N'';

    ALTER TABLE [cut_orders] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cut_orders_station_stage_status' AND object_id = OBJECT_ID(N'[cut_orders]'))
    BEGIN
        CREATE INDEX [IX_cut_orders_station_stage_status]
        ON [cut_orders]([StationCode], [ProcessingStage], [CutOrderStatus], [IsDeleted]);
    END
END

IF OBJECT_ID(N'[weighing_sessions]', N'U') IS NOT NULL AND COL_LENGTH('weighing_sessions', 'StationCode') IS NOT NULL
BEGIN
    UPDATE [weighing_sessions]
    SET [StationCode] = @StationCode
    WHERE [StationCode] IS NULL OR LTRIM(RTRIM([StationCode])) = N'';

    ALTER TABLE [weighing_sessions] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_sessions_station_status_time' AND object_id = OBJECT_ID(N'[weighing_sessions]'))
    BEGIN
        CREATE INDEX [IX_weighing_sessions_station_status_time]
        ON [weighing_sessions]([StationCode], [SessionStatus], [Weight2Time], [CreatedAt]);
    END
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL AND COL_LENGTH('weighing_session_lines', 'StationCode') IS NOT NULL
BEGIN
    UPDATE wl
    SET wl.[StationCode] = COALESCE(NULLIF(LTRIM(RTRIM(ws.[StationCode])), N''), @StationCode)
    FROM [weighing_session_lines] wl
    LEFT JOIN [weighing_sessions] ws ON ws.[Id] = wl.[WeighingSessionId]
    WHERE wl.[StationCode] IS NULL OR LTRIM(RTRIM(wl.[StationCode])) = N'';

    ALTER TABLE [weighing_session_lines] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;
END

IF OBJECT_ID(N'[weigh_tickets]', N'U') IS NOT NULL AND COL_LENGTH('weigh_tickets', 'StationCode') IS NOT NULL
BEGIN
    UPDATE wt
    SET wt.[StationCode] = COALESCE(NULLIF(LTRIM(RTRIM(co.[StationCode])), N''), NULLIF(LTRIM(RTRIM(ws.[StationCode])), N''), @StationCode)
    FROM [weigh_tickets] wt
    LEFT JOIN [cut_orders] co ON co.[Id] = wt.[CutOrderId]
    LEFT JOIN [weighing_sessions] ws ON ws.[Id] = wt.[WeighingSessionId]
    WHERE wt.[StationCode] IS NULL OR LTRIM(RTRIM(wt.[StationCode])) = N'';

    ALTER TABLE [weigh_tickets] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weigh_tickets_station_ticket_no' AND object_id = OBJECT_ID(N'[weigh_tickets]'))
    BEGIN
        CREATE INDEX [IX_weigh_tickets_station_ticket_no]
        ON [weigh_tickets]([StationCode], [TicketNo]);
    END
END

IF OBJECT_ID(N'[delivery_tickets]', N'U') IS NOT NULL AND COL_LENGTH('delivery_tickets', 'StationCode') IS NOT NULL
BEGIN
    UPDATE dt
    SET dt.[StationCode] = COALESCE(NULLIF(LTRIM(RTRIM(co.[StationCode])), N''), NULLIF(LTRIM(RTRIM(ws.[StationCode])), N''), @StationCode)
    FROM [delivery_tickets] dt
    LEFT JOIN [cut_orders] co ON co.[Id] = dt.[CutOrderId]
    LEFT JOIN [weighing_sessions] ws ON ws.[Id] = dt.[WeighingSessionId]
    WHERE dt.[StationCode] IS NULL OR LTRIM(RTRIM(dt.[StationCode])) = N'';

    ALTER TABLE [delivery_tickets] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_delivery_tickets_station_delivery_no' AND object_id = OBJECT_ID(N'[delivery_tickets]'))
    BEGIN
        CREATE INDEX [IX_delivery_tickets_station_delivery_no]
        ON [delivery_tickets]([StationCode], [DeliveryNo]);
    END
END

IF OBJECT_ID(N'[weighing_session_images]', N'U') IS NOT NULL AND COL_LENGTH('weighing_session_images', 'StationCode') IS NOT NULL
BEGIN
    UPDATE wi
    SET wi.[StationCode] = COALESCE(NULLIF(LTRIM(RTRIM(ws.[StationCode])), N''), @StationCode)
    FROM [weighing_session_images] wi
    LEFT JOIN [weighing_sessions] ws ON ws.[Id] = wi.[WeighingSessionId]
    WHERE wi.[StationCode] IS NULL OR LTRIM(RTRIM(wi.[StationCode])) = N'';

    ALTER TABLE [weighing_session_images] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;
END

IF OBJECT_ID(N'[sync_outbox]', N'U') IS NOT NULL AND COL_LENGTH('sync_outbox', 'StationCode') IS NOT NULL
BEGIN
    UPDATE so
    SET so.[StationCode] =
        COALESCE(
            NULLIF(LTRIM(RTRIM(co.[StationCode])), N''),
            NULLIF(LTRIM(RTRIM(wt.[StationCode])), N''),
            NULLIF(LTRIM(RTRIM(dt.[StationCode])), N''),
            NULLIF(LTRIM(RTRIM(ws.[StationCode])), N''),
            NULLIF(LTRIM(RTRIM(wl.[StationCode])), N''),
            @StationCode)
    FROM [sync_outbox] so
    LEFT JOIN [cut_orders] co ON co.[Id] = so.[AggregateId]
    LEFT JOIN [weigh_tickets] wt ON wt.[Id] = so.[AggregateId]
    LEFT JOIN [delivery_tickets] dt ON dt.[Id] = so.[AggregateId]
    LEFT JOIN [weighing_sessions] ws ON ws.[Id] = so.[AggregateId]
    LEFT JOIN [weighing_session_lines] wl ON wl.[Id] = so.[AggregateId]
    WHERE so.[StationCode] IS NULL OR LTRIM(RTRIM(so.[StationCode])) = N'';

    ALTER TABLE [sync_outbox] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_sync_outbox_station_status_next_retry' AND object_id = OBJECT_ID(N'[sync_outbox]'))
    BEGIN
        CREATE INDEX [IX_sync_outbox_station_status_next_retry]
        ON [sync_outbox]([StationCode], [Status], [NextRetryAt]);
    END
END

-- 8. Backfill and configure vehicles StationCode and UX Index
IF OBJECT_ID(N'[vehicles]', N'U') IS NOT NULL AND COL_LENGTH('vehicles', 'StationCode') IS NOT NULL
BEGIN
    UPDATE [vehicles]
    SET [StationCode] = @StationCode
    WHERE [StationCode] IS NULL OR LTRIM(RTRIM([StationCode])) = N'';

    ALTER TABLE [vehicles] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_vehicles_plate_mooc' AND object_id = OBJECT_ID(N'[vehicles]'))
    BEGIN
        DROP INDEX [UX_vehicles_plate_mooc] ON [vehicles];
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_vehicles_station_plate_mooc' AND object_id = OBJECT_ID(N'[vehicles]'))
    BEGIN
        CREATE UNIQUE INDEX [UX_vehicles_station_plate_mooc] ON [vehicles]([StationCode], [VehiclePlate], [MoocNumber]);
    END
END

-- 9. Backfill and configure customers StationCode and UX Index
IF OBJECT_ID(N'[customers]', N'U') IS NOT NULL AND COL_LENGTH('customers', 'StationCode') IS NOT NULL
BEGIN
    UPDATE [customers]
    SET [StationCode] = @StationCode
    WHERE [StationCode] IS NULL OR LTRIM(RTRIM([StationCode])) = N'';

    ALTER TABLE [customers] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_customers_code' AND object_id = OBJECT_ID(N'[customers]'))
    BEGIN
        DROP INDEX [UX_customers_code] ON [customers];
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_customers_station_code' AND object_id = OBJECT_ID(N'[customers]'))
    BEGIN
        CREATE UNIQUE INDEX [UX_customers_station_code] ON [customers]([StationCode], [CustomerCode]);
    END
END

-- 10. Backfill and configure products StationCode and UX Index
IF OBJECT_ID(N'[products]', N'U') IS NOT NULL AND COL_LENGTH('products', 'StationCode') IS NOT NULL
BEGIN
    UPDATE [products]
    SET [StationCode] = @StationCode
    WHERE [StationCode] IS NULL OR LTRIM(RTRIM([StationCode])) = N'';

    ALTER TABLE [products] ALTER COLUMN [StationCode] nvarchar(50) NOT NULL;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_products_code' AND object_id = OBJECT_ID(N'[products]'))
    BEGIN
        DROP INDEX [UX_products_code] ON [products];
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_products_station_code' AND object_id = OBJECT_ID(N'[products]'))
    BEGIN
        CREATE UNIQUE INDEX [UX_products_station_code] ON [products]([StationCode], [ProductCode]);
    END
END
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility check completed for StationCode backfill and indexes.");
    }

    private static async Task EnsureStationOperationSettingsTableAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'dbo.station_operation_settings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.station_operation_settings
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_station_operation_settings PRIMARY KEY,
        StationCode nvarchar(50) NOT NULL,
        SettingKey nvarchar(100) NOT NULL,
        SettingValue nvarchar(1000) NOT NULL CONSTRAINT DF_station_operation_settings_value DEFAULT (N''),
        CreatedAt datetime2(7) NOT NULL,
        CreatedBy nvarchar(100) NOT NULL,
        UpdatedAt datetime2(7) NULL,
        UpdatedBy nvarchar(100) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_station_operation_settings_station_key' AND object_id = OBJECT_ID(N'dbo.station_operation_settings'))
BEGIN
    CREATE UNIQUE INDEX UX_station_operation_settings_station_key
    ON dbo.station_operation_settings(StationCode, SettingKey);
END;

DECLARE @Now datetime2(7) = SYSDATETIME();
DECLARE @Defaults TABLE(SettingKey nvarchar(100) NOT NULL, SettingValue nvarchar(1000) NOT NULL);
INSERT INTO @Defaults(SettingKey, SettingValue)
VALUES
    (N'crusher_single_weigh_enabled', N'false'),
    (N'crusher_default_weigh_mode', N'TWO_WEIGH'),
    (N'crusher_require_standard_tare_for_single_weigh', N'true'),
    (N'crusher_standard_tare_tolerance_kg', N'0'),
    (N'crusher_default_product_code', N''),
    (N'clay_single_weigh_enabled', N'false'),
    (N'clay_default_weigh_mode', N'TWO_WEIGH'),
    (N'clay_require_standard_tare_for_single_weigh', N'true'),
    (N'clay_standard_tare_tolerance_kg', N'0'),
    (N'clay_default_product_code', N'');

INSERT INTO dbo.station_operation_settings(Id, StationCode, SettingKey, SettingValue, CreatedAt, CreatedBy)
SELECT NEWID(), s.StationCode, d.SettingKey, d.SettingValue, @Now, N'SYSTEM'
FROM dbo.stations s
CROSS JOIN @Defaults d
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.station_operation_settings existing
    WHERE existing.StationCode = s.StationCode
      AND existing.SettingKey = d.SettingKey
);
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Schema compatibility check completed for station_operation_settings.");
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

    private static async Task EnsureWeighingSessionImagesTableAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'[weighing_session_images]', N'U') IS NULL
BEGIN
    CREATE TABLE [weighing_session_images](
        [Id] uniqueidentifier NOT NULL,
        [WeighingSessionId] uniqueidentifier NOT NULL,
        [CaptureStage] nvarchar(20) NOT NULL,
        [CameraCode] nvarchar(20) NOT NULL,
        [CameraName] nvarchar(100) NOT NULL,
        [RtspUrlSnapshot] nvarchar(1000) NULL,
        [ImageFormat] nvarchar(20) NOT NULL,
        [ImageBytes] varbinary(max) NOT NULL,
        [FileSizeBytes] bigint NOT NULL CONSTRAINT [DF_weighing_session_images_file_size_bootstrap] DEFAULT ((0)),
        [CapturedAt] datetime2 NOT NULL,
        [CapturedBy] nvarchar(100) NOT NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_weighing_session_images_is_deleted_bootstrap] DEFAULT ((0)),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_weighing_session_images] PRIMARY KEY ([Id])
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_images_session_id' AND object_id = OBJECT_ID(N'[weighing_session_images]'))
BEGIN
    CREATE INDEX [IX_weighing_session_images_session_id] ON [weighing_session_images]([WeighingSessionId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_images_lookup' AND object_id = OBJECT_ID(N'[weighing_session_images]'))
BEGIN
    CREATE INDEX [IX_weighing_session_images_lookup] ON [weighing_session_images]([WeighingSessionId], [CaptureStage], [CameraCode]);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_weighing_session_images_captured_at' AND object_id = OBJECT_ID(N'[weighing_session_images]'))
BEGIN
    CREATE INDEX [IX_weighing_session_images_captured_at] ON [weighing_session_images]([CapturedAt]);
END
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        await EnsureTableColumnsAsync(db, logger, "weighing_session_images", WeighingSessionImageColumnPatches, ct);
        logger?.LogDebug("Schema compatibility check completed for weighing session images table and indexes.");
    }

    private static async Task DropLegacyDeviceConfigTableAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'[device_configs]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [device_configs];
END
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger?.LogDebug("Dropped legacy device_configs table if it existed.");
    }

    private static async Task EnsureDocumentCountersTableAsync(StationDbContext db, ILogger? logger, CancellationToken ct)
    {
        const string createTableSql = """
IF OBJECT_ID(N'dbo.document_counters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.document_counters
    (
        CounterKey nvarchar(100) NOT NULL,
        LastValue int NOT NULL,
        UpdatedAt datetime2(7) NOT NULL,
        CONSTRAINT PK_document_counters PRIMARY KEY (CounterKey)
    );
END
""";
        await db.Database.ExecuteSqlRawAsync(createTableSql, ct);
        logger?.LogDebug("Schema compatibility check completed for document_counters table.");

        const string resyncSql = """
-- 1. Sync WeighingSession counters
IF OBJECT_ID(N'dbo.weighing_sessions', N'U') IS NOT NULL
BEGIN
    MERGE dbo.document_counters AS target
    USING (
        SELECT 'WeighingSession_' + LEFT(SessionNo, 6) AS CounterKey, 
               MAX(TRY_CAST(RIGHT(SessionNo, 4) AS INT)) AS MaxVal
        FROM dbo.weighing_sessions
        WHERE SessionNo LIKE 'LC[0-9][0-9][0-9][0-9]%' AND LEN(SessionNo) = 10
        GROUP BY LEFT(SessionNo, 6)
        HAVING MAX(TRY_CAST(RIGHT(SessionNo, 4) AS INT)) IS NOT NULL
    ) AS source
    ON target.CounterKey = source.CounterKey
    WHEN MATCHED AND target.LastValue < source.MaxVal THEN
        UPDATE SET target.LastValue = source.MaxVal, target.UpdatedAt = SYSDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (CounterKey, LastValue, UpdatedAt)
        VALUES (source.CounterKey, source.MaxVal, SYSDATETIME());
END

-- 2. Sync WeighTicket counters
IF OBJECT_ID(N'dbo.weigh_tickets', N'U') IS NOT NULL
BEGIN
    DECLARE @TicketPrefix NVARCHAR(50) = NULL;
    IF OBJECT_ID(N'dbo.app_config', N'U') IS NOT NULL
    BEGIN
        SET @TicketPrefix = (SELECT TOP 1 ConfigValue FROM dbo.app_config WHERE ConfigKey = 'ticket_prefix');
    END
    IF @TicketPrefix IS NULL SET @TicketPrefix = N'QN';

    DECLARE @TicketPrefixLike NVARCHAR(100) = @TicketPrefix + '[0-9][0-9][0-9][0-9]%';

    MERGE dbo.document_counters AS target
    USING (
        SELECT 'WeighTicket_' + LEFT(TicketNo, LEN(TicketNo) - 4) AS CounterKey, 
               MAX(TRY_CAST(RIGHT(TicketNo, 4) AS INT)) AS MaxVal
        FROM dbo.weigh_tickets
        WHERE TicketNo LIKE @TicketPrefixLike AND LEN(TicketNo) = LEN(@TicketPrefix) + 8
        GROUP BY LEFT(TicketNo, LEN(TicketNo) - 4)
        HAVING MAX(TRY_CAST(RIGHT(TicketNo, 4) AS INT)) IS NOT NULL
    ) AS source
    ON target.CounterKey = source.CounterKey
    WHEN MATCHED AND target.LastValue < source.MaxVal THEN
        UPDATE SET target.LastValue = source.MaxVal, target.UpdatedAt = SYSDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (CounterKey, LastValue, UpdatedAt)
        VALUES (source.CounterKey, source.MaxVal, SYSDATETIME());
END

-- 3. Sync DeliveryTicket counters
IF OBJECT_ID(N'dbo.delivery_tickets', N'U') IS NOT NULL
BEGIN
    DECLARE @DeliveryPrefix NVARCHAR(50) = NULL;
    IF OBJECT_ID(N'dbo.app_config', N'U') IS NOT NULL
    BEGIN
        SET @DeliveryPrefix = (SELECT TOP 1 ConfigValue FROM dbo.app_config WHERE ConfigKey = 'delivery_prefix');
    END
    IF @DeliveryPrefix IS NULL SET @DeliveryPrefix = N'DN';

    DECLARE @DeliveryPrefixLike NVARCHAR(100) = @DeliveryPrefix + '[0-9][0-9][0-9][0-9]%';

    MERGE dbo.document_counters AS target
    USING (
        SELECT 'DeliveryTicket_' + LEFT(DeliveryNo, LEN(DeliveryNo) - 4) AS CounterKey, 
               MAX(TRY_CAST(RIGHT(DeliveryNo, 4) AS INT)) AS MaxVal
        FROM dbo.delivery_tickets
        WHERE DeliveryNo LIKE @DeliveryPrefixLike AND LEN(DeliveryNo) = LEN(@DeliveryPrefix) + 8
        GROUP BY LEFT(DeliveryNo, LEN(DeliveryNo) - 4)
        HAVING MAX(TRY_CAST(RIGHT(DeliveryNo, 4) AS INT)) IS NOT NULL
    ) AS source
    ON target.CounterKey = source.CounterKey
    WHEN MATCHED AND target.LastValue < source.MaxVal THEN
        UPDATE SET target.LastValue = source.MaxVal, target.UpdatedAt = SYSDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (CounterKey, LastValue, UpdatedAt)
        VALUES (source.CounterKey, source.MaxVal, SYSDATETIME());
END
""";
        try
        {
            await db.Database.ExecuteSqlRawAsync(resyncSql, ct);
            logger?.LogInformation("Successfully synchronized and self-healed document_counters table with existing records.");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to self-heal document_counters table.");
        }
    }

    private sealed record ColumnPatch(string ColumnName, string SqlDefinition);
}
