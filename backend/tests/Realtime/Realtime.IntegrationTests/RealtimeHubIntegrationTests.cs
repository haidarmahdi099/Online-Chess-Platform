using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Realtime.IntegrationTests;

public sealed class RealtimeHubIntegrationTests : IClassFixture<RealtimeApplicationFactory>
{
    private readonly HttpClient _client;

    public RealtimeHubIntegrationTests(RealtimeApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Negotiate_requires_a_valid_authenticated_user()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/game/negotiate?negotiateVersion=1");

        using var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_can_negotiate_signalr_connection()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/game/negotiate?negotiateVersion=1");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken());

        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<NegotiateResponse>();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.ConnectionId);
        Assert.NotNull(body?.ConnectionToken);
        Assert.Contains(body!.AvailableTransports, transport => transport.Transport == "WebSockets");
    }

    private static string CreateToken()
    {
        const string secret = "realtime-integration-test-secret-key-32-bytes-minimum";
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "ChessPlatform.Identity",
            audience: "ChessPlatform",
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, "integration-user") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record NegotiateResponse(
        string? ConnectionId,
        string? ConnectionToken,
        IReadOnlyList<AvailableTransport> AvailableTransports);

    private sealed record AvailableTransport(string Transport);
}

public sealed class RealtimeApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestSecret = "realtime-integration-test-secret-key-32-bytes-minimum";
    private readonly Dictionary<string, string?> _previousEnvironment = new();

    public RealtimeApplicationFactory()
    {
        SetEnvironment("Jwt__SecretKey", TestSecret);
        SetEnvironment("Jwt__Issuer", "ChessPlatform.Identity");
        SetEnvironment("Jwt__Audience", "ChessPlatform");
        SetEnvironment("GameService__BaseUrl", "http://127.0.0.1:59999/");
        SetEnvironment("Kafka__BootstrapServers", "127.0.0.1:59998");
        SetEnvironment("SignalR__Redis__RequiredForMultiInstance", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestSecret,
                ["Jwt:Issuer"] = "ChessPlatform.Identity",
                ["Jwt:Audience"] = "ChessPlatform",
                ["GameService:BaseUrl"] = "http://127.0.0.1:59999/",
                ["Kafka:BootstrapServers"] = "127.0.0.1:59998",
                ["SignalR:Redis:RequiredForMultiInstance"] = "false"
            }));
        builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        foreach (var (key, value) in _previousEnvironment)
            Environment.SetEnvironmentVariable(key, value);
    }

    private void SetEnvironment(string key, string value)
    {
        _previousEnvironment[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }
}
