using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGameSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_chat_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_chat_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_clocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhiteRemainingMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    BlackRemainingMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    TurnStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhiteToMove = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_clocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_moves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MoveNumber = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    To = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Promotion = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    FenAfterMove = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_moves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WhitePlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlackPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WinnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndReason = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InitialTimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    IncrementSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_chat_messages_GameId_CreatedAt",
                table: "game_chat_messages",
                columns: new[] { "GameId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_game_clocks_GameId",
                table: "game_clocks",
                column: "GameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_moves_GameId_CreatedAt",
                table: "game_moves",
                columns: new[] { "GameId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_game_moves_GameId_MoveNumber",
                table: "game_moves",
                columns: new[] { "GameId", "MoveNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_games_BlackPlayerId",
                table: "games",
                column: "BlackPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_games_Status",
                table: "games",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_games_WhitePlayerId",
                table: "games",
                column: "WhitePlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_chat_messages");

            migrationBuilder.DropTable(
                name: "game_clocks");

            migrationBuilder.DropTable(
                name: "game_moves");

            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
