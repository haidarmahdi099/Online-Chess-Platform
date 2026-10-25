using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tournament.Infrastructure.Persistence;

#nullable disable

namespace Tournament.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TournamentDbContext))]
[Migration("20261017120000_AddTournamentOwner")]
public sealed class AddTournamentOwner : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByUserId",
            table: "tournaments",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CreatedByUserId", table: "tournaments");
    }
}
