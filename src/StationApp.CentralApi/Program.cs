using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using StationApp.CentralApi.Authentication;
using StationApp.CentralApi.Configuration;
using StationApp.CentralApi.Persistence;
using StationApp.CentralApi.Services;
using StationApp.Contracts.Sync;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, configuration) =>
{
    var logDirectory = context.Configuration["CentralApi:LogDirectory"];
    if (string.IsNullOrWhiteSpace(logDirectory))
    {
        logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "StationApp",
            "CentralApi",
            "logs");
    }

    var logPath = Path.Combine(logDirectory, "central-api-.log");

    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
});

builder.Services.Configure<CentralApiOptions>(builder.Configuration.GetSection(CentralApiOptions.SectionName));
builder.Services.AddDbContext<CentralSyncDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("CentralConnection")
        ?? "Server=.;Database=StationAppCentral;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;"));

var app = builder.Build();
var startupLogger = app.Logger;
var centralOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CentralApiOptions>>().Value;
var centralConnectionString = builder.Configuration.GetConnectionString("CentralConnection") ?? string.Empty;
startupLogger.LogInformation(
    "StationApp.CentralApi starting. Environment={Environment} DatabaseServer={DatabaseServer} DatabaseName={DatabaseName} ApiKeyConfigured={ApiKeyConfigured}",
    app.Environment.EnvironmentName,
    GetSqlConnectionPart(centralConnectionString, "Data Source") ?? GetSqlConnectionPart(centralConnectionString, "Server") ?? "unknown",
    GetSqlConnectionPart(centralConnectionString, "Initial Catalog") ?? GetSqlConnectionPart(centralConnectionString, "Database") ?? "unknown",
    !string.IsNullOrWhiteSpace(centralOptions.ApiKey));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CentralSyncDbContext>();
    startupLogger.LogInformation("Ensuring central database schema compatibility.");
    await db.Database.EnsureCreatedAsync();
    await EnsureCentralSchemaCompatibilityAsync(db);
    startupLogger.LogInformation("Central database schema compatibility check completed.");
}

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("StationApp.CentralApi.Request");
    var startedAt = Stopwatch.StartNew();
    try
    {
        await next();
        startedAt.Stop();

        if (context.Request.Path == "/health")
        {
            logger.LogInformation(
                "Request completed. Method={Method} Path={Path} StatusCode={StatusCode} DurationMs={DurationMs} TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                startedAt.ElapsedMilliseconds,
                context.TraceIdentifier);
        }
    }
    catch (Exception ex)
    {
        startedAt.Stop();
        logger.LogError(
            ex,
            "Unhandled request exception. Method={Method} Path={Path} DurationMs={DurationMs} TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            startedAt.ElapsedMilliseconds,
            context.TraceIdentifier);
        throw;
    }
});

app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

app.MapGet("/health", async (CentralSyncDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    var logger = loggerFactory.CreateLogger("StationApp.CentralApi.Health");
    var dbOk = await db.Database.CanConnectAsync(ct);
    logger.LogInformation("Health check completed. Database={Database} TraceId={TraceId}", dbOk ? "ok" : "unreachable", System.Diagnostics.Activity.Current?.Id ?? "n/a");
    return Results.Ok(new
    {
        success = true,
        service = "StationApp.CentralApi",
        database = dbOk ? "ok" : "unreachable"
    });
});

