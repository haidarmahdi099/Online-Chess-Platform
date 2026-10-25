using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Identity.Infrastructure.Data;

#nullable disable
namespace Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261020110000_AddSocialFeatures")]
public partial class AddSocialFeatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "FriendRequests", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
            ReceiverUserId = table.Column<Guid>(type: "uuid", nullable: false),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_FriendRequests", x => x.Id));
        migrationBuilder.CreateTable(name: "UserBlocks", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            BlockerUserId = table.Column<Guid>(type: "uuid", nullable: false),
            BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_UserBlocks", x => x.Id));
        migrationBuilder.CreateTable(name: "UserPreferences", columns: table => new
        {
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            BoardTheme = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            PieceTheme = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            SoundEnabled = table.Column<bool>(type: "boolean", nullable: false),
            PremovesEnabled = table.Column<bool>(type: "boolean", nullable: false),
            ShowOnlineStatus = table.Column<bool>(type: "boolean", nullable: false),
            AllowChallenges = table.Column<bool>(type: "boolean", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_UserPreferences", x => x.UserId));
        migrationBuilder.CreateIndex(name: "IX_FriendRequests_SenderUserId_ReceiverUserId_Status", table: "FriendRequests", columns: new[] { "SenderUserId", "ReceiverUserId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_FriendRequests_ReceiverUserId_Status", table: "FriendRequests", columns: new[] { "ReceiverUserId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_UserBlocks_BlockerUserId_BlockedUserId", table: "UserBlocks", columns: new[] { "BlockerUserId", "BlockedUserId" }, unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("FriendRequests");
        migrationBuilder.DropTable("UserBlocks");
        migrationBuilder.DropTable("UserPreferences");
    }
}
