using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_ReArchitecture_RegistrationCentric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VehicleRegistrationId",
                table: "weigh_tickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDeliveryTicketId",
                table: "delivery_tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "delivery_tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleRegistrationId",
                table: "delivery_tickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "vehicle_registrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErpVehicleRegistrationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RegistrationSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RegistrationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransportMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VehiclePlate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MoocNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceiverIdNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PlannedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    BagCount = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasOverweightCase = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CurrentPrimaryWeighTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentPrimaryDeliveryTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_registrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_created_at",
                table: "vehicle_registrations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_registration_status",
                table: "vehicle_registrations",
                column: "RegistrationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_sync_status",
                table: "vehicle_registrations",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_vehicle_plate",
                table: "vehicle_registrations",
                column: "VehiclePlate");

            migrationBuilder.CreateIndex(
                name: "UX_vehicle_registrations_erp_vehicle_registration_id",
                table: "vehicle_registrations",
                column: "ErpVehicleRegistrationId",
                unique: true,
                filter: "[ErpVehicleRegistrationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "VehicleRegistrationId",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "SourceDeliveryTicketId",
                table: "delivery_tickets");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "delivery_tickets");

            migrationBuilder.DropColumn(
                name: "VehicleRegistrationId",
                table: "delivery_tickets");
        }
    }
}
