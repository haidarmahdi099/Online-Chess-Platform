using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Tournament.Application;
using Tournament.Infrastructure;
using Tournament.Infrastructure.Persistence;
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
var gameServiceBaseUrl = builder.Configuration["GameService:BaseUrl"];
if (!builder.Environment.IsDevelopment() &&
    (!Uri.TryCreate(gameServiceBaseUrl, UriKind.Absolute, out var gameServiceUri) ||
     gameServiceUri.IsLoopback))
{
    throw new InvalidOperationException("GameService:BaseUrl must be configured to the reachable Game API URL in non-development environments.");
}
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TournamentDbContext>("database", tags: ["ready"]);
builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddTournamentApplication();
builder.Services.AddTournamentInfrastructure(builder.Configuration);

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
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,

            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ChessPlatform.Identity",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ChessPlatform",

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:SecretKey"]
                    ?? throw new InvalidOperationException(
                        "JWT secret is not configured.")))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
// Apply pending EF Core migrations before accepting traffic. Ensure the database is backed up
// and migration permissions are configured for this service's database user.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<TournamentDbContext>();
    await db.Database.MigrateAsync();
}
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
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
