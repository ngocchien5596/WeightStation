using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StationApp.Infrastructure.Persistence;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    [DbContext(typeof(StationDbContext))]
    [Migration("20260428083500_AddVehicleRegistrationPrintCodes")]
    public partial class AddVehicleRegistrationPrintCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'CutOrderCode') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [CutOrderCode] nvarchar(100) NULL;
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'CutOrderCode') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [CutOrderCode] nvarchar(100) NULL;
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'OrderCode') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [OrderCode] nvarchar(100) NULL;
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'OrderCode') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [OrderCode] nvarchar(100) NULL;
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'CutOrderCode') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [CutOrderCode];
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'CutOrderCode') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [CutOrderCode];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'OrderCode') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [OrderCode];
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'OrderCode') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [OrderCode];
END
""");
        }
    }
}
