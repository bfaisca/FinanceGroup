using FinanceApp.Application.Accounts;
using FinanceApp.Application.Auth;
using FinanceApp.Application.Categories;
using FinanceApp.Application.CreditCards;
using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Emails;
using FinanceApp.Application.Installments;
using FinanceApp.Application.Recurrences;
using FinanceApp.Application.Reports;
using FinanceApp.Application.Transactions;
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
        services.AddScoped<BalanceProjectionService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<CreditCardSummaryService>();
        services.AddScoped<CreditCardService>();
        services.AddScoped<InstallmentPlanService>();
        services.AddScoped<IRecurrenceGenerator, RecurrenceGenerator>();
        services.AddScoped<RecurrenceService>();
        services.AddScoped<ReportService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
