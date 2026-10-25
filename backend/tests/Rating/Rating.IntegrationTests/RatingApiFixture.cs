using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Rating.Application.Events;
using Rating.Application.Services;
using Rating.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Rating.IntegrationTests;

public sealed class RatingApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("rating_integration")
        .WithUsername("rating_test")
        .WithPassword("rating_test_password")
        .Build();
    private RatingApiFactory? _factory;

    public HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("Fixture is not initialized."))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new RatingApiFactory();

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings__RatingDatabase"] = _postgres.GetConnectionString(),
            ["Database__MigrateOnStartup"] = "true",
            ["Kafka__BootstrapServers"] = "localhost:9092"
        };
        var previous = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (key, value) in settings)
                Environment.SetEnvironmentVariable(key, value);

            using var client = _factory.CreateClient();
            using var live = await client.GetAsync("/health/live");
            live.EnsureSuccessStatusCode();
        }
        finally
        {
            foreach (var (key, value) in previous)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RatingDbContext>();
        await db.ProcessedEvents.ExecuteDeleteAsync();
        await db.RatingHistories.ExecuteDeleteAsync();
        await db.PlayerRatings.ExecuteDeleteAsync();
    }

    public async Task ProcessGameAsync(GameFinishedEvent gameEvent)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRatingService>()
            .ProcessGameFinishedAsync(gameEvent);
    }

    public async Task ProcessPuzzleAsync(PuzzleAttemptedEvent puzzleEvent)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRatingService>()
            .ProcessPuzzleAttemptedAsync(puzzleEvent);
    }

    public async Task<(int Ratings, int History, int Events)> GetCountsAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RatingDbContext>();
        return (
            await db.PlayerRatings.CountAsync(),
            await db.RatingHistories.CountAsync(),
            await db.ProcessedEvents.CountAsync());
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed class RatingApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        }
    }
}
