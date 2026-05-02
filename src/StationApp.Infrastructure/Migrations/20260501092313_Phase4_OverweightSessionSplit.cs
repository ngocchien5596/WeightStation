using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_OverweightSessionSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TtcpWeightSnapshot",
                table: "weigh_tickets",
                newName: "Ttcp10WeightSnapshot");

            migrationBuilder.AddColumn<int>(
                name: "AllocatedBagCount",
                table: "delivery_tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedWeight",
                table: "delivery_tickets",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOverweight",
                table: "weighing_sessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OverweightAmount",
                table: "weighing_sessions",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverweightResolvedAt",
                table: "weighing_sessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverweightResolvedBy",
                table: "weighing_sessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverweightResolutionStatus",
                table: "weighing_sessions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NOT_APPLICABLE");

            migrationBuilder.AddColumn<decimal>(
                name: "Ttcp10WeightSnapshot",
                table: "weighing_sessions",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecordRole",
                table: "weigh_tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MASTER_SESSION",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "WORKING");

            migrationBuilder.AlterColumn<string>(
                name: "RecordRole",
                table: "delivery_tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NORMAL",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "WORKING");

            migrationBuilder.Sql("UPDATE weigh_tickets SET RecordRole = 'MASTER_SESSION' WHERE RecordRole = 'WORKING';");
            migrationBuilder.Sql("UPDATE delivery_tickets SET RecordRole = 'NORMAL' WHERE RecordRole = 'WORKING';");
            migrationBuilder.Sql("UPDATE weighing_session_lines SET LineStatus = 'ALLOCATED' WHERE LineStatus = 'PRINTED';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RecordRole",
                table: "weigh_tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "WORKING",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "MASTER_SESSION");

            migrationBuilder.AlterColumn<string>(
                name: "RecordRole",
                table: "delivery_tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "WORKING",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "NORMAL");

            migrationBuilder.DropColumn(
                name: "AllocatedBagCount",
                table: "delivery_tickets");

            migrationBuilder.DropColumn(
                name: "AllocatedWeight",
                table: "delivery_tickets");

            migrationBuilder.DropColumn(
                name: "IsOverweight",
                table: "weighing_sessions");

            migrationBuilder.DropColumn(
                name: "OverweightAmount",
                table: "weighing_sessions");

            migrationBuilder.DropColumn(
                name: "OverweightResolvedAt",
                table: "weighing_sessions");

            migrationBuilder.DropColumn(
                name: "OverweightResolvedBy",
                table: "weighing_sessions");

            migrationBuilder.DropColumn(
                name: "OverweightResolutionStatus",
                table: "weighing_sessions");

            migrationBuilder.DropColumn(
                name: "Ttcp10WeightSnapshot",
                table: "weighing_sessions");

            migrationBuilder.RenameColumn(
                name: "Ttcp10WeightSnapshot",
                table: "weigh_tickets",
                newName: "TtcpWeightSnapshot");
        }
    }
}
