using Microsoft.Extensions.DependencyInjection;
using Tournament.Application.Services;

namespace Tournament.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTournamentApplication(
        this IServiceCollection services)
    {
        services.AddScoped<ITournamentService, TournamentService>();

        return services;
    }
}
