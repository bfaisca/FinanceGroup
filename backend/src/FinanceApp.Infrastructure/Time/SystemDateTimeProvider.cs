using FinanceApp.Application.Common.Abstractions;

namespace FinanceApp.Infrastructure.Time;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
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
}
