using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Infrastructure.Email;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Security;
using FinanceApp.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceApp.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddValidatedOptions<JwtOptions>(JwtOptions.SectionName, o => o.Validate());
        services.AddValidatedOptions<AppOptions>(AppOptions.SectionName, o => o.Validate());
        services.AddValidatedOptions<SmtpOptions>(SmtpOptions.SectionName, o => o.Validate());

        // Lido de forma tardia (no resolve) para respeitar overrides de configuração (ex.: testes).
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"A connection string '{ConnectionStringName}' não foi configurada (ConnectionStrings__{ConnectionStringName}).");
            }

            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Singleton: o fuso (App__TimeZone) é resolvido uma vez e validado na inicialização.
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<Application.Common.Abstractions.IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ISecureTokenService, SecureTokenService>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }

    private static void AddValidatedOptions<T>(this IServiceCollection services, string section, Func<T, IEnumerable<string>> validate)
        where T : class
    {
        services.AddOptions<T>()
            .BindConfiguration(section)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<T>>(new DelegateOptionsValidator<T>(validate));
    }

    private sealed class DelegateOptionsValidator<T>(Func<T, IEnumerable<string>> validate) : IValidateOptions<T>
        where T : class
    {
        public ValidateOptionsResult Validate(string? name, T options)
        {
            var failures = validate(options).ToList();
            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
