using Game.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Game.Api;

public sealed class GameDbContextFactory
    : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(
                "appsettings.Development.json",
                optional: true)
            // Match normal ASP.NET Core configuration: environment variables override JSON.
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("GameDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "GameDatabase connection string was not found.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<GameDbContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return new GameDbContext(optionsBuilder.Options);
    }
}