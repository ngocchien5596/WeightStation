using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    public partial class SeedAdminPasswordHash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
UPDATE [users]
SET [PasswordHash] = '100000.e2bXJfaQ0/3e5H0gRC/n9A==.4d/n6fqXXfblAWtzgDPcgVSOdGwX+acLqtiQgALBEe4='
WHERE [Id] = '00000000-0000-0000-0000-000000000001'
  AND ([PasswordHash] IS NULL OR LTRIM(RTRIM([PasswordHash])) = '');
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
UPDATE [users]
SET [PasswordHash] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000001'
  AND [PasswordHash] = '100000.e2bXJfaQ0/3e5H0gRC/n9A==.4d/n6fqXXfblAWtzgDPcgVSOdGwX+acLqtiQgALBEe4=';
""");
        }
    }
}
