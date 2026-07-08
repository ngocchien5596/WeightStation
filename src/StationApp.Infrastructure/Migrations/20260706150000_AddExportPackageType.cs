using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportPackageType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
   AND COL_LENGTH('cut_orders', 'ExportPackageType') IS NULL
BEGIN
    ALTER TABLE [cut_orders]
    ADD [ExportPackageType] nvarchar(30) NULL;
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[cut_orders]', N'U') IS NOT NULL
   AND COL_LENGTH('cut_orders', 'ExportPackageType') IS NOT NULL
BEGIN
    ALTER TABLE [cut_orders] DROP COLUMN [ExportPackageType];
END
""");
        }
    }
}