app.MapPost("/api/vehicle-registrations", (CutOrder payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
    SyncEndpointHandler.UpsertAsync(db, SyncAggregateTypes.CutOrder, payload.Id, payload, logger, httpContext, ct));
app.MapPost("/api/weigh-tickets", (WeighTicket payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
    SyncEndpointHandler.UpsertAsync(db, SyncAggregateTypes.WeighTicket, payload.Id, payload, logger, httpContext, ct));
app.MapPost("/api/delivery-tickets", (DeliveryTicket payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
    SyncEndpointHandler.UpsertAsync(db, SyncAggregateTypes.DeliveryTicket, payload.Id, payload, logger, httpContext, ct));
app.MapPost("/api/vehicles", (Vehicle payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
    SyncEndpointHandler.UpsertAsync(db, SyncAggregateTypes.Vehicle, payload.Id, payload, logger, httpContext, ct));
app.MapPost("/api/customers", (Customer payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
    SyncEndpointHandler.UpsertAsync(db, SyncAggregateTypes.Customer, payload.Id, payload, logger, httpContext, ct));
app.MapPost("/api/products", (Product payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
    SyncEndpointHandler.UpsertAsync(db, SyncAggregateTypes.Product, payload.Id, payload, logger, httpContext, ct));
app.MapPost("/api/weighing-sessions", (WeighingSession payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
    SyncEndpointHandler.UpsertAsync(db, SyncAggregateTypes.WeighingSession, payload.Id, payload, logger, httpContext, ct));
app.MapPost("/api/weighing-session-lines", (WeighingSessionLine payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
    SyncEndpointHandler.UpsertAsync(db, SyncAggregateTypes.WeighingSessionLine, payload.Id, payload, logger, httpContext, ct));
app.MapPost("/api/weighing-session-images", async (SyncWeighingSessionImageRequest payload, CentralSyncDbContext db, ILogger<Program> logger, HttpContext httpContext, CancellationToken ct) =>
{
    var entity = new WeighingSessionImage
    {
        Id = payload.Id,
        StationCode = payload.StationCode,
        WeighingSessionId = payload.WeighingSessionId,
        CaptureStage = Enum.Parse<StationApp.Domain.Enums.CameraCaptureStage>(payload.CaptureStage, true),
        CameraCode = payload.CameraCode,
        CameraName = payload.CameraName,
        RtspUrlSnapshot = payload.RtspUrlSnapshot,
        ImageFormat = payload.ImageFormat,
        ImageBytes = payload.ImageBytes,
        FileSizeBytes = payload.FileSizeBytes,
        CapturedAt = payload.CapturedAt,
        CapturedBy = payload.CapturedBy,
        CreatedAt = payload.CreatedAt,
        CreatedBy = payload.CreatedBy,
        UpdatedAt = payload.UpdatedAt,
        UpdatedBy = payload.UpdatedBy
    };

    return await SyncEndpointHandler.UpsertAsync(db, "WeighingSessionImage", payload.Id, entity, logger, httpContext, ct);
});

startupLogger.LogInformation("StationApp.CentralApi startup complete. Listening for requests.");
await app.RunAsync();

static async Task EnsureCentralSchemaCompatibilityAsync(CentralSyncDbContext db)
{
    await db.Database.ExecuteSqlRawAsync(
        """
        IF OBJECT_ID(N'[sync_ingestion_logs]', N'U') IS NULL
        BEGIN
            CREATE TABLE [sync_ingestion_logs](
                [Id] uniqueidentifier NOT NULL,
                [StationCode] nvarchar(50) NULL,
                [AggregateType] nvarchar(100) NOT NULL,
                [SourceRecordId] uniqueidentifier NOT NULL,
                [ReceivedAt] datetime2 NOT NULL,
                [ProcessedAt] datetime2 NULL,
                [Status] nvarchar(30) NOT NULL,
                [ErrorMessage] nvarchar(4000) NULL,
                CONSTRAINT [PK_sync_ingestion_logs] PRIMARY KEY ([Id])
            );
        END

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = 'IX_sync_ingestion_logs_aggregate_source'
              AND object_id = OBJECT_ID(N'[sync_ingestion_logs]'))
        BEGIN
            CREATE INDEX [IX_sync_ingestion_logs_aggregate_source]
            ON [sync_ingestion_logs]([AggregateType], [SourceRecordId]);
        END

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = 'IX_sync_ingestion_logs_received_at'
              AND object_id = OBJECT_ID(N'[sync_ingestion_logs]'))
        BEGIN
            CREATE INDEX [IX_sync_ingestion_logs_received_at]
            ON [sync_ingestion_logs]([ReceivedAt]);
        END

        IF OBJECT_ID(N'[weighing_session_images]', N'U') IS NULL
        BEGIN
            CREATE TABLE [weighing_session_images](
                [Id] uniqueidentifier NOT NULL,
                [StationCode] nvarchar(50) NOT NULL CONSTRAINT [DF_weighing_session_images_station_code_create] DEFAULT (N'QN01'),
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
        """);

    await EnsureColumnAsync(db, "cut_orders", "StationCode", "nvarchar(50) NOT NULL CONSTRAINT [DF_cut_orders_station_code_bootstrap] DEFAULT (N'QN01')");
    await EnsureColumnAsync(db, "cut_orders", "ErpExportCompleted", "bit NOT NULL CONSTRAINT [DF_cut_orders_erp_export_completed_bootstrap] DEFAULT ((0))");
    await EnsureColumnAsync(db, "cut_orders", "IsTemporaryExport", "bit NOT NULL CONSTRAINT [DF_cut_orders_is_temporary_export_bootstrap] DEFAULT ((0))");
    await EnsureColumnAsync(db, "cut_orders", "MappedRealCutOrderId", "uniqueidentifier NULL");
    await EnsureColumnAsync(db, "cut_orders", "MappedTemporaryCutOrderId", "uniqueidentifier NULL");
    await EnsureColumnAsync(db, "cut_orders", "TemporaryExportCreatedReason", "nvarchar(50) NULL");
    await EnsureColumnAsync(db, "cut_orders", "TemporaryExportDisplayCode", "nvarchar(100) NULL");
    await EnsureColumnAsync(db, "cut_orders", "TemporaryExportSourceErpCutOrderId", "nvarchar(100) NULL");
    await EnsureColumnAsync(db, "cut_orders", "MappedAt", "datetime2 NULL");
    await EnsureColumnAsync(db, "cut_orders", "MappedBy", "nvarchar(100) NULL");

    await EnsureDeliveryTicketSyncStatusSchemaAsync(db);
    await EnsureColumnAsync(db, "sync_ingestion_logs", "StationCode", "nvarchar(50) NULL");
    await EnsureColumnAsync(db, "vehicles", "IsInternalVehicle", "bit NOT NULL CONSTRAINT [DF_vehicles_is_internal_vehicle_bootstrap] DEFAULT ((0))");
    await EnsureColumnAsync(db, "vehicles", "StandardTareSource", "nvarchar(50) NULL");
    await EnsureColumnAsync(db, "vehicles", "StandardTareUpdatedAt", "datetime2 NULL");
    await EnsureColumnAsync(db, "vehicles", "StandardTareUpdatedBy", "nvarchar(100) NULL");

    await EnsureColumnAsync(db, "weigh_tickets", "StationCode", "nvarchar(50) NOT NULL CONSTRAINT [DF_weigh_tickets_station_code_bootstrap] DEFAULT (N'QN01')");
    await EnsureColumnAsync(db, "weigh_tickets", "WeighingMode", "nvarchar(40) NOT NULL CONSTRAINT [DF_weigh_tickets_weighing_mode_bootstrap] DEFAULT (N'TWO_WEIGH')");
    await EnsureColumnAsync(db, "weigh_tickets", "InternalVehicleNo", "nvarchar(30) NULL");
    await EnsureColumnAsync(db, "weigh_tickets", "StandardTareWeightSnapshot", "decimal(18,3) NULL");
    await EnsureColumnAsync(db, "weigh_tickets", "StandardTareSourceSnapshot", "nvarchar(50) NULL");
    await EnsureColumnAsync(db, "weigh_tickets", "StandardTareVehicleId", "uniqueidentifier NULL");
    await EnsureColumnAsync(db, "weigh_tickets", "NetWeightCalculationMode", "nvarchar(50) NULL CONSTRAINT [DF_weigh_tickets_net_calc_mode_bootstrap] DEFAULT (N'WEIGHT2_DIFF')");
    await EnsureColumnAsync(db, "delivery_tickets", "StationCode", "nvarchar(50) NOT NULL CONSTRAINT [DF_delivery_tickets_station_code_bootstrap] DEFAULT (N'QN01')");

    await EnsureColumnAsync(db, "weighing_sessions", "StationCode", "nvarchar(50) NOT NULL CONSTRAINT [DF_weighing_sessions_station_code_bootstrap] DEFAULT (N'QN01')");
    await EnsureColumnAsync(db, "weighing_sessions", "SyncStatus", "nvarchar(30) NOT NULL CONSTRAINT [DF_weighing_sessions_sync_status_bootstrap] DEFAULT (N'SYNC_QUEUED')");
    await EnsureColumnAsync(db, "weighing_sessions", "LastSyncAttemptAt", "datetime2 NULL");
    await EnsureColumnAsync(db, "weighing_sessions", "LastSyncError", "nvarchar(1000) NULL");
    await EnsureColumnAsync(db, "weighing_sessions", "WeighingMode", "nvarchar(40) NOT NULL CONSTRAINT [DF_weighing_sessions_weighing_mode_bootstrap] DEFAULT (N'TWO_WEIGH')");
    await EnsureColumnAsync(db, "weighing_sessions", "InternalVehicleNo", "nvarchar(30) NULL");
    await EnsureColumnAsync(db, "weighing_sessions", "StandardTareWeightSnapshot", "decimal(18,3) NULL");
    await EnsureColumnAsync(db, "weighing_sessions", "StandardTareSourceSnapshot", "nvarchar(50) NULL");
    await EnsureColumnAsync(db, "weighing_sessions", "StandardTareVehicleId", "uniqueidentifier NULL");
    await EnsureColumnAsync(db, "weighing_sessions", "NetWeightCalculationMode", "nvarchar(50) NULL CONSTRAINT [DF_weighing_sessions_net_calc_mode_bootstrap] DEFAULT (N'WEIGHT2_DIFF')");

    await EnsureColumnAsync(db, "weighing_session_lines", "StationCode", "nvarchar(50) NOT NULL CONSTRAINT [DF_weighing_session_lines_station_code_bootstrap] DEFAULT (N'QN01')");
    await EnsureColumnAsync(db, "weighing_session_lines", "SyncStatus", "nvarchar(30) NOT NULL CONSTRAINT [DF_weighing_session_lines_sync_status_bootstrap] DEFAULT (N'SYNC_QUEUED')");
    await EnsureColumnAsync(db, "weighing_session_lines", "LastSyncAttemptAt", "datetime2 NULL");
    await EnsureColumnAsync(db, "weighing_session_lines", "LastSyncError", "nvarchar(1000) NULL");

    await EnsureColumnAsync(db, "weighing_session_images", "StationCode", "nvarchar(50) NOT NULL CONSTRAINT [DF_weighing_session_images_station_code_bootstrap] DEFAULT (N'QN01')");
    await EnsureColumnAsync(db, "weighing_session_images", "Id", "uniqueidentifier NOT NULL CONSTRAINT [DF_weighing_session_images_id_bootstrap] DEFAULT (newid())");
    await EnsureColumnAsync(db, "weighing_session_images", "WeighingSessionId", "uniqueidentifier NOT NULL CONSTRAINT [DF_weighing_session_images_session_id_bootstrap] DEFAULT ('00000000-0000-0000-0000-000000000000')");
    await EnsureColumnAsync(db, "weighing_session_images", "CaptureStage", "nvarchar(20) NOT NULL CONSTRAINT [DF_weighing_session_images_stage_bootstrap] DEFAULT (N'WEIGHT1')");
    await EnsureColumnAsync(db, "weighing_session_images", "CameraCode", "nvarchar(20) NOT NULL CONSTRAINT [DF_weighing_session_images_camera_code_bootstrap] DEFAULT (N'CAM1')");
    await EnsureColumnAsync(db, "weighing_session_images", "CameraName", "nvarchar(100) NOT NULL CONSTRAINT [DF_weighing_session_images_camera_name_bootstrap] DEFAULT (N'Camera 1')");
    await EnsureColumnAsync(db, "weighing_session_images", "RtspUrlSnapshot", "nvarchar(1000) NULL");
    await EnsureColumnAsync(db, "weighing_session_images", "ImageFormat", "nvarchar(20) NOT NULL CONSTRAINT [DF_weighing_session_images_format_bootstrap] DEFAULT (N'jpg')");
    await EnsureColumnAsync(db, "weighing_session_images", "ImageBytes", "varbinary(max) NOT NULL CONSTRAINT [DF_weighing_session_images_bytes_bootstrap] DEFAULT (0x)");
    await EnsureColumnAsync(db, "weighing_session_images", "FileSizeBytes", "bigint NOT NULL CONSTRAINT [DF_weighing_session_images_file_size_patch_bootstrap] DEFAULT ((0))");
    await EnsureColumnAsync(db, "weighing_session_images", "CapturedAt", "datetime2 NOT NULL CONSTRAINT [DF_weighing_session_images_captured_at_patch_bootstrap] DEFAULT (sysutcdatetime())");
    await EnsureColumnAsync(db, "weighing_session_images", "CapturedBy", "nvarchar(100) NOT NULL CONSTRAINT [DF_weighing_session_images_captured_by_bootstrap] DEFAULT (N'SYSTEM')");
    await EnsureColumnAsync(db, "weighing_session_images", "SyncStatus", "nvarchar(20) NOT NULL CONSTRAINT [DF_weighing_session_images_sync_status_bootstrap] DEFAULT (N'PENDING')");
    await EnsureColumnAsync(db, "weighing_session_images", "LastSyncAttemptAt", "datetime2 NULL");
    await EnsureColumnAsync(db, "weighing_session_images", "LastSyncSuccessAt", "datetime2 NULL");
    await EnsureColumnAsync(db, "weighing_session_images", "LastSyncError", "nvarchar(1000) NULL");
    await EnsureColumnAsync(db, "weighing_session_images", "RetryCount", "int NOT NULL CONSTRAINT [DF_weighing_session_images_retry_count_bootstrap] DEFAULT ((0))");
    await EnsureColumnAsync(db, "weighing_session_images", "IsDeleted", "bit NOT NULL CONSTRAINT [DF_weighing_session_images_is_deleted_patch_bootstrap] DEFAULT ((0))");
    await EnsureColumnAsync(db, "weighing_session_images", "DeletedAt", "datetime2 NULL");
    await EnsureColumnAsync(db, "weighing_session_images", "DeletedBy", "nvarchar(100) NULL");
    await EnsureColumnAsync(db, "weighing_session_images", "CreatedAt", "datetime2 NOT NULL CONSTRAINT [DF_weighing_session_images_created_at_bootstrap] DEFAULT (sysutcdatetime())");
    await EnsureColumnAsync(db, "weighing_session_images", "CreatedBy", "nvarchar(100) NOT NULL CONSTRAINT [DF_weighing_session_images_created_by_bootstrap] DEFAULT (N'SYSTEM')");
    await EnsureColumnAsync(db, "weighing_session_images", "UpdatedAt", "datetime2 NULL");
    await EnsureColumnAsync(db, "weighing_session_images", "UpdatedBy", "nvarchar(100) NULL");
}

static Task EnsureColumnAsync(CentralSyncDbContext db, string tableName, string columnName, string sqlDefinition)
{
    var sql = $"""
    IF COL_LENGTH('{tableName}', '{columnName}') IS NULL
    BEGIN
        ALTER TABLE [{tableName}] ADD [{columnName}] {sqlDefinition};
    END
    """;

    return db.Database.ExecuteSqlRawAsync(sql);
}

static Task EnsureDeliveryTicketSyncStatusSchemaAsync(CentralSyncDbContext db)
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

    return db.Database.ExecuteSqlRawAsync(sql);
}

static string? GetSqlConnectionPart(string connectionString, string key)
{
    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
    return builder.ContainsKey(key) ? builder[key]?.ToString() : null;
}
