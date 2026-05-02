using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StationApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('users', 'PasswordHash') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE [users]
        SET [PasswordHash] = ''$2a$11$163R2ooQUQJV1vz3PUT.suSHkSkXzm9uqReEbooIM8MoZayruJsAm''
        WHERE [Id] = ''00000000-0000-0000-0000-000000000001'';';
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('users', 'PasswordHash') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE [users]
        SET [PasswordHash] = NULL
        WHERE [Id] = ''00000000-0000-0000-0000-000000000001'';';
END
""");
        }
    }
}
