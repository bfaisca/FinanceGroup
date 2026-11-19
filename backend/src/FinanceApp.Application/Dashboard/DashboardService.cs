using System.Globalization;
using FinanceApp.Application.Accounts;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Dashboard;

/// <summary>
/// Dashboard individual (contrato §9). Todas as agregações são feitas no banco; somente movimentações
/// do usuário autenticado e não canceladas entram nos totais.
/// </summary>
public sealed class DashboardService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    BalanceProjectionService projection,
    IValidator<DashboardQuery> validator)
{
    public const int UpcomingDuesLimit = 10;
    public const int UpcomingDuesWindowDays = 30;
    public const int HistoryMonths = 6;

    public async Task<DashboardDto> GetAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var from = query.From!.Value;
        var to = query.To!.Value;
        var today = clock.Today;

        var balance = await GetBalanceAsync(userId, to, cancellationToken).ConfigureAwait(false);
        var totals = await GetTotalsAsync(userId, from, to, cancellationToken).ConfigureAwait(false);
        var byCategory = await GetExpensesByCategoryAsync(userId, from, to, totals.Expense, cancellationToken).ConfigureAwait(false);
        var upcoming = await GetUpcomingDuesAsync(userId, today, cancellationToken).ConfigureAwait(false);
        var history = await GetMonthlyHistoryAsync(userId, to, cancellationToken).ConfigureAwait(false);

        return new DashboardDto(new DashboardPeriodDto(from, to), balance, totals, byCategory, upcoming, history, today);
    }

    private IQueryable<Transaction> ActiveTransactions(Guid userId) =>
        db.Transactions.AsNoTracking().Where(t => t.CreatedByUserId == userId && t.Status != TransactionStatus.Canceled);

    private async Task<DashboardBalanceDto> GetBalanceAsync(Guid userId, DateOnly asOf, CancellationToken cancellationToken)
    {
        var accountIds = await db.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.IsActive)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var balances = await projection.GetAsync(accountIds, asOf, cancellationToken).ConfigureAwait(false);
        var withBalance = balances.Values.Where(b => b.Reference is not null).ToList();

        return new DashboardBalanceDto(
            withBalance.Count == 0 ? null : withBalance.Sum(b => b.Reference!.Balance),
            withBalance.Count == 0 ? null : withBalance.Sum(b => b.ProjectedBalance!.Value),
            asOf,
            withBalance.Count,
            accountIds.Count - withBalance.Count);
    }

    private async Task<DashboardTotalsDto> GetTotalsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await ActiveTransactions(userId)
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Amount = g.Sum(t => t.Amount), Paid = g.Sum(t => t.PaidAmount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var income = rows.FirstOrDefault(r => r.Type == TransactionType.Income);
        var expense = rows.FirstOrDefault(r => r.Type == TransactionType.Expense);
        var incomeTotal = income?.Amount ?? 0m;
        var expenseTotal = expense?.Amount ?? 0m;

        return new DashboardTotalsDto(
            incomeTotal,
            expenseTotal,
            incomeTotal - expenseTotal,
            income?.Paid ?? 0m,
            incomeTotal - (income?.Paid ?? 0m),
            expense?.Paid ?? 0m,
            expenseTotal - (expense?.Paid ?? 0m));
    }

    /// <summary>Despesas agrupadas pela categoria RAIZ (subcategorias somadas ao pai), em ordem decrescente.</summary>
    private async Task<IReadOnlyList<ExpenseByCategoryDto>> GetExpensesByCategoryAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        decimal totalExpense,
        CancellationToken cancellationToken)
    {
        var groups = await ActiveTransactions(userId)
            .Where(t => t.Type == TransactionType.Expense && t.TransactionDate >= from && t.TransactionDate <= to)
            .GroupBy(t => t.Category!.ParentCategoryId ?? t.CategoryId)
            .Select(g => new { RootId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (groups.Count == 0)
        {
            return [];
        }

        var ids = groups.Select(g => g.RootId).ToList();
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Color })
            .ToDictionaryAsync(c => c.Id, cancellationToken)
            .ConfigureAwait(false);

        return groups
            .Select(g => new ExpenseByCategoryDto(
                g.RootId,
                categories[g.RootId].Name,
                categories[g.RootId].Color,
                g.Total,
                totalExpense == 0 ? 0m : Math.Round(g.Total / totalExpense * 100m, 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.CategoryName, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>Até 10 itens com restante &gt; 0, não cancelados, vencendo em até 30 dias (inclui atrasados).</summary>
    private async Task<IReadOnlyList<TransactionDto>> GetUpcomingDuesAsync(Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        var limit = today.AddDays(UpcomingDuesWindowDays);
        var rows = await ActiveTransactions(userId)
            .Where(t => t.PaidAmount < t.Amount && t.DueDate != null && t.DueDate <= limit)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Take(UpcomingDuesLimit)
            .ToRows()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(r => r.ToDto(today)).ToList();
    }

    /// <summary>Seis meses terminando no mês de <paramref name="to"/>; meses sem dados vêm zerados.</summary>
    private async Task<IReadOnlyList<MonthlyHistoryDto>> GetMonthlyHistoryAsync(Guid userId, DateOnly to, CancellationToken cancellationToken)
    {
        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        var firstMonth = lastMonth.AddMonths(-(HistoryMonths - 1));
        var endExclusive = lastMonth.AddMonths(1);

        var rows = await ActiveTransactions(userId)
            .Where(t => t.TransactionDate >= firstMonth && t.TransactionDate < endExclusive)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var months = new List<MonthlyHistoryDto>(HistoryMonths);
        for (var i = 0; i < HistoryMonths; i++)
        {
            var month = firstMonth.AddMonths(i);
            var income = rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Type == TransactionType.Income).Sum(r => r.Total);
            var expense = rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Type == TransactionType.Expense).Sum(r => r.Total);
            months.Add(new MonthlyHistoryDto(month.ToString("yyyy-MM", CultureInfo.InvariantCulture), income, expense, income - expense));
        }

        return months;
    }
}
