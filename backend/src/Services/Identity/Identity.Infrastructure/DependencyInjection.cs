using Identity.Application.Abstractions;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Email;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment = null)
    {
        var connectionString =
            configuration.GetConnectionString("IdentityDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "IdentityDatabase connection string is not configured.");
        }

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<
            IPasswordHasher,
            PasswordHasher>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
        services.AddScoped<IPasswordResetTokenService, PasswordResetTokenService>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.Configure<BrevoOptions>(configuration.GetSection("Brevo"));
        services.PostConfigure<BrevoOptions>(options =>
        {
            var frontendBaseUrl = configuration["Frontend:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
                options.FrontendBaseUrl = frontendBaseUrl.TrimEnd('/');
        });

        var environmentName = hostEnvironment?.EnvironmentName ??
            configuration["ASPNETCORE_ENVIRONMENT"] ??
            configuration["DOTNET_ENVIRONMENT"] ?? "Production";
        var isDevelopment = string.Equals(
            environmentName, "Development", StringComparison.OrdinalIgnoreCase);

        var brevoConfigured =
            !string.IsNullOrWhiteSpace(configuration["Brevo:ApiKey"]) &&
            !string.IsNullOrWhiteSpace(configuration["Brevo:SenderEmail"]);

        if (isDevelopment && !brevoConfigured)
        {
            // No (or incomplete) mail provider configured locally: log the reset link instead.
            services.AddSingleton<IEmailSender, DevelopmentLoggingEmailSender>();
        }
        else
        {
            if (!brevoConfigured)
            {
                throw new InvalidOperationException(
                    "Brevo:ApiKey and Brevo:SenderEmail must be configured (BREVO_API_KEY / BREVO_SENDER_EMAIL).");
            }

            services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
            {
                client.BaseAddress = new Uri("https://api.brevo.com/");
            });
        }

        return services;
    }
}
