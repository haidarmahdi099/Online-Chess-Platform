using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_seeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitialTimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    IncrementSeconds = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rated = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_seeks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_seeks_IsActive_Rated_Category_InitialTimeSeconds_Incre~",
                table: "game_seeks",
                columns: new[] { "IsActive", "Rated", "Category", "InitialTimeSeconds", "IncrementSeconds", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_game_seeks_PlayerId",
                table: "game_seeks",
                column: "PlayerId",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_seeks");
        }
    }
}
