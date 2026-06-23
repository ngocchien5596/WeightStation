using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportBagCountConfirmationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'SystemCalculatedBagCount') IS NULL
BEGIN
    ALTER TABLE [weighing_session_lines] ADD [SystemCalculatedBagCount] int NULL;
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'BagCountConfirmedAt') IS NULL
BEGIN
    ALTER TABLE [weighing_session_lines] ADD [BagCountConfirmedAt] datetime2 NULL;
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'BagCountConfirmedBy') IS NULL
BEGIN
    ALTER TABLE [weighing_session_lines] ADD [BagCountConfirmedBy] nvarchar(100) NULL;
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'BagCountConfirmationMode') IS NULL
BEGIN
    ALTER TABLE [weighing_session_lines] ADD [BagCountConfirmationMode] nvarchar(50) NULL;
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'BagCountConfirmationMode') IS NOT NULL
BEGIN
    ALTER TABLE [weighing_session_lines] DROP COLUMN [BagCountConfirmationMode];
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'BagCountConfirmedBy') IS NOT NULL
BEGIN
    ALTER TABLE [weighing_session_lines] DROP COLUMN [BagCountConfirmedBy];
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'BagCountConfirmedAt') IS NOT NULL
BEGIN
    ALTER TABLE [weighing_session_lines] DROP COLUMN [BagCountConfirmedAt];
END

IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'SystemCalculatedBagCount') IS NOT NULL
BEGIN
    ALTER TABLE [weighing_session_lines] DROP COLUMN [SystemCalculatedBagCount];
END
""");
        }
    }
}
