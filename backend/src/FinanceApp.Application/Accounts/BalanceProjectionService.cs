using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Accounts;

/// <summary>Saldo informado e projetado de uma conta numa data-alvo.</summary>
public sealed record AccountBalanceInfo(SnapshotReference? Reference, decimal? ProjectedBalance);

/// <summary>
/// Calcula o saldo projetado (D-019) com agregação no banco. As contas recebidas já devem ter sido
/// filtradas pelo usuário autenticado pelo chamador.
/// </summary>
public sealed class BalanceProjectionService(IAppDbContext db)
{
    public async Task<IReadOnlyDictionary<Guid, AccountBalanceInfo>> GetAsync(
        IReadOnlyCollection<Guid> accountIds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, AccountBalanceInfo>(accountIds.Count);
        if (accountIds.Count == 0)
        {
            return result;
        }

        // Snapshot de referência: o que não possui outro mais novo (referenceDate, depois createdAt).
        var candidates = await db.AccountBalanceSnapshots
            .AsNoTracking()
            .Where(s => accountIds.Contains(s.AccountId))
            .Where(s => !db.AccountBalanceSnapshots.Any(o =>
                o.AccountId == s.AccountId
                && (o.ReferenceDate > s.ReferenceDate
                    || (o.ReferenceDate == s.ReferenceDate && o.CreatedAt > s.CreatedAt))))
            .Select(s => new { s.AccountId, s.Balance, s.ReferenceDate, s.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var references = candidates
            .GroupBy(c => c.AccountId)
            .ToDictionary(
                g => g.Key,
                g => ProjectedBalanceCalculator.SelectReference(g.Select(c => new SnapshotReference(c.Balance, c.ReferenceDate, c.CreatedAt)))!);

        foreach (var accountId in accountIds)
        {
            if (!references.TryGetValue(accountId, out var reference))
            {
                result[accountId] = new AccountBalanceInfo(null, null);
                continue;
            }

            decimal income = 0m, expense = 0m;
            if (asOf > reference.ReferenceDate)
            {
                var referenceDate = reference.ReferenceDate;
                var totals = await db.Transactions
                    .AsNoTracking()
                    // Conta efetiva (D-025): movimentações da conta + compras de cartões associados a ela.
                    // A data efetiva das compras no cartão é o vencimento da fatura (dueDate gravado).
                    .Where(t => (t.AccountId == accountId || (t.CreditCardId != null && t.CreditCard!.AccountId == accountId))
                                && t.Status != TransactionStatus.Canceled
                                && (t.DueDate ?? t.TransactionDate) > referenceDate
                                && (t.DueDate ?? t.TransactionDate) <= asOf)
                    .GroupBy(t => t.Type)
                    .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                income = totals.Where(t => t.Type == TransactionType.Income).Sum(t => t.Total);
                expense = totals.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Total);
            }

            result[accountId] = new AccountBalanceInfo(
                reference,
                ProjectedBalanceCalculator.Project(reference.Balance, reference.ReferenceDate, asOf, income, expense));
        }

        return result;
    }
}
