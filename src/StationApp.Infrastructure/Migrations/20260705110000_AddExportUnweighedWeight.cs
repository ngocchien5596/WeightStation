using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportUnweighedWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
   AND COL_LENGTH('cut_orders', 'ExportUnweighedWeight') IS NULL
BEGIN
    ALTER TABLE [cut_orders]
    ADD [ExportUnweighedWeight] decimal(18,3) NOT NULL
        CONSTRAINT [DF_cut_orders_export_unweighed_weight] DEFAULT ((0));
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
   AND COL_LENGTH('cut_orders', 'ExportUnweighedWeight') IS NOT NULL
BEGIN
    DECLARE @DefaultConstraintName sysname;

    SELECT @DefaultConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[cut_orders]')
      AND c.name = N'ExportUnweighedWeight';

    IF @DefaultConstraintName IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [cut_orders] DROP CONSTRAINT [' + @DefaultConstraintName + N']');
    END

    ALTER TABLE [cut_orders] DROP COLUMN [ExportUnweighedWeight];
END
""");
        }
    }
}
