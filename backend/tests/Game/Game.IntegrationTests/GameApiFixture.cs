using System.Net;
using Game.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Game.IntegrationTests;

public sealed class GameApiFixture : IAsyncLifetime
{
    private const string TournamentKey = "integration-tournament-service-key-at-least-32-bytes";
    private readonly PostgreSqlContainer? _postgres = CreatePostgresContainer();
    private readonly string? _externalConnectionString = Environment.GetEnvironmentVariable("GAME_INTEGRATION_TEST_CONNECTION_STRING");
    private GameApiFactory? _factory;

    public string InternalServiceKey => TournamentKey;

    public HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("The test fixture has not started."))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        if (_externalConnectionString is null)
            await _postgres!.StartAsync();

        var connectionString = _externalConnectionString ?? _postgres!.GetConnectionString();
        _factory = new GameApiFactory();

        var environment = new Dictionary<string, string?>
        {
            ["ConnectionStrings__GameDatabase"] = connectionString,
            ["Database__MigrateOnStartup"] = "true",
            ["Jwt__SecretKey"] = "integration-tests-only-signing-key-at-least-32-bytes",
            ["Jwt__Issuer"] = "ChessPlatform.Identity.Tests",
            ["Jwt__Audience"] = "ChessPlatform.Identity.Tests",
            ["InternalServices__TournamentKey"] = TournamentKey,
            ["Kafka__BootstrapServers"] = "localhost:9092"
        };
        var previousEnvironment = environment.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var setting in environment)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);

            using var client = _factory.CreateClient();
            using var response = await client.GetAsync("/health/ready");
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            foreach (var setting in previousEnvironment)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    private static PostgreSqlContainer? CreatePostgresContainer() =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GAME_INTEGRATION_TEST_CONNECTION_STRING"))
            ? new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("game_integration")
                .WithUsername("game_test")
                .WithPassword("game_test_password")
                .Build()
            : null;

    private sealed class GameApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                // The HTTP API tests use a fake Identity eligibility response and do not exercise Kafka.
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory, TestIdentityHttpClientFactory>();
            });
        }
    }
}

internal sealed class TestIdentityHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new ChallengeEligibilityHandler())
    {
        BaseAddress = new Uri("http://identity.test/")
    };
}

internal sealed class ChallengeEligibilityHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { allowed = true, reason = (string?)null })
        });
}
