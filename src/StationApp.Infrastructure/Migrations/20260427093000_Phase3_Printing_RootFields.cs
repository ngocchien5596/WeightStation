using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    public partial class Phase3_Printing_RootFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('vehicle_registrations', 'ConsumptionPlace') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [ConsumptionPlace] nvarchar(255) NULL;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('vehicle_registrations', 'LoadingPlace') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [LoadingPlace] nvarchar(255) NULL;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('vehicle_registrations', 'LotNo') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [LotNo] nvarchar(100) NULL;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('vehicle_registrations', 'RepresentativeName') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [RepresentativeName] nvarchar(150) NULL;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('vehicle_registrations', 'SealNo') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [SealNo] nvarchar(100) NULL;
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsumptionPlace",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "LoadingPlace",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "LotNo",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "RepresentativeName",
                table: "vehicle_registrations");

            migrationBuilder.DropColumn(
                name: "SealNo",
                table: "vehicle_registrations");
        }
    }
}
