using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tournament.Application.Interfaces;
using Tournament.Infrastructure.Messaging;
using Tournament.Infrastructure.Persistence;
using Tournament.Infrastructure.Provisioning;
using Tournament.Infrastructure.Repositories;
using Tournament.Infrastructure.Workers;

namespace Tournament.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTournamentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TournamentDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("TournamentDatabase")));

        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddSingleton<ITournamentEventPublisher, TournamentEventPublisher>();

        services.AddHttpClient(
            HttpGameProvisioner.HttpClientName,
            client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IGameProvisioner, HttpGameProvisioner>();

        // Set Tournament:BackgroundWorkersEnabled=false to run an instance that only serves
        // HTTP (for example when several replicas run and only one should drive tournaments).
        var workersEnabled = !bool.TryParse(configuration["Tournament:BackgroundWorkersEnabled"], out var flag) || flag;
        if (workersEnabled)
        {
            services.AddHostedService<GameFinishedConsumer>();
            services.AddHostedService<TournamentLifecycleWorker>();
            services.AddHostedService<TournamentOutboxDispatcher>();
        }

        return services;
    }
}
