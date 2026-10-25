using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Tournament.Application.Interfaces;
using Tournament.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Tournament.IntegrationTests;

public sealed class TournamentApiFixture : IAsyncLifetime
{
    public const string JwtSecret = "tournament-integration-test-secret-at-least-32-bytes";
    public const string InternalServiceKey = "tournament-integration-service-key-at-least-32-bytes";
    public FakeGameProvisioner GameProvisioner { get; } = new();
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("tournament_integration")
        .WithUsername("tournament_test")
        .WithPassword("tournament_test_password")
        .Build();
    private TournamentApiFactory? _factory;

    public HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("Fixture is not initialized."))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new TournamentApiFactory(GameProvisioner);
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings__TournamentDatabase"] = _postgres.GetConnectionString(),
            ["Database__MigrateOnStartup"] = "true",
            ["Jwt__SecretKey"] = JwtSecret,
            ["Jwt__Issuer"] = "ChessPlatform.Identity.Tests",
            ["Jwt__Audience"] = "ChessPlatform.Identity.Tests",
            ["InternalServices__TournamentKey"] = InternalServiceKey,
            ["Tournament__BackgroundWorkersEnabled"] = "false"
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

    public async Task ResetDatabaseAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync();
        await db.TournamentMatches.ExecuteDeleteAsync();
        await db.TournamentParticipants.ExecuteDeleteAsync();
        await db.Tournaments.ExecuteDeleteAsync();
        GameProvisioner.Reset();
    }

    public async Task SetStartTimeInPastAsync(Guid tournamentId)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        await db.Tournaments.Where(t => t.Id == tournamentId)
            .ExecuteUpdateAsync(update => update.SetProperty(t => t.StartsAtUtc, DateTime.UtcNow.AddMinutes(-1)));
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed class TournamentApiFactory(FakeGameProvisioner provisioner) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IGameProvisioner>();
                services.AddSingleton<IGameProvisioner>(provisioner);
            });
        }
    }
}

public sealed class FakeGameProvisioner : IGameProvisioner
{
    public int Calls { get; private set; }

    public Task<ProvisionResult> ProvisionAsync(PendingProvision match, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(new ProvisionResult(ProvisionOutcome.Created, Guid.NewGuid()));
    }

    public void Reset() => Calls = 0;
}
