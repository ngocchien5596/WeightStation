using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    public partial class FixWeighTicketPrimaryDisplayDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DECLARE @constraintName sysname;
SELECT @constraintName = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
INNER JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = 'weigh_tickets' AND c.name = 'IsPrimaryDisplay';

IF @constraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [weigh_tickets] DROP CONSTRAINT [' + @constraintName + ']');
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'weigh_tickets' AND c.name = 'IsPrimaryDisplay')
BEGIN
    ALTER TABLE [weigh_tickets] ADD DEFAULT ((1)) FOR [IsPrimaryDisplay];
END
""");
        }
    }
}
