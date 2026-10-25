using Game.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Game.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261020150000_AddChallengesAndRematches")]
public sealed class AddChallengesAndRematches : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "game_challenges", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            ChallengerUserId = table.Column<Guid>(type: "uuid", nullable: false),
            OpponentUserId = table.Column<Guid>(type: "uuid", nullable: false),
            InitialTimeSeconds = table.Column<int>(type: "integer", nullable: false),
            IncrementSeconds = table.Column<int>(type: "integer", nullable: false),
            Rated = table.Column<bool>(type: "boolean", nullable: false),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            GameId = table.Column<Guid>(type: "uuid", nullable: true),
            CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_game_challenges", x => x.Id));
        m.CreateIndex("IX_game_challenges_OpponentUserId_Status_CreatedAtUtc", "game_challenges", new[] { "OpponentUserId", "Status", "CreatedAtUtc" });
        m.CreateIndex("IX_game_challenges_ChallengerUserId_Status_CreatedAtUtc", "game_challenges", new[] { "ChallengerUserId", "Status", "CreatedAtUtc" });
        m.CreateTable(name: "game_rematch_requests", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            OriginalGameId = table.Column<Guid>(type: "uuid", nullable: false),
            RequesterUserId = table.Column<Guid>(type: "uuid", nullable: false),
            OpponentUserId = table.Column<Guid>(type: "uuid", nullable: false),
            InitialTimeSeconds = table.Column<int>(type: "integer", nullable: false),
            IncrementSeconds = table.Column<int>(type: "integer", nullable: false),
            Rated = table.Column<bool>(type: "boolean", nullable: false),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            RematchGameId = table.Column<Guid>(type: "uuid", nullable: true),
            CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_game_rematch_requests", x => x.Id));
        m.CreateIndex("IX_game_rematch_requests_OriginalGameId_CreatedAtUtc", "game_rematch_requests", new[] { "OriginalGameId", "CreatedAtUtc" });
        m.CreateIndex("IX_game_rematch_requests_OpponentUserId_Status_CreatedAtUtc", "game_rematch_requests", new[] { "OpponentUserId", "Status", "CreatedAtUtc" });
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("game_rematch_requests"); m.DropTable("game_challenges");
    }
}
