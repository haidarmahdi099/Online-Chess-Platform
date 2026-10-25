using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Puzzle.Infrastructure.Persistence;

#nullable disable

namespace Puzzle.Infrastructure.Migrations;

[DbContext(typeof(PuzzleDbContext))]
[Migration("20261022120000_AddPuzzleRatingAwards")]
public sealed class AddPuzzleRatingAwards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "puzzle_rating_awards",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                PuzzleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_puzzle_rating_awards", x => new { x.UserId, x.PuzzleId });
                table.ForeignKey(
                    name: "FK_puzzle_rating_awards_puzzles_PuzzleId",
                    column: x => x.PuzzleId,
                    principalTable: "puzzles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_puzzle_rating_awards_PuzzleId",
            table: "puzzle_rating_awards",
            column: "PuzzleId");

        // Preserve the existing first-attempt/rating behavior for records written before
        // the dedicated idempotency table was introduced.
        migrationBuilder.Sql("""
            INSERT INTO puzzle_rating_awards ("UserId", "PuzzleId")
            SELECT DISTINCT "UserId", "PuzzleId"
            FROM puzzle_attempts
            ON CONFLICT ("UserId", "PuzzleId") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "puzzle_rating_awards");
}
