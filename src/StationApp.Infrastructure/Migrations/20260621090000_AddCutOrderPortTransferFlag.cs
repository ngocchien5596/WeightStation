using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCutOrderPortTransferFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
   AND COL_LENGTH('cut_orders', 'IsPortTransfer') IS NULL
BEGIN
    ALTER TABLE [cut_orders]
    ADD [IsPortTransfer] bit NOT NULL
        CONSTRAINT [DF_cut_orders_is_port_transfer] DEFAULT ((0));
END

IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
   AND COL_LENGTH('cut_orders', 'IsPortTransfer') IS NOT NULL
   AND COL_LENGTH('cut_orders', 'TransactionType') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_cut_orders_station_port_transfer' AND object_id = OBJECT_ID(N'[cut_orders]'))
BEGIN
    CREATE INDEX [IX_cut_orders_station_port_transfer]
    ON [cut_orders]([StationCode], [IsPortTransfer], [TransactionType], [IsDeleted]);
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_cut_orders_station_port_transfer' AND object_id = OBJECT_ID(N'[cut_orders]'))
BEGIN
    DROP INDEX [IX_cut_orders_station_port_transfer] ON [cut_orders];
END

IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
   AND COL_LENGTH('cut_orders', 'IsPortTransfer') IS NOT NULL
BEGIN
    DECLARE @DefaultConstraintName sysname;

    SELECT @DefaultConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[cut_orders]')
      AND c.name = N'IsPortTransfer';

    IF @DefaultConstraintName IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [cut_orders] DROP CONSTRAINT [' + @DefaultConstraintName + N']');
    END

    ALTER TABLE [cut_orders] DROP COLUMN [IsPortTransfer];
END
""");
        }
    }
}
