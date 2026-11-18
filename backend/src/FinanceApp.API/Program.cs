using FinanceApp.API.Configuration;
using System.Globalization;
using FinanceApp.Application;
using FinanceApp.Infrastructure;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Host.UseSerilog((context, services, logger) =>
{
    logger.ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        // No .NET 8 este middleware registra como erro inclusive exceções tratadas (validação, 401...).
        // Erros inesperados são registrados pelo GlobalExceptionHandler.
        .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal);

    if (context.HostingEnvironment.IsDevelopment())
    {
        logger.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture);
    }
    else
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
});

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddApi();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString(FinanceApp.Infrastructure.DependencyInjection.ConnectionStringName)))
{
    throw new InvalidOperationException("Configure ConnectionStrings__Default.");
}

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
#pragma warning disable CA1848 // Log único na inicialização.
    app.Logger.LogInformation("Aplicando migrations do banco de dados...");
#pragma warning restore CA1848
    await db.Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseSerilogRequestLogging(o =>
{
    // Somente método, rota, status e duração: nunca corpo, query string ou cabeçalhos.
    o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} em {Elapsed:0.0} ms";
    o.GetLevel = (http, _, ex) =>
        ex is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
        : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Debug
        : LogEventLevel.Information;
});

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() });
    },
}).AllowAnonymous().DisableRateLimiting();

await app.RunAsync();

/// <summary>Exposto para <c>WebApplicationFactory&lt;Program&gt;</c> nos testes de integração.</summary>
public partial class Program
{
    protected Program()
    {
    }
}
