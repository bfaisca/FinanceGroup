using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceApp.Infrastructure.Persistence;

/// <summary>
/// Usado apenas pelo <c>dotnet ef</c>. Lê <c>ConnectionStrings__Default</c> do ambiente;
/// na ausência, usa o PostgreSQL local de desenvolvimento.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDevelopmentConnection =
        "Host=localhost;Port=5432;Database=groupfinance;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(string.IsNullOrWhiteSpace(connectionString) ? LocalDevelopmentConnection : connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
