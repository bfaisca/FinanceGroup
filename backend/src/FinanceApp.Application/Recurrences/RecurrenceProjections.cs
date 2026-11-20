using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Recurrences;

/// <summary>Dados derivados de uma regra: ocorrências geradas e próxima ocorrência em aberto.</summary>
internal readonly record struct RecurrenceDerived(int GeneratedCount, DateOnly? NextOccurrenceDate);

internal static class RecurrenceProjections
{
    /// <summary>
    /// <c>generatedCount</c> inclui canceladas. <c>nextOccurrenceDate</c> é a primeira data do cronograma ≥ hoje que não foi
    /// paga/recebida nem cancelada (ainda não gerada ou gerada em aberto); <c>null</c> se a regra está inativa ou encerrada.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, RecurrenceDerived>> LoadDerivedAsync(
        this IAppDbContext db,
        IReadOnlyCollection<RecurrenceRule> rules,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, RecurrenceDerived>(rules.Count);
        if (rules.Count == 0)
        {
            return result;
        }

        var ids = rules.Select(r => r.Id).ToList();
        var counts = await db.Transactions
            .AsNoTracking()
            .Where(t => t.GroupId == null && t.RecurrenceRuleId != null && ids.Contains(t.RecurrenceRuleId.Value))
            .GroupBy(t => t.RecurrenceRuleId!.Value)
            .Select(g => new { RuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RuleId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        var closed = (await db.Transactions
                .AsNoTracking()
                .Where(t => t.GroupId == null && t.RecurrenceRuleId != null && ids.Contains(t.RecurrenceRuleId.Value)
                            && t.OccurrenceDate >= today
                            && (t.Status == TransactionStatus.Paid || t.Status == TransactionStatus.Received || t.Status == TransactionStatus.Canceled))
                .Select(t => new { RuleId = t.RecurrenceRuleId!.Value, Date = t.OccurrenceDate!.Value })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(x => x.RuleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Date).ToHashSet());

        foreach (var rule in rules)
        {
            IReadOnlySet<DateOnly> closedDates = closed.GetValueOrDefault(rule.Id) ?? [];
            result[rule.Id] = new RecurrenceDerived(counts.GetValueOrDefault(rule.Id), rule.GetNextOccurrence(today, closedDates));
        }

        return result;
    }
}
