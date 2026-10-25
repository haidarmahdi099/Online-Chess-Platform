using Identity.Application.Auth.ForgotPassword;
using Identity.Application.Auth.Login;
using Identity.Application.Auth.Logout;
using Identity.Application.Auth.Refresh;
using Identity.Application.Auth.Register;
using Identity.Application.Auth.ResetPassword;
using Identity.Application.Users.GetCurrentUser;
using Identity.Application.Users.GetUserByUsername;
using Identity.Application.Users.LookupUsers;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<GetUserByUsernameHandler>();
        services.AddScoped<LookupUsersHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();
        return services;
    }
}