using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Puzzle.Application.Interfaces;
using Puzzle.Infrastructure.Persistence;
using Puzzle.Infrastructure.Messaging;
using Puzzle.Infrastructure.Repositories;

namespace Puzzle.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPuzzleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PuzzleDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("PuzzleDatabase")));

        services.AddScoped<IPuzzleRepository, PuzzleRepository>();

        services.AddHostedService<PuzzleOutboxPublisherWorker>();

        return services;
    }
}