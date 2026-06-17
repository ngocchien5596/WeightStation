using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StationApp.Infrastructure.Persistence;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    [DbContext(typeof(StationDbContext))]
    [Migration("20260428113000_AddSoftDeleteToChildTickets")]
    public partial class AddSoftDeleteToChildTickets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('weigh_tickets', 'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [weigh_tickets] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_weigh_tickets_is_deleted] DEFAULT ((0));
END

IF COL_LENGTH('weigh_tickets', 'DeletedAt') IS NULL
BEGIN
    ALTER TABLE [weigh_tickets] ADD [DeletedAt] datetime2 NULL;
END

IF COL_LENGTH('weigh_tickets', 'DeletedBy') IS NULL
BEGIN
    ALTER TABLE [weigh_tickets] ADD [DeletedBy] nvarchar(100) NULL;
END

IF COL_LENGTH('delivery_tickets', 'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [delivery_tickets] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_delivery_tickets_is_deleted] DEFAULT ((0));
END

IF COL_LENGTH('delivery_tickets', 'DeletedAt') IS NULL
BEGIN
    ALTER TABLE [delivery_tickets] ADD [DeletedAt] datetime2 NULL;
END

IF COL_LENGTH('delivery_tickets', 'DeletedBy') IS NULL
BEGIN
    ALTER TABLE [delivery_tickets] ADD [DeletedBy] nvarchar(100) NULL;
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('delivery_tickets', 'DeletedBy') IS NOT NULL
BEGIN
    ALTER TABLE [delivery_tickets] DROP COLUMN [DeletedBy];
END

IF COL_LENGTH('delivery_tickets', 'DeletedAt') IS NOT NULL
BEGIN
    ALTER TABLE [delivery_tickets] DROP COLUMN [DeletedAt];
END

IF COL_LENGTH('delivery_tickets', 'IsDeleted') IS NOT NULL
BEGIN
    DECLARE @deliveryConstraintName sysname;
    SELECT @deliveryConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'delivery_tickets' AND c.name = 'IsDeleted';

    IF @deliveryConstraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [delivery_tickets] DROP CONSTRAINT [' + @deliveryConstraintName + ']');
    END

    ALTER TABLE [delivery_tickets] DROP COLUMN [IsDeleted];
END

IF COL_LENGTH('weigh_tickets', 'DeletedBy') IS NOT NULL
BEGIN
    ALTER TABLE [weigh_tickets] DROP COLUMN [DeletedBy];
END

IF COL_LENGTH('weigh_tickets', 'DeletedAt') IS NOT NULL
BEGIN
    ALTER TABLE [weigh_tickets] DROP COLUMN [DeletedAt];
END

IF COL_LENGTH('weigh_tickets', 'IsDeleted') IS NOT NULL
BEGIN
    DECLARE @weighConstraintName sysname;
    SELECT @weighConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'weigh_tickets' AND c.name = 'IsDeleted';

    IF @weighConstraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [weigh_tickets] DROP CONSTRAINT [' + @weighConstraintName + ']');
    END

    ALTER TABLE [weigh_tickets] DROP COLUMN [IsDeleted];
END
""");
        }
    }
}
