using FinanceApp.Application.Auth;
using FinanceApp.Application.Emails;
using FinanceApp.Application.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(ServiceLifetime.Singleton);
        services.AddSingleton<PasswordResetEmailFactory>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        return services;
    }
}
