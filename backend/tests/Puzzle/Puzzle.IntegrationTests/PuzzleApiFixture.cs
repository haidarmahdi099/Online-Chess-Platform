using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Puzzle.Domain.Entities;
using Puzzle.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Puzzle.IntegrationTests;

public sealed class PuzzleApiFixture : IAsyncLifetime
{
    public const string JwtSecret = "puzzle-integration-tests-signing-key-at-least-32-bytes";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("puzzle_integration")
        .WithUsername("puzzle_test")
        .WithPassword("puzzle_test_password")
        .Build();
    private PuzzleApiFactory? _factory;

    public HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("Fixture is not initialized."))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new PuzzleApiFactory();

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings__PuzzleDatabase"] = _postgres.GetConnectionString(),
            ["Database__MigrateOnStartup"] = "true",
            ["Jwt__SecretKey"] = JwtSecret,
            ["Jwt__Issuer"] = "ChessPlatform.Identity.Tests",
            ["Jwt__Audience"] = "ChessPlatform.Identity.Tests",
            ["Kafka__BootstrapServers"] = "localhost:9092"
        };
        var previous = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (key, value) in settings)
                Environment.SetEnvironmentVariable(key, value);

            using var client = _factory.CreateClient();
            using var ready = await client.GetAsync("/health/ready");
            ready.EnsureSuccessStatusCode();
        }
        finally
        {
            foreach (var (key, value) in previous)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    public async Task<Guid> SeedPuzzleAsync(
        string solutionMoves = "e2e4 e7e5 g1f3",
        int rating = 1500,
        string themes = "fork,check",
        bool isActive = true)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PuzzleDbContext>();
        var puzzle = new Puzzle.Domain.Entities.Puzzle
        {
            Fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            SolutionMoves = solutionMoves,
            Rating = rating,
            Themes = themes,
            IsActive = isActive
        };
        db.Puzzles.Add(puzzle);
        await db.SaveChangesAsync();
        return puzzle.Id;
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PuzzleDbContext>();
        await db.PuzzleRatingAwards.ExecuteDeleteAsync();
        await db.PuzzleAttempts.ExecuteDeleteAsync();
        await db.OutboxMessages.ExecuteDeleteAsync();
        await db.Puzzles.ExecuteDeleteAsync();
    }

    public async Task<(int Attempts, int OutboxMessages, int RatingAwards)> GetCountsAsync(Guid userId, Guid puzzleId)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PuzzleDbContext>();
        return (
            await db.PuzzleAttempts.CountAsync(a => a.UserId == userId && a.PuzzleId == puzzleId),
            await db.OutboxMessages.CountAsync(m => m.EventType == "puzzle.attempted"),
            await db.PuzzleRatingAwards.CountAsync(a => a.UserId == userId && a.PuzzleId == puzzleId));
    }

    public Task DisposeAsync()
    {
        if (_factory is not null)
            _factory.Dispose();
        return _postgres.DisposeAsync().AsTask();
    }

    private sealed class PuzzleApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        }
    }
}
