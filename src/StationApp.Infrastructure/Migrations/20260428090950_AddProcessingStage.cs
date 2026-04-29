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
            migrationBuilder.AddColumn<string>(
                name: "ProcessingStage",
                table: "vehicle_registrations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "IN_YARD");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_registrations_processing_stage",
                table: "vehicle_registrations",
                columns: new[] { "ProcessingStage", "IsCancelled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vehicle_registrations_processing_stage",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "ProcessingStage",
                table: "vehicle_registrations");
        }
    }
}
