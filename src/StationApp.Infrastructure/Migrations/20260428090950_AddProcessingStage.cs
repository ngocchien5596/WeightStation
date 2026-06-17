using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'ProcessingStage') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [ProcessingStage] nvarchar(30) NOT NULL CONSTRAINT [DF_vehicle_registrations_processing_stage] DEFAULT ('IN_YARD');
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'ProcessingStage') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [ProcessingStage] nvarchar(30) NOT NULL CONSTRAINT [DF_cut_orders_processing_stage] DEFAULT ('IN_YARD');
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicle_registrations_processing_stage' AND object_id = OBJECT_ID('vehicle_registrations'))
BEGIN
    CREATE INDEX [IX_vehicle_registrations_processing_stage] ON [vehicle_registrations]([ProcessingStage], [IsCancelled]);
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicle_registrations_processing_stage' AND object_id = OBJECT_ID('cut_orders'))
BEGIN
    CREATE INDEX [IX_vehicle_registrations_processing_stage] ON [cut_orders]([ProcessingStage], [IsCancelled]);
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicle_registrations_processing_stage' AND object_id = OBJECT_ID('vehicle_registrations'))
BEGIN
    DROP INDEX [IX_vehicle_registrations_processing_stage] ON [vehicle_registrations];
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vehicle_registrations_processing_stage' AND object_id = OBJECT_ID('cut_orders'))
BEGIN
    DROP INDEX [IX_vehicle_registrations_processing_stage] ON [cut_orders];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'ProcessingStage') IS NOT NULL
BEGIN
    DECLARE @constraintName sysname;
    SELECT @constraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'vehicle_registrations' AND c.name = 'ProcessingStage';

    IF @constraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [vehicle_registrations] DROP CONSTRAINT [' + @constraintName + ']');
    END
    ALTER TABLE [vehicle_registrations] DROP COLUMN [ProcessingStage];
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'ProcessingStage') IS NOT NULL
BEGIN
    DECLARE @constraintName sysname;
    SELECT @constraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'cut_orders' AND c.name = 'ProcessingStage';

    IF @constraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [cut_orders] DROP CONSTRAINT [' + @constraintName + ']');
    END
    ALTER TABLE [cut_orders] DROP COLUMN [ProcessingStage];
END
""");
        }
    }
}
