using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StationApp.Infrastructure.Persistence;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    [DbContext(typeof(StationDbContext))]
    [Migration("20260427093000_Phase3_Printing_RootFields")]
    public partial class Phase3_Printing_RootFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'ConsumptionPlace') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [ConsumptionPlace] nvarchar(255) NULL;
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'ConsumptionPlace') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [ConsumptionPlace] nvarchar(255) NULL;
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'LoadingPlace') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [LoadingPlace] nvarchar(255) NULL;
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'LoadingPlace') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [LoadingPlace] nvarchar(255) NULL;
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'LotNo') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [LotNo] nvarchar(100) NULL;
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'LotNo') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [LotNo] nvarchar(100) NULL;
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'RepresentativeName') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [RepresentativeName] nvarchar(150) NULL;
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'RepresentativeName') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [RepresentativeName] nvarchar(150) NULL;
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'SealNo') IS NULL
BEGIN
    ALTER TABLE [vehicle_registrations] ADD [SealNo] nvarchar(100) NULL;
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'SealNo') IS NULL
BEGIN
    ALTER TABLE [cut_orders] ADD [SealNo] nvarchar(100) NULL;
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'ConsumptionPlace') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [ConsumptionPlace];
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'ConsumptionPlace') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [ConsumptionPlace];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'LoadingPlace') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [LoadingPlace];
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'LoadingPlace') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [LoadingPlace];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'LotNo') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [LotNo];
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'LotNo') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [LotNo];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'RepresentativeName') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [RepresentativeName];
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'RepresentativeName') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [RepresentativeName];
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('vehicle_registrations', 'U') IS NOT NULL AND COL_LENGTH('vehicle_registrations', 'SealNo') IS NOT NULL
BEGIN
    ALTER TABLE [vehicle_registrations] DROP COLUMN [SealNo];
END
IF OBJECT_ID('cut_orders', 'U') IS NOT NULL AND COL_LENGTH('cut_orders', 'SealNo') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [SealNo];
END
""");
        }
    }
}
