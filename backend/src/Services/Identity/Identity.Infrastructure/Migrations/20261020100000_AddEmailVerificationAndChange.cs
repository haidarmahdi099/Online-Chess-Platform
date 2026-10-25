using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Identity.Infrastructure.Data;

#nullable disable
namespace Identity.Infrastructure.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261020100000_AddEmailVerificationAndChange")]
public partial class AddEmailVerificationAndChange : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "EmailVerified", table: "Users", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<string>(name: "EmailVerificationTokenHash", table: "Users", type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "EmailVerificationExpiresAt", table: "Users", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PendingEmail", table: "Users", type: "character varying(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EmailChangeTokenHash", table: "Users", type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "EmailChangeExpiresAt", table: "Users", type: "timestamp with time zone", nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmailVerified", table: "Users");
        migrationBuilder.DropColumn(name: "EmailVerificationTokenHash", table: "Users");
        migrationBuilder.DropColumn(name: "EmailVerificationExpiresAt", table: "Users");
        migrationBuilder.DropColumn(name: "PendingEmail", table: "Users");
        migrationBuilder.DropColumn(name: "EmailChangeTokenHash", table: "Users");
        migrationBuilder.DropColumn(name: "EmailChangeExpiresAt", table: "Users");
    }
}
