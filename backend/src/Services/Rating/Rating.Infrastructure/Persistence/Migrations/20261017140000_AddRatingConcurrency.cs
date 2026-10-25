using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rating.Infrastructure.Persistence;

#nullable disable

namespace Rating.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RatingDbContext))]
[Migration("20261017140000_AddRatingConcurrency")]
public sealed class AddRatingConcurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // PostgreSQL provides xmin as a system column on every table. It is mapped
        // as the PlayerRating concurrency token by the EF model and must not be
        // created as a regular column here.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // There is no database object to remove: xmin is a PostgreSQL system column.
    }
}
