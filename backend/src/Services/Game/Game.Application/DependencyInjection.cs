using Game.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IGameSeekService, GameSeekService>();

        return services;
    }
}