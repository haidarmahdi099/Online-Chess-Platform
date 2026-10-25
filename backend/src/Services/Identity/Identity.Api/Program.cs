using Microsoft.EntityFrameworkCore;
using Identity.Application;
using Identity.Infrastructure;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("database", tags: ["ready"]);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);

    // Behind Docker/Kubernetes the gateway is not on loopback. List its address(es) in
    // ForwardedHeaders:TrustedProxies, otherwise every user appears to share the gateway's IP
    // and the per-IP login lockout would lock everybody out together.
    var trustedProxies = builder.Configuration
        .GetSection("ForwardedHeaders:TrustedProxies")
        .Get<string[]>() ?? Array.Empty<string>();

    foreach (var proxy in trustedProxies)
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

builder.Services.AddControllers();

builder.Services.AddApplication();
builder.Services.AddHostedService<Identity.Api.Admin.AdminRoleSeeder>();

builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment);

var jwtSection = builder.Configuration.GetSection("Jwt");

var secretKey = jwtSection["SecretKey"];
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];

if (string.IsNullOrWhiteSpace(secretKey) ||
    secretKey.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase) ||
    Encoding.UTF8.GetByteCount(secretKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:SecretKey must be configured before starting this service (at least 32 bytes, not the CHANGE_THIS placeholder). Set Jwt__SecretKey, for example with JWT_SECRET_KEY in .env and '. .\\scripts\\Load-Env.ps1'.");
}

if (string.IsNullOrWhiteSpace(issuer))
{
    throw new InvalidOperationException(
        "JWT Issuer is not configured.");
}

if (string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException(
        "JWT Audience is not configured.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secretKey)),

                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
// Apply pending EF Core migrations before accepting traffic. Ensure the database is backed up
// and migration permissions are configured for this service's database user.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Identity.Api.UnhandledException");
    if (feature?.Error is not null)
        logger.LogError(feature.Error, "Unhandled identity error. TraceId: {TraceId}", context.TraceIdentifier);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await Results.Problem(statusCode: 500, title: "An unexpected error occurred.",
        extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
}));

app.UseForwardedHeaders();

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
