
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Realtime.Hubs;
using Realtime.Health;
using Realtime.Messaging;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var configuredJwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(configuredJwtSecret) ||
    configuredJwtSecret.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase) ||
    Encoding.UTF8.GetByteCount(configuredJwtSecret) < 32)
{
    throw new InvalidOperationException("A strong Jwt:SecretKey must be configured before starting this service (at least 32 bytes, not the CHANGE_THIS placeholder). Set Jwt__SecretKey, for example with JWT_SECRET_KEY in .env and '. .\\scripts\\Load-Env.ps1'.");
}
var gameServiceBaseUrl = builder.Configuration["GameService:BaseUrl"];
if (!Uri.TryCreate(gameServiceBaseUrl, UriKind.Absolute, out var gameServiceUri))
{
    throw new InvalidOperationException("GameService:BaseUrl must be configured to the reachable Game API URL.");
}
builder.Services.AddHealthChecks()
    .AddCheck<KafkaReadinessHealthCheck>("kafka", tags: new[] { "ready" })
    .AddCheck<GameApiReadinessHealthCheck>("game-api", tags: new[] { "ready" })
    .AddCheck<RedisReadinessHealthCheck>("redis-backplane", tags: new[] { "ready" });

var redisBackplane = builder.Configuration["SignalR:Redis:ConnectionString"];
var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(redisBackplane))
{
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
        _ => StackExchange.Redis.ConnectionMultiplexer.Connect(redisBackplane));
    signalRBuilder.AddStackExchangeRedis(redisBackplane, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal(
            builder.Configuration["SignalR:Redis:ChannelPrefix"] ?? "ChessPlatform");
    });
}
else if (!builder.Environment.IsDevelopment() && builder.Configuration.GetValue("SignalR:Redis:RequiredForMultiInstance", true))
{
    throw new InvalidOperationException("Configure SignalR:Redis:ConnectionString for multi-instance Realtime deployments.");
}
signalRBuilder.AddHubOptions<GameHub>(options =>
    {
        options.AddFilter<HubExceptionFilter>();
    });

builder.Services.AddTransient<HubExceptionFilter>();
builder.Services.AddProblemDetails();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ChessPlatform.Identity",
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ChessPlatform",

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:SecretKey"]!))
            };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken =
                    context.Request.Query["access_token"];

                var path =
                    context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/game"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient("GameApi", client =>
{
    client.BaseAddress = gameServiceUri;
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<IUserIdProvider, ChessUserIdProvider>();
builder.Services.AddSingleton<GamePresenceTracker>(services =>
    new GamePresenceTracker(services.GetService<StackExchange.Redis.IConnectionMultiplexer>()));
builder.Services.AddHostedService<GamePresenceLeaseRefresher>();
builder.Services.AddSingleton<GameMatchBroadcaster>();
builder.Services.AddSingleton<GameEndBroadcaster>(services =>
    new GameEndBroadcaster(
        services.GetRequiredService<IHubContext<GameHub>>(),
        services.GetRequiredService<ILogger<GameEndBroadcaster>>(),
        services.GetService<StackExchange.Redis.IConnectionMultiplexer>()));
builder.Services.AddHostedService<GameFinishedEventConsumer>();

var app = builder.Build();

// Handles unexpected HTTP-pipeline exceptions.
// It does not handle SignalR hub invocation exceptions.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature =
            context.Features.Get<IExceptionHandlerPathFeature>();

        var logger =
            context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Realtime.UnhandledException");

        if (exceptionFeature?.Error is not null)
        {
            logger.LogError(
                exceptionFeature.Error,
                "Unhandled HTTP exception at {Path}. TraceId: {TraceId}",
                context.Request.Path,
                context.TraceIdentifier);
        }

        context.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected server error occurred.",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier
            }).ExecuteAsync(context);
    });
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<GameHub>("/hubs/game");

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.Run();

public partial class Program { }
