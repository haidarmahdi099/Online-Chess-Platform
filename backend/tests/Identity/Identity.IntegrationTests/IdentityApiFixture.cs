using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Identity.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Net;
using Testcontainers.PostgreSql;

namespace Identity.IntegrationTests;

public sealed class IdentityApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgres = CreatePostgresContainer();
    private readonly string? _externalConnectionString = Environment.GetEnvironmentVariable("IDENTITY_INTEGRATION_TEST_CONNECTION_STRING");

    private IdentityApiFactory? _factory;

    public CapturingEmailSender EmailSender { get; } = new();

    public HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("The test fixture has not started."))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        if (_externalConnectionString is null)
            await _postgres!.StartAsync();
        var connectionString = _externalConnectionString ?? _postgres!.GetConnectionString();
        _factory = new IdentityApiFactory(connectionString, EmailSender);

        // Environment providers can take precedence over the in-memory factory configuration.
        // Apply isolated values for host startup, then restore the test process environment.
        var environment = new Dictionary<string, string?>
        {
            ["ConnectionStrings__IdentityDatabase"] = connectionString,
            ["Database__MigrateOnStartup"] = "true",
            ["Jwt__SecretKey"] = "integration-tests-only-signing-key-at-least-32-bytes",
            ["Jwt__Issuer"] = "ChessPlatform.Identity.Tests",
            ["Jwt__Audience"] = "ChessPlatform.Identity.Tests",
            ["Brevo__ApiKey"] = "integration-test-placeholder",
            ["Brevo__SenderEmail"] = "identity-tests@example.test",
            ["Identity__AdminUsernames__0"] = string.Empty
        };
        var previousEnvironment = environment.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var setting in environment)
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);

            // Force host startup here so migrations fail the fixture, not a random test later.
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
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IDENTITY_INTEGRATION_TEST_CONNECTION_STRING"))
            ? new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("identity_integration")
                .WithUsername("identity_test")
                .WithPassword("identity_test_password")
                .Build()
            : null;

    private sealed class IdentityApiFactory(string connectionString, CapturingEmailSender emailSender)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDatabase"] = connectionString,
                    ["Database:MigrateOnStartup"] = "true",
                    ["Jwt:SecretKey"] = "integration-tests-only-signing-key-at-least-32-bytes",
                    ["Jwt:Issuer"] = "ChessPlatform.Identity.Tests",
                    ["Jwt:Audience"] = "ChessPlatform.Identity.Tests",
                    ["Identity:AdminUsernames:0"] = string.Empty
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(emailSender);
                services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
            });
        }
    }
}

internal sealed class TestClientIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
            await nextMiddleware();
        });
        next(app);
    };
}

public sealed record CapturedEmail(string Recipient, string Subject, string HtmlBody);

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<CapturedEmail> _messages = new();

    public Task SendEmailAsync(string recipientEmail, string subject, string htmlContent, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new CapturedEmail(recipientEmail, subject, htmlContent));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string recipientEmail, string resetToken, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new CapturedEmail(recipientEmail, "Password reset", $"<p>{resetToken}</p>"));
        return Task.CompletedTask;
    }

    public string LatestToken(string subjectContains)
    {
        var email = _messages.LastOrDefault(message => message.Subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No captured email with subject containing '{subjectContains}'.");
        var tokenMatch = Regex.Match(email.HtmlBody, "(?<token>[A-Za-z0-9_-]{40,})");
        return tokenMatch.Success
            ? tokenMatch.Groups["token"].Value
            : throw new InvalidOperationException("The captured email did not contain a token paragraph.");
    }

    public string LatestCode(string subjectContains)
    {
        var email = _messages.LastOrDefault(message => message.Subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No captured email with subject containing '{subjectContains}'.");
        var codeMatch = Regex.Match(
            email.HtmlBody,
            "aria-label=\"Your verification code\"[^>]*>\\s*(?<code>\\d{6})\\s*</div>",
            RegexOptions.CultureInvariant);
        return codeMatch.Success
            ? codeMatch.Groups["code"].Value
            : throw new InvalidOperationException("The captured email did not contain a six-digit verification code.");
    }
}
