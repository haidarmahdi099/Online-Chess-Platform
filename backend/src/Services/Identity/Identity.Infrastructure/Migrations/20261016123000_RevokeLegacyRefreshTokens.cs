using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Migrations;

public partial class RevokeLegacyRefreshTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"RefreshTokens\" SET \"RevokedAt\" = CURRENT_TIMESTAMP WHERE \"RevokedAt\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
