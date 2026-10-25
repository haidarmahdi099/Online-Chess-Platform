using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Puzzle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalPuzzleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalPuzzleId",
                table: "puzzles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalPuzzleId",
                table: "puzzles");
        }
    }
}
