using Microsoft.Extensions.DependencyInjection;
using Puzzle.Application.Services;

namespace Puzzle.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPuzzleApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IPuzzleService, PuzzleService>();


        return services;

    }
}