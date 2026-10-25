using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tournament.Infrastructure.Persistence;

#nullable disable

namespace Tournament.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TournamentDbContext))]
[Migration("20261017160000_AddTournamentLifecycle")]
public sealed class AddTournamentLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "TotalRounds", table: "tournaments", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "CurrentRound", table: "tournaments", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(name: "StartedAtUtc", table: "tournaments", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "CompletedAtUtc", table: "tournaments", type: "timestamp with time zone", nullable: true);

        migrationBuilder.AddColumn<string>(name: "Result", table: "tournament_matches", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "WinnerId", table: "tournament_matches", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FinishedAtUtc", table: "tournament_matches", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<int>(name: "ProvisionAttempts", table: "tournament_matches", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(name: "NextProvisionAtUtc", table: "tournament_matches", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "LastProvisionError", table: "tournament_matches", type: "character varying(200)", maxLength: 200, nullable: true);

        // Tournaments created before the Swiss lifecycle only ever had a single round of
        // pairings. Describe them as a one-round tournament so they stay consistent.
        migrationBuilder.Sql("""
            UPDATE tournaments t
            SET "CurrentRound" = 1, "TotalRounds" = 1, "StartedAtUtc" = t."StartsAtUtc"
            WHERE t."Status" = 'InProgress'
              AND EXISTS (SELECT 1 FROM tournament_matches m WHERE m."TournamentId" = t."Id");
            """);
        migrationBuilder.Sql("""
            UPDATE tournament_matches
            SET "Result" = 'Bye'
            WHERE "Status" = 'Bye' AND "Result" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LastProvisionError", table: "tournament_matches");
        migrationBuilder.DropColumn(name: "NextProvisionAtUtc", table: "tournament_matches");
        migrationBuilder.DropColumn(name: "ProvisionAttempts", table: "tournament_matches");
        migrationBuilder.DropColumn(name: "FinishedAtUtc", table: "tournament_matches");
        migrationBuilder.DropColumn(name: "WinnerId", table: "tournament_matches");
        migrationBuilder.DropColumn(name: "Result", table: "tournament_matches");

        migrationBuilder.DropColumn(name: "CompletedAtUtc", table: "tournaments");
        migrationBuilder.DropColumn(name: "StartedAtUtc", table: "tournaments");
        migrationBuilder.DropColumn(name: "CurrentRound", table: "tournaments");
        migrationBuilder.DropColumn(name: "TotalRounds", table: "tournaments");
    }
}
