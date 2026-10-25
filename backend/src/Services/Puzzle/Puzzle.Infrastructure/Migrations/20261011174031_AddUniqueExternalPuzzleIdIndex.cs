using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Puzzle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueExternalPuzzleIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_puzzles_ExternalPuzzleId",
                table: "puzzles",
                column: "ExternalPuzzleId",
                unique: true,
                filter: "\"ExternalPuzzleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_puzzles_ExternalPuzzleId",
                table: "puzzles");
        }
    }
}
