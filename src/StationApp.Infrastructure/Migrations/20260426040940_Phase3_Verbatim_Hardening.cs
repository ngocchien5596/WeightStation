using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_Verbatim_Hardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastPrintError",
                table: "weigh_tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPrintedAt",
                table: "weigh_tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInboundAttemptAt",
                table: "vehicle_registrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncAttemptAt",
                table: "vehicle_registrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "vehicle_registrations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPrintError",
                table: "delivery_tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPrintedAt",
                table: "delivery_tickets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPrintError",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "LastPrintedAt",
                table: "weigh_tickets");

            migrationBuilder.DropColumn(
                name: "LastInboundAttemptAt",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "LastSyncAttemptAt",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "LastPrintError",
                table: "delivery_tickets");

            migrationBuilder.DropColumn(
                name: "LastPrintedAt",
                table: "delivery_tickets");
        }
    }
}
