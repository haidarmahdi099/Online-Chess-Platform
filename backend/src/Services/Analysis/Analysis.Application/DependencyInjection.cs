using Analysis.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Analysis.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalysisApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IAnalysisService, AnalysisService>();

        return services;
    }
}
