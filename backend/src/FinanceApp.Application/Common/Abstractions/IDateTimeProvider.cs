namespace FinanceApp.Application.Common.Abstractions;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// "Hoje" no fuso da aplicação (<c>App__TimeZone</c>, D-021). Base de Overdue, validações de data
    /// futura, padrões de período e saldo projetado — nunca o UTC puro.
    /// </summary>
    DateOnly Today { get; }
}
