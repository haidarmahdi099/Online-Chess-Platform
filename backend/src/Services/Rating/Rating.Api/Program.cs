using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Rating.Api.Health;
using Rating.Application.Interfaces;
using Rating.Application.Services;
using Rating.Infrastructure.Messaging;
using Rating.Infrastructure.Persistence;
using Rating.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RatingDbContext>("database", tags: ["ready"])
    .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);

// Add services to the container.
builder.Services.AddDbContext<RatingDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("RatingDatabase")));
builder.Services.AddScoped<IRatingRepository, RatingRepository>();
builder.Services.AddScoped<IRatingCalculator, EloRatingCalculator>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddHostedService<RatingEventConsumer>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
// Apply pending EF Core migrations before accepting traffic. Ensure the database is backed up
// and migration permissions are configured for this service's database user.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<RatingDbContext>();
    await db.Database.MigrateAsync();
}

// Unhandled failures become a 500 ProblemDetails (no stack trace outside Development),
// like the other services.
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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
