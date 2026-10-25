using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rating.Infrastructure.Persistence;

public sealed class RatingDbContextFactory
    : IDesignTimeDbContextFactory<RatingDbContext>
{
    public RatingDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__RatingDatabase")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__RatingDatabase (run '. .\\scripts\\Load-Env.ps1') before running EF design-time commands.");

        var optionsBuilder =
            new DbContextOptionsBuilder<RatingDbContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return new RatingDbContext(optionsBuilder.Options);
    }
}
