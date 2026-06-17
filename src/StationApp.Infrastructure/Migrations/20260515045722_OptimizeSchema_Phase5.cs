using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeSchema_Phase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'LastInboundAttemptAt') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [LastInboundAttemptAt];
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'LastInboundAttemptAt') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [LastInboundAttemptAt];
END
""");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "device_configs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "device_configs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "app_config",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "app_config",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "app_config",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "device_baudrate",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "device_com_port",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "device_parser_type",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "retry_base_seconds",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "station_code",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "sync_interval_seconds",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "ticket_prefix",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "tolerance_kg",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.UpdateData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "OverweightSplitStepWeight",
                columns: new[] { "CreatedAt", "CreatedBy", "UpdatedBy" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tickets_sync_status",
                table: "delivery_tickets",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tickets_vehicle_registration_id",
                table: "delivery_tickets",
                column: "VehicleRegistrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_delivery_tickets_sync_status",
                table: "delivery_tickets");

            migrationBuilder.DropIndex(
                name: "IX_delivery_tickets_vehicle_registration_id",
                table: "delivery_tickets");

            migrationBuilder.DeleteData(
                table: "app_config",
                keyColumn: "ConfigKey",
                keyValue: "OverweightSplitStepWeight");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "device_configs");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "device_configs");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "app_config");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "app_config");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "app_config");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'LastInboundAttemptAt') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [LastInboundAttemptAt] datetime2 NULL;
END

IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'LastInboundAttemptAt') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [LastInboundAttemptAt] datetime2 NULL;
END
""");
        }
    }
}
