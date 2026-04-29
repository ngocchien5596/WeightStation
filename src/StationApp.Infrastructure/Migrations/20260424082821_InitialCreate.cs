using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_config",
                columns: table => new
                {
                    ConfigKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfigValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_config", x => x.ConfigKey);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ComPort = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Baudrate = table.Column<int>(type: "int", nullable: false),
                    Parity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DataBits = table.Column<int>(type: "int", nullable: true),
                    StopBits = table.Column<int>(type: "int", nullable: true),
                    FrameEndChar = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ParserType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StabilityThreshold = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    StableCycles = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "weigh_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErpVehicleRegistrationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VehiclePlate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MoocNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PlannedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    BagCount = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransportMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Weight1 = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Weight1User = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Weight1Time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Weight1UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Weight1Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Weight1IsStable = table.Column<bool>(type: "bit", nullable: true),
                    Weight2 = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Weight2User = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Weight2Time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Weight2UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Weight2Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Weight2IsStable = table.Column<bool>(type: "bit", nullable: true),
                    NetWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weigh_tickets", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "app_config",
                columns: new[] { "ConfigKey", "ConfigValue", "UpdatedAt" },
                values: new object[,]
                {
                    { "device_baudrate", "9600", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "device_com_port", "COM1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "device_parser_type", "DEFAULT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "retry_base_seconds", "30", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "station_code", "QN01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "sync_interval_seconds", "30", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "ticket_prefix", "QN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "tolerance_kg", "500", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "IsActive", "RoleCode", "UpdatedAt", "Username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Administrator", true, "ADMIN", null, "admin" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityType_EntityId",
                table: "audit_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_outbox_aggregate_id",
                table: "sync_outbox",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_outbox_idempotency_key",
                table: "sync_outbox",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_sync_outbox_status_next_retry",
                table: "sync_outbox",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weigh_tickets_created_at",
                table: "weigh_tickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_weigh_tickets_erp_vehicle_registration_id",
                table: "weigh_tickets",
                column: "ErpVehicleRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_weigh_tickets_status",
                table: "weigh_tickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_weigh_tickets_sync_status",
                table: "weigh_tickets",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_weigh_tickets_vehicle_plate",
                table: "weigh_tickets",
                column: "VehiclePlate");

            migrationBuilder.CreateIndex(
                name: "UX_weigh_tickets_idempotency_key",
                table: "weigh_tickets",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_weigh_tickets_ticket_no",
                table: "weigh_tickets",
                column: "TicketNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_config");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "device_configs");

            migrationBuilder.DropTable(
                name: "sync_outbox");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "weigh_tickets");
        }
    }
}
