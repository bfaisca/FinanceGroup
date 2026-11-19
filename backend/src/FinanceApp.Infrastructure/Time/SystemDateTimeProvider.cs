using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FinanceApp.Infrastructure.Time;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    private readonly TimeZoneInfo timeZone;

    public SystemDateTimeProvider(IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        // A validação das opções (ValidateOnStart) já garante que o fuso existe; a falha aqui seria um bug de configuração.
        timeZone = AppOptions.TryResolveTimeZone(options.Value.TimeZone, out var resolved)
            ? resolved
            : throw new InvalidOperationException($"App:TimeZone '{options.Value.TimeZone}' é inválido.");
    }

    /// <summary>
    /// UTC truncado em milissegundos: o PostgreSQL armazena microssegundos, e truncar evita que o valor
    /// devolvido logo após a criação difira do valor lido depois do banco.
    /// </summary>
    public DateTimeOffset UtcNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
        }
    }

    /// <summary>"Hoje" no fuso configurado (D-021).</summary>
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
}
