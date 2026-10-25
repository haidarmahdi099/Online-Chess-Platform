using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Puzzle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPuzzleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "puzzles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fen = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SolutionMoves = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Themes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puzzles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "puzzle_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PuzzleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedMoves = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puzzle_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_puzzle_attempts_puzzles_PuzzleId",
                        column: x => x.PuzzleId,
                        principalTable: "puzzles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_puzzle_attempts_PuzzleId",
                table: "puzzle_attempts",
                column: "PuzzleId");

            migrationBuilder.CreateIndex(
                name: "IX_puzzle_attempts_UserId_AttemptedAtUtc",
                table: "puzzle_attempts",
                columns: new[] { "UserId", "AttemptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_puzzles_IsActive_Rating",
                table: "puzzles",
                columns: new[] { "IsActive", "Rating" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "puzzle_attempts");

            migrationBuilder.DropTable(
                name: "puzzles");
        }
    }
}
