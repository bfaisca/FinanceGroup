using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Reports;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using FinanceApp.Application.Common.Abstractions;

namespace FinanceApp.Application.Groups;

/// <summary>
/// Dashboard do grupo (D-034 emendada por D-046/D-048, contrato §16.3). Período por <c>transactionDate</c>, somente despesas NÃO
/// canceladas. Pago = principal + juros; pendente = original − principal pago (nunca conta como pago). Agregações feitas no banco.
/// </summary>
public sealed class GroupDashboardService(
    IAppDbContext db,
    GroupAccess access,
    IValidator<GroupDashboardQuery> validator)
{
    public async Task<GroupDashboardDto> GetAsync(Guid groupId, GroupDashboardQuery query, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);

        var from = query.From!.Value;
        var to = query.To!.Value;
        var inPeriod = Period(groupId, from, to);

        var expenses = await inPeriod.SumAsync(t => (decimal?)t.Amount, cancellationToken).ConfigureAwait(false) ?? 0m;
        var principalPaid = await inPeriod.SumAsync(t => (decimal?)t.PaidAmount, cancellationToken).ConfigureAwait(false) ?? 0m;
        var payers = await GetPayersAsync(inPeriod, cancellationToken).ConfigureAwait(false);
        var interest = payers.Sum(p => p.Interest);

        var userIds = payers.Select(a => a.UserId).ToList();
        var names = await access.LoadUserNamesAsync(userIds, cancellationToken).ConfigureAwait(false);
        var statuses = await db.GroupMembers
            .AsNoTracking()
            .Where(m => m.GroupId == groupId && userIds.Contains(m.UserId))
            .ToDictionaryAsync(m => m.UserId, m => m.Status, cancellationToken)
            .ConfigureAwait(false);

        var byCategory = await GetExpensesByCategoryAsync(inPeriod, expenses, cancellationToken).ConfigureAwait(false);
        var monthly = await GetMonthlyAsync(groupId, query, cancellationToken).ConfigureAwait(false);
        var previous = await GetPreviousPeriodAsync(groupId, from, to, expenses, cancellationToken).ConfigureAwait(false);

        return new GroupDashboardDto(
            new DashboardPeriodDto(from, to),
            GroupDashboardMath.Totals(expenses, principalPaid, interest),
            GroupDashboardMath.BuildPaidBy(payers, names, statuses),
            byCategory,
            monthly,
            previous);
    }

    private IQueryable<Transaction> NonCanceled(Guid groupId) =>
        db.Transactions.AsNoTracking().Where(t => t.GroupId == groupId && t.Status != TransactionStatus.Canceled);

    private IQueryable<Transaction> Period(Guid groupId, DateOnly from, DateOnly to) =>
        NonCanceled(groupId).Where(t => t.TransactionDate >= from && t.TransactionDate <= to);

    /// <summary>Por quem PAGOU: principal e juros somados por pagador, das despesas do período (pendente não conta).</summary>
    private async Task<List<PayerAmount>> GetPayersAsync(IQueryable<Transaction> inPeriod, CancellationToken cancellationToken) =>
        await (
                from p in db.TransactionPayments.AsNoTracking()
                join t in inPeriod on p.TransactionId equals t.Id
                group p by p.CreatedByUserId into g
                select new PayerAmount(g.Key, g.Sum(p => p.Amount), g.Sum(p => p.InterestAmount)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Despesas pela categoria RAIZ, com o detalhamento das subcategorias (percentual da subcategoria sobre a raiz).</summary>
    private async Task<IReadOnlyList<GroupCategoryShareDto>> GetExpensesByCategoryAsync(
        IQueryable<Transaction> inPeriod,
        decimal totalExpense,
        CancellationToken cancellationToken)
    {
        var groups = await inPeriod
            .GroupBy(t => new { RootId = t.Category!.ParentCategoryId ?? t.CategoryId, t.CategoryId })
            .Select(g => new { g.Key.RootId, g.Key.CategoryId, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (groups.Count == 0)
        {
            return [];
        }

        var ids = groups.Select(g => g.RootId).Concat(groups.Select(g => g.CategoryId)).Distinct().ToList();
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Color })
            .ToDictionaryAsync(c => c.Id, cancellationToken)
            .ConfigureAwait(false);

        return groups
            .GroupBy(g => g.RootId)
            .Select(root =>
            {
                var rootTotal = root.Sum(g => g.Total);
                var children = root
                    .Where(g => g.CategoryId != root.Key)
                    .Select(g => new GroupCategoryChildDto(
                        g.CategoryId, categories[g.CategoryId].Name, g.Total, ReportMath.Percentage(g.Total, rootTotal)))
                    .OrderByDescending(c => c.Amount)
                    .ThenBy(c => c.Name, StringComparer.CurrentCulture)
                    .ToList();
                return new GroupCategoryShareDto(
                    root.Key, categories[root.Key].Name, categories[root.Key].Color, rootTotal, ReportMath.Percentage(rootTotal, totalExpense), children);
            })
            .OrderByDescending(c => c.Amount)
            .ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary><c>months</c> meses terminando em <c>endMonth</c> (padrão: mês de <c>to</c>); meses sem dados vêm zerados.</summary>
    private async Task<IReadOnlyList<GroupMonthlyDto>> GetMonthlyAsync(Guid groupId, GroupDashboardQuery query, CancellationToken cancellationToken)
    {
        var to = query.To!.Value;
        var lastMonth = query.EndMonth is not null && ReportMath.TryParseMonth(query.EndMonth, out var parsed)
            ? parsed
            : new DateOnly(to.Year, to.Month, 1);
        var firstMonth = lastMonth.AddMonths(-(query.Months - 1));
        var endExclusive = lastMonth.AddMonths(1);

        var inRange = NonCanceled(groupId).Where(t => t.TransactionDate >= firstMonth && t.TransactionDate < endExclusive);
        var expenseRows = await inRange
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var paidRows = await (
                from p in db.TransactionPayments.AsNoTracking()
                join t in inRange on p.TransactionId equals t.Id
                group p by new { t.TransactionDate.Year, t.TransactionDate.Month } into g
                select new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount + p.InterestAmount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var months = new List<GroupMonthlyDto>(query.Months);
        for (var i = 0; i < query.Months; i++)
        {
            var month = firstMonth.AddMonths(i);
            months.Add(new GroupMonthlyDto(
                ReportMath.MonthKey(month),
                expenseRows.Where(r => r.Year == month.Year && r.Month == month.Month).Sum(r => r.Total),
                paidRows.Where(r => r.Year == month.Year && r.Month == month.Month).Sum(r => r.Total)));
        }

        return months;
    }

    private async Task<GroupPreviousPeriodDto> GetPreviousPeriodAsync(
        Guid groupId,
        DateOnly from,
        DateOnly to,
        decimal currentExpenses,
        CancellationToken cancellationToken)
    {
        var (previousFrom, previousTo) = GroupDashboardMath.PreviousPeriod(from, to);
        var expenses = await Period(groupId, previousFrom, previousTo).SumAsync(t => (decimal?)t.Amount, cancellationToken).ConfigureAwait(false) ?? 0m;
        return new GroupPreviousPeriodDto(previousFrom, previousTo, expenses, GroupDashboardMath.ChangePercent(currentExpenses, expenses));
    }
}
