using Analysis.Application.Interfaces;
using Analysis.Infrastructure.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Analysis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalysisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StockfishOptions>(
            configuration.GetSection(StockfishOptions.SectionName));

        // Singleton: it owns the semaphore that limits concurrent engine processes.
        services.AddSingleton<IChessEngine, StockfishEngine>();

        return services;
    }
}
