using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Recurrences;

/// <summary>
/// Gerador de ocorrências (D-010/D-027). Idempotente: ignora datas que já tenham ocorrência (inclusive canceladas) e,
/// em corrida entre execuções, a unicidade (<c>recurrence_rule_id</c>, <c>occurrence_date</c>) impede duplicatas — a
/// violação é tratada como "já existe".
/// </summary>
public interface IRecurrenceGenerator
{
    /// <summary>Gera e persiste as ocorrências que faltam de UMA regra ativa até <c>today + horizonMonths</c>; devolve quantas criou.</summary>
    Task<int> GenerateAsync(Guid ruleId, DateOnly today, int horizonMonths, CancellationToken cancellationToken);

    /// <summary>Gera para todas as regras ativas de todos os usuários (serviço em segundo plano); devolve o total criado.</summary>
    Task<int> GenerateAllAsync(DateOnly today, int horizonMonths, CancellationToken cancellationToken);

    /// <summary>
    /// Monta (sem persistir) as ocorrências que faltam de uma regra, para que o chamador as grave junto de outras
    /// alterações na MESMA transação. <paramref name="ignoredExistingDates"/> são datas de ocorrências que o chamador
    /// está removendo e que devem ser regeneradas.
    /// </summary>
    Task<IReadOnlyList<Transaction>> PrepareAsync(
        RecurrenceRule rule,
        DateOnly today,
        int horizonMonths,
        IReadOnlySet<DateOnly>? ignoredExistingDates,
        CancellationToken cancellationToken);
}
