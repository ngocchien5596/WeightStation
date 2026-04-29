using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    public partial class AddVehicleRegistrationPrintCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('vehicle_registrations', 'CutOrderCode') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [CutOrderCode] nvarchar(100) NULL;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('vehicle_registrations', 'OrderCode') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [OrderCode] nvarchar(100) NULL;
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CutOrderCode",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "OrderCode",
                table: "vehicle_registrations");
        }
    }
}
