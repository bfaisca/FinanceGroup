using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Recurrences;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceApp.Infrastructure.Background;

/// <summary>
/// Gera ocorrências de recorrências na inicialização e a cada <c>Recurrence__GenerationIntervalMinutes</c> (D-027).
/// Seguro com várias instâncias: a unicidade (<c>recurrence_rule_id</c>, <c>occurrence_date</c>) impede duplicatas e o
/// gerador trata a violação como "já existe". Cada execução usa um escopo de DI próprio; os logs não trazem dados sensíveis.
/// </summary>
public sealed partial class RecurrenceGenerationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RecurrenceOptions> options,
    ILogger<RecurrenceGenerationHostedService> logger) : BackgroundService
{
    private readonly ILogger<RecurrenceGenerationHostedService> logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.BackgroundEnabled)
        {
            LogDisabled();
            return;
        }

        var interval = TimeSpan.FromMinutes(settings.GenerationIntervalMinutes);
        LogStarted(settings.GenerationIntervalMinutes, settings.HorizonMonths);

        using var timer = new PeriodicTimer(interval);
        do
        {
            await RunOnceAsync(settings.HorizonMonths, stoppingToken).ConfigureAwait(false);
        }
        while (await WaitNextAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Uma execução isolada (escopo próprio). Falhas são registradas e nunca derrubam o serviço.</summary>
    internal async Task<int> RunOnceAsync(int horizonMonths, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var generator = scope.ServiceProvider.GetRequiredService<IRecurrenceGenerator>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            var created = await generator.GenerateAllAsync(clock.Today, horizonMonths, cancellationToken).ConfigureAwait(false);
            LogRun(created);

            // Manutenção periódica (D-044): retenção de notificações lidas. Falha aqui não invalida a geração acima.
            await PurgeNotificationsAsync(scope.ServiceProvider, clock, cancellationToken).ConfigureAwait(false);
            return created;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            return 0;
        }
    }

    private async Task PurgeNotificationsAsync(IServiceProvider services, IDateTimeProvider clock, CancellationToken cancellationToken)
    {
        try
        {
            var db = services.GetRequiredService<AppDbContext>();
            var removed = await NotificationRetention.PurgeReadAsync(db, clock.UtcNow, cancellationToken).ConfigureAwait(false);
            if (removed > 0)
            {
                LogNotificationsPurged(removed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A manutenção é oportunista: registra e tenta de novo na próxima execução.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogNotificationsPurgeFailure(ex);
        }
    }

    private static async Task<bool> WaitNextAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Geração de recorrências desativada (Recurrence:BackgroundEnabled=false).")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Geração de recorrências iniciada: a cada {IntervalMinutes} min, horizonte de {HorizonMonths} meses.")]
    private partial void LogStarted(int intervalMinutes, int horizonMonths);

    [LoggerMessage(Level = LogLevel.Information, Message = "Geração de recorrências concluída: {Created} ocorrências criadas.")]
    private partial void LogRun(int created);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retenção de notificações: {Removed} notificações lidas há mais de 90 dias removidas.")]
    private partial void LogNotificationsPurged(int removed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha na retenção de notificações; nova tentativa na próxima execução.")]
    private partial void LogNotificationsPurgeFailure(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha na geração de recorrências; nova tentativa na próxima execução.")]
    private partial void LogFailure(Exception exception);
}
