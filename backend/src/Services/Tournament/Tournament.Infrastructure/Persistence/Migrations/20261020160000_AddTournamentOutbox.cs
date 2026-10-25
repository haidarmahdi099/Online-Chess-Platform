using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tournament.Infrastructure.Persistence;

#nullable disable
namespace Tournament.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TournamentDbContext))]
[Migration("20261020160000_AddTournamentOutbox")]
public sealed class AddTournamentOutbox : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "tournament_outbox_messages", columns: table => new
        {
            EventId = table.Column<Guid>(type: "uuid", nullable: false),
            EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
            TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
            DataJson = table.Column<string>(type: "text", nullable: false),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Attempts = table.Column<int>(type: "integer", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
        }, constraints: table => table.PrimaryKey("PK_tournament_outbox_messages", x => x.EventId));
        m.CreateIndex("IX_tournament_outbox_messages_Status_CreatedAtUtc", "tournament_outbox_messages", new[] { "Status", "CreatedAtUtc" });
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("tournament_outbox_messages");
}
