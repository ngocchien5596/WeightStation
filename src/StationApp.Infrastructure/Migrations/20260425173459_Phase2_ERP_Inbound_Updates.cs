using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_ERP_Inbound_Updates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InboundErrorCode",
                table: "vehicle_registrations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InboundErrorMessage",
                table: "vehicle_registrations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InboundProcessedAt",
                table: "vehicle_registrations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInboundProcessed",
                table: "vehicle_registrations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InboundErrorCode",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "InboundErrorMessage",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "InboundProcessedAt",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "IsInboundProcessed",
                table: "vehicle_registrations");
        }
    }
}
