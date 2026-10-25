using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tournament.Infrastructure.Persistence;

#nullable disable

namespace Tournament.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TournamentDbContext))]
[Migration("20261017090000_InitialTournamentSchema")]
public sealed class InitialTournamentSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tournaments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                InitialTimeSeconds = table.Column<int>(type: "integer", nullable: false),
                IncrementSeconds = table.Column<int>(type: "integer", nullable: false),
                MaxParticipants = table.Column<int>(type: "integer", nullable: false),
                StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_tournaments", x => x.Id));

        migrationBuilder.CreateTable(
            name: "tournament_participants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_tournament_participants", x => x.Id);
                table.ForeignKey("FK_tournament_participants_tournaments_TournamentId", x => x.TournamentId, "tournaments", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tournament_matches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                RoundNumber = table.Column<int>(type: "integer", nullable: false),
                WhitePlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                BlackPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                GameId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_tournament_matches", x => x.Id);
                table.ForeignKey("FK_tournament_matches_tournaments_TournamentId", x => x.TournamentId, "tournaments", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_tournaments_Status_StartsAtUtc", "tournaments", new[] { "Status", "StartsAtUtc" });
        migrationBuilder.CreateIndex("IX_tournament_participants_TournamentId_UserId", "tournament_participants", new[] { "TournamentId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_tournament_participants_UserId", "tournament_participants", "UserId");
        migrationBuilder.CreateIndex("IX_tournament_matches_TournamentId_RoundNumber", "tournament_matches", new[] { "TournamentId", "RoundNumber" });
        migrationBuilder.CreateIndex("IX_tournament_matches_GameId", "tournament_matches", "GameId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("tournament_matches");
        migrationBuilder.DropTable("tournament_participants");
        migrationBuilder.DropTable("tournaments");
    }
}
