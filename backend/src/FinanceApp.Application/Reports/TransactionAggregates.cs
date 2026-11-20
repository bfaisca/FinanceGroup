using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Reports;

/// <summary>Agregações compartilhadas entre dashboard e relatórios (mesma base: movimentações não canceladas, D-028).</summary>
internal static class TransactionAggregates
{
    /// <summary>Totais e valores pagos/pendentes. Pagamentos parciais: pago soma <c>paidAmount</c>, pendente soma o restante.</summary>
    public static async Task<ReportTotalsDto> GetTotalsAsync(IQueryable<Transaction> nonCanceled, CancellationToken cancellationToken)
    {
        var rows = await nonCanceled
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Amount = g.Sum(t => t.Amount), Paid = g.Sum(t => t.PaidAmount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var income = rows.FirstOrDefault(r => r.Type == TransactionType.Income);
        var expense = rows.FirstOrDefault(r => r.Type == TransactionType.Expense);
        var incomeTotal = income?.Amount ?? 0m;
        var expenseTotal = expense?.Amount ?? 0m;

        return new ReportTotalsDto(
            incomeTotal,
            expenseTotal,
            incomeTotal - expenseTotal,
            income?.Paid ?? 0m,
            incomeTotal - (income?.Paid ?? 0m),
            expense?.Paid ?? 0m,
            expenseTotal - (expense?.Paid ?? 0m));
    }
}
