using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Puzzle.Infrastructure.Persistence;

#nullable disable

namespace Puzzle.Infrastructure.Migrations;

public partial class AddPuzzleOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "puzzle_outbox_messages",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_puzzle_outbox_messages", x => x.EventId));

        migrationBuilder.CreateIndex(
            name: "IX_puzzle_outbox_messages_PublishedAtUtc_CreatedAtUtc",
            table: "puzzle_outbox_messages",
            columns: new[] { "PublishedAtUtc", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "puzzle_outbox_messages");
}
