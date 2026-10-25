using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpectatorConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "games");

            migrationBuilder.AddColumn<bool>(
                name: "BlackSpectatorConsent",
                table: "games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SpectatorConsentRejected",
                table: "games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WhiteSpectatorConsent",
                table: "games",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlackSpectatorConsent",
                table: "games");

            migrationBuilder.DropColumn(
                name: "SpectatorConsentRejected",
                table: "games");

            migrationBuilder.DropColumn(
                name: "WhiteSpectatorConsent",
                table: "games");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "games",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
