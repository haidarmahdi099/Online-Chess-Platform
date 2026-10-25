using Analysis.Domain.Chess;
using Analysis.Domain.Evaluation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Analysis.Application.Interfaces;

namespace Analysis.IntegrationTests;

public sealed class AnalysisApiFixture : IAsyncLifetime
{
    public const string JwtSecret = "analysis-integration-test-secret-key-at-least-32-bytes";
    public FakeChessEngine Engine { get; } = new();
    private AnalysisApiFactory? _factory;

    public HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("Fixture is not initialized."))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        _factory = new AnalysisApiFactory(Engine);
        var settings = new Dictionary<string, string?>
        {
            ["Jwt__SecretKey"] = JwtSecret,
            ["Jwt__Issuer"] = "ChessPlatform.Identity.Tests",
            ["Jwt__Audience"] = "ChessPlatform.Identity.Tests"
        };
        var previous = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (key, value) in settings)
                Environment.SetEnvironmentVariable(key, value);

            using var client = _factory.CreateClient();
            using var live = await client.GetAsync("/health/live");
            if (!live.IsSuccessStatusCode)
                throw new InvalidOperationException($"Analysis API startup probe failed ({(int)live.StatusCode}): {await live.Content.ReadAsStringAsync()}");
        }
        finally
        {
            foreach (var (key, value) in previous)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    private sealed class AnalysisApiFactory(FakeChessEngine engine) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChessEngine>();
                services.AddSingleton<IChessEngine>(engine);
            });
        }
    }
}

public sealed class FakeChessEngine : IChessEngine
{
    public PositionEvaluation Evaluation { get; set; } =
        new(ScoreType.Centipawns, 24, 10, "e2e4", new[] { "e2e4", "e7e5" });
    public bool Available { get; set; } = true;
    public bool ThrowUnavailable { get; set; }
    public int EvaluationCount { get; private set; }
    public FenPosition? LastPosition { get; private set; }
    public int? LastDepth { get; private set; }

    public Task<PositionEvaluation> EvaluateAsync(FenPosition position, int depth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EvaluationCount++;
        LastPosition = position;
        LastDepth = depth;
        if (ThrowUnavailable)
            throw new Analysis.Application.Exceptions.EngineUnavailableException("Test engine unavailable.");
        return Task.FromResult(Evaluation);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Available);
    }

    public void Reset()
    {
        EvaluationCount = 0;
        LastPosition = null;
        LastDepth = null;
        ThrowUnavailable = false;
        Available = true;
        Evaluation = new PositionEvaluation(ScoreType.Centipawns, 24, 10, "e2e4", new[] { "e2e4", "e7e5" });
    }
}
