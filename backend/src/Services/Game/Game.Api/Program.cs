using Game.Application;
using Game.Application.Exceptions;
using Game.Application.Games;
using Game.Domain.Chess;
using Game.Infrastructure;
using Game.Infrastructure.Chess;
using Game.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var configuredJwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(configuredJwtSecret) ||
    configuredJwtSecret.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase) ||
    Encoding.UTF8.GetByteCount(configuredJwtSecret) < 32)
{
    throw new InvalidOperationException("A strong Jwt:SecretKey must be configured before starting this service (at least 32 bytes, not the CHANGE_THIS placeholder). Set Jwt__SecretKey, for example with JWT_SECRET_KEY in .env and '. .\\scripts\\Load-Env.ps1'.");
}
var tournamentServiceKey = builder.Configuration["InternalServices:TournamentKey"];
if (string.IsNullOrWhiteSpace(tournamentServiceKey) || Encoding.UTF8.GetByteCount(tournamentServiceKey) < 32)
{
    throw new InvalidOperationException("InternalServices:TournamentKey must be a secret of at least 32 bytes (set INTERNAL_TOURNAMENT_KEY in .env and run scripts/Load-Env.ps1).");
}
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GameDbContext>("database", tags: ["ready"]);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddControllers();
builder.Services.AddHttpClient("IdentityApi", client =>
{
    var baseUrl = builder.Configuration["IdentityApi:BaseUrl"] ?? "http://localhost:5170/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IChessRules, PaxChessRules>();
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("GameDatabase")));

builder.Services
.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ChessPlatform.Identity",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ChessPlatform",

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                builder.Configuration["Jwt:SecretKey"]!))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();
// Apply pending EF Core migrations before accepting traffic. Ensure the database is backed up
// and migration permissions are configured for this service's database user.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<GameDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exception = context.Features
            .Get<IExceptionHandlerFeature>()
            ?.Error;

        if (exception is GameMoveConflictException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;

            await Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Move conflict",
                detail: "The game was updated by another request. Please refresh the game and try again.")
                .ExecuteAsync(context);

            return;
        }

        if (exception is GameConcurrencyException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;

            await Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Game state conflict",
                detail: "The game was updated by another request. Please refresh the game and try again.")
                .ExecuteAsync(context);

            return;
        }
        if (exception is GameSeekConflictException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;

            await Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Active search already exists",
                detail: exception.Message)
                .ExecuteAsync(context);

            return;
        }

        if (exception is InvalidOperationException)
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Game.Api.Conflict")
                .LogWarning(exception, "Request conflicted with the current game state. TraceId: {TraceId}", context.TraceIdentifier);

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The requested operation conflicts with the current game state.",
                extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
                .ExecuteAsync(context);
            return;
        }

        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Game.Api.UnhandledException");
        logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", context.TraceIdentifier);

        if (exception is ArgumentException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            await Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request",
                detail: exception.Message)
                .ExecuteAsync(context);

            return;
        }
        if (exception is GameSeekNotFoundException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;

            await Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Active search not found",
                detail: exception.Message)
                .ExecuteAsync(context);

            return;
        }
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.")
            .ExecuteAsync(context);
    });
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Service-to-service calls (Tournament -> Game) use plain HTTP inside the trusted network and
// carry a secret header; redirecting them to HTTPS would drop or break the request.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/internal"),
    branch => branch.UseHttpsRedirection());

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.Run();

public partial class Program { }
