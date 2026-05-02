using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    public partial class AddWeighingSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WeighingSessionId",
                table: "vehicle_registrations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WeighingSessionId",
                table: "weigh_tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WeighingSessionId",
                table: "delivery_tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WeighingSessionLineId",
                table: "delivery_tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "weighing_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VehiclePlate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MoocNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Weight1 = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Weight2 = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    NetWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Weight1Time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Weight2Time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SessionStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasPrintedMasterWeighTicket = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighing_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "weighing_session_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeighingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleRegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DistributorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DistributorName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PlannedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    PlannedBagCount = table.Column<int>(type: "int", nullable: true),
                    ActualAllocatedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ActualAllocatedBagCount = table.Column<int>(type: "int", nullable: true),
                    LineStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HasPrintedDeliveryTicket = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeliveryTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighing_session_lines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_weighing_session_id",
                table: "vehicle_registrations",
                column: "WeighingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tickets_weighing_session_line_id",
                table: "delivery_tickets",
                column: "WeighingSessionLineId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_tickets_weighing_session_id",
                table: "delivery_tickets",
                column: "WeighingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_weigh_tickets_weighing_session_id",
                table: "weigh_tickets",
                column: "WeighingSessionId");

            migrationBuilder.CreateIndex(
                name: "UX_weighing_sessions_session_no",
                table: "weighing_sessions",
                column: "SessionNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_sessions_vehicle_plate",
                table: "weighing_sessions",
                column: "VehiclePlate");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_sessions_status",
                table: "weighing_sessions",
                column: "SessionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_sessions_created_at",
                table: "weighing_sessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_session_lines_session_id",
                table: "weighing_session_lines",
                column: "WeighingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_session_lines_registration_id",
                table: "weighing_session_lines",
                column: "VehicleRegistrationId");

            migrationBuilder.CreateIndex(
                name: "UX_weighing_session_lines_session_registration",
                table: "weighing_session_lines",
                columns: new[] { "WeighingSessionId", "VehicleRegistrationId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "weighing_session_lines");
            migrationBuilder.DropTable(name: "weighing_sessions");

            migrationBuilder.DropIndex(name: "IX_vehicle_registrations_weighing_session_id", table: "vehicle_registrations");
            migrationBuilder.DropIndex(name: "IX_delivery_tickets_weighing_session_line_id", table: "delivery_tickets");
            migrationBuilder.DropIndex(name: "IX_delivery_tickets_weighing_session_id", table: "delivery_tickets");
            migrationBuilder.DropIndex(name: "IX_weigh_tickets_weighing_session_id", table: "weigh_tickets");

            migrationBuilder.DropColumn(name: "WeighingSessionId", table: "vehicle_registrations");
            migrationBuilder.DropColumn(name: "WeighingSessionId", table: "weigh_tickets");
            migrationBuilder.DropColumn(name: "WeighingSessionId", table: "delivery_tickets");
            migrationBuilder.DropColumn(name: "WeighingSessionLineId", table: "delivery_tickets");
        }
    }
}
