using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261018120000_AddCaseInsensitiveUsernameIndex")]
public partial class AddCaseInsensitiveUsernameIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Fail with a clear message instead of a cryptic index error if case-variant duplicates exist.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM "Users" GROUP BY lower("Username") HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Cannot add the case-insensitive username index: some usernames differ only by case. Rename or delete the duplicates first.';
                END IF;
            END $$;
            """);

        // Not part of the EF model (EF cannot express a lower() index), so the model snapshot is unchanged.
        migrationBuilder.Sql("""CREATE UNIQUE INDEX "IX_Users_Username_Lower" ON "Users" (lower("Username"));""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX "IX_Users_Username_Lower";""");
    }
}