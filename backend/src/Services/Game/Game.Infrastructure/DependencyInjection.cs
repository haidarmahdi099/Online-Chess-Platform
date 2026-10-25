using Game.Application.Contracts;
using Game.Application.Games;
using Game.Infrastructure.Messaging;
using Game.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Game.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IGameSeekRepository, GameSeekRepository>();
        services.AddScoped<IGameRepository, GameRepository>();

        services.AddSingleton<GameFinishedEventPublisher>();
        services.AddHostedService<OutboxPublisherWorker>();
        services.AddHostedService<GameClockWatcher>();

        return services;
    }
}
