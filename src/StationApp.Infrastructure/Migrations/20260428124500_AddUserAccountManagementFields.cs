using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StationApp.Infrastructure.Persistence;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    [DbContext(typeof(StationDbContext))]
    [Migration("20260428124500_AddUserAccountManagementFields")]
    public partial class AddUserAccountManagementFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('users', 'PasswordHash') IS NULL
BEGIN
    ALTER TABLE [users] ADD [PasswordHash] nvarchar(255) NULL;
END

IF COL_LENGTH('users', 'LastLoginAt') IS NULL
BEGIN
    ALTER TABLE [users] ADD [LastLoginAt] datetime2 NULL;
END

IF COL_LENGTH('users', 'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [users] ADD [CreatedBy] nvarchar(100) NULL;
END

IF COL_LENGTH('users', 'UpdatedBy') IS NULL
BEGIN
    ALTER TABLE [users] ADD [UpdatedBy] nvarchar(100) NULL;
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('users', 'UpdatedBy') IS NOT NULL
BEGIN
    ALTER TABLE [users] DROP COLUMN [UpdatedBy];
END

IF COL_LENGTH('users', 'CreatedBy') IS NOT NULL
BEGIN
    ALTER TABLE [users] DROP COLUMN [CreatedBy];
END

IF COL_LENGTH('users', 'LastLoginAt') IS NOT NULL
BEGIN
    ALTER TABLE [users] DROP COLUMN [LastLoginAt];
END

IF COL_LENGTH('users', 'PasswordHash') IS NOT NULL
BEGIN
    ALTER TABLE [users] DROP COLUMN [PasswordHash];
END
""");
        }
    }
}
