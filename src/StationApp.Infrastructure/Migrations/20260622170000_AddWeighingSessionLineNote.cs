using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeighingSessionLineNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'Note') IS NULL
BEGIN
    ALTER TABLE [weighing_session_lines] ADD [Note] nvarchar(500) NULL;
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[weighing_session_lines]', N'U') IS NOT NULL
   AND COL_LENGTH('weighing_session_lines', 'Note') IS NOT NULL
BEGIN
    ALTER TABLE [weighing_session_lines] DROP COLUMN [Note];
END
""");
        }
    }
}
