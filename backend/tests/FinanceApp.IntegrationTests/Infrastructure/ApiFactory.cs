using FinanceApp.Application.Common.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FinanceApp.IntegrationTests.Infrastructure;

/// <summary>
/// API real em memória contra um PostgreSQL REAL (D-015). Cada execução cria um banco com nome único,
/// aplica as migrations na inicialização e remove o banco ao final.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DefaultAdminConnection = "Host=localhost;Port=5432;Username=postgres;Password=postgres";
    public const string FrontendBaseUrl = "http://frontend.test";

    private readonly string adminConnectionString;
    private readonly string databaseName = $"groupfinance_test_{Guid.NewGuid():N}";

    public ApiFactory()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION");
        var admin = new NpgsqlConnectionStringBuilder(string.IsNullOrWhiteSpace(fromEnv) ? DefaultAdminConnection : fromEnv)
        {
            Database = "postgres",
            Pooling = false,
        };
        adminConnectionString = admin.ConnectionString;
        ConnectionString = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName, Pooling = true }.ConnectionString;
    }

    public string ConnectionString { get; }

    public FakeEmailSender Emails { get; } = new();

    /// <summary>Relógio de teste: tempo real por padrão; <see cref="TestClock.TodayOverride"/> fixa "hoje" (D-021).</summary>
    public TestClock Clock { get; } = new();

    /// <summary>Configurações extras aplicadas por cima das padrões de teste (ex.: rate limiting).</summary>
    public Dictionary<string, string?> ExtraSettings { get; } = [];

    public async Task InitializeAsync()
    {
        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        // Força a inicialização do host (aplica as migrations).
        _ = Services;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        // HTTPS para que o cookie Secure (fora de Development) seja aceito pelo cliente.
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = false,
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Jwt:Secret"] = "integration-tests-secret-with-more-than-32-chars",
                ["Jwt:Issuer"] = "GroupFinance",
                ["Jwt:Audience"] = "GroupFinance.Web",
                ["App:FrontendBaseUrl"] = FrontendBaseUrl,
                ["RateLimiting:Enabled"] = "false",
                ["Recurrence:BackgroundEnabled"] = "false",
                ["Serilog:MinimumLevel:Default"] = "Warning",
            };
            foreach (var (key, value) in ExtraSettings)
            {
                settings[key] = value;
            }

            config.AddInMemoryCollection(settings);
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(Clock);
        });
    }
}

/// <summary>Tempo real (UTC), com "hoje" opcionalmente fixado para testes determinísticos.</summary>
public sealed class TestClock : IDateTimeProvider
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Quando definido, substitui "hoje". Deve ser restaurado para <c>null</c> ao final do teste.</summary>
    public DateOnly? TodayOverride { get; set; }

    public DateTimeOffset UtcNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
        }
    }

    public DateOnly Today => TodayOverride ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, SaoPaulo).DateTime);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
