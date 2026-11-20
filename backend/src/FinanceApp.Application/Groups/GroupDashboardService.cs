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
/// Dashboard do grupo (D-034, contrato §16.3). Período por <c>transactionDate</c>, somente movimentações NÃO canceladas.
/// "Quem cadastrou" (soma dos valores) e "quem pagou" (soma dos pagamentos) são sempre calculados separadamente;
/// pendente nunca conta como pago. Todas as agregações são feitas no banco.
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

        var totals = await TransactionAggregates.GetTotalsAsync(inPeriod, cancellationToken).ConfigureAwait(false);
        var created = await GetCreatedByAsync(inPeriod, cancellationToken).ConfigureAwait(false);
        var paid = await GetPaidByAsync(inPeriod, cancellationToken).ConfigureAwait(false);

        var userIds = created.Select(a => a.UserId).Concat(paid.Select(a => a.UserId)).Distinct().ToList();
        var names = await access.LoadUserNamesAsync(userIds, cancellationToken).ConfigureAwait(false);
        var statuses = await db.GroupMembers
            .AsNoTracking()
            .Where(m => m.GroupId == groupId && userIds.Contains(m.UserId))
            .ToDictionaryAsync(m => m.UserId, m => m.Status, cancellationToken)
            .ConfigureAwait(false);

        var byCategory = await GetExpensesByCategoryAsync(inPeriod, totals.Expense, cancellationToken).ConfigureAwait(false);
        var monthly = await GetMonthlyAsync(groupId, query, cancellationToken).ConfigureAwait(false);
        var previous = await GetPreviousPeriodAsync(groupId, from, to, totals.Expense, totals.Income, cancellationToken).ConfigureAwait(false);

        return new GroupDashboardDto(
            new DashboardPeriodDto(from, to),
            new GroupDashboardTotalsDto(
                totals.Expense,
                totals.PaidExpense,
                totals.PendingExpense,
                totals.Income,
                totals.ReceivedIncome,
                totals.PendingIncome,
                totals.Income - totals.Expense),
            GroupDashboardMath.BuildParticipation(created, names, statuses),
            GroupDashboardMath.BuildParticipation(paid, names, statuses),
            byCategory,
            monthly,
            previous);
    }

    private IQueryable<Transaction> NonCanceled(Guid groupId) =>
        db.Transactions.AsNoTracking().Where(t => t.GroupId == groupId && t.Status != TransactionStatus.Canceled);

    private IQueryable<Transaction> Period(Guid groupId, DateOnly from, DateOnly to) =>
        NonCanceled(groupId).Where(t => t.TransactionDate >= from && t.TransactionDate <= to);

    /// <summary>Por quem CADASTROU: soma dos valores por <c>CreatedByUserId</c>.</summary>
    private static async Task<List<ParticipationAmount>> GetCreatedByAsync(IQueryable<Transaction> inPeriod, CancellationToken cancellationToken)
    {
        var rows = await inPeriod
            .GroupBy(t => new { t.CreatedByUserId, t.Type })
            .Select(g => new { g.Key.CreatedByUserId, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ToParticipation(rows.Select(r => (r.CreatedByUserId, r.Type, r.Total)));
    }

    /// <summary>Por quem PAGOU/RECEBEU: soma dos PAGAMENTOS por pagador, de movimentações do período (pendente não conta).</summary>
    private async Task<List<ParticipationAmount>> GetPaidByAsync(IQueryable<Transaction> inPeriod, CancellationToken cancellationToken)
    {
        var rows = await (
                from p in db.TransactionPayments.AsNoTracking()
                join t in inPeriod on p.TransactionId equals t.Id
                group p by new { p.CreatedByUserId, t.Type } into g
                select new { g.Key.CreatedByUserId, g.Key.Type, Total = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ToParticipation(rows.Select(r => (r.CreatedByUserId, r.Type, r.Total)));
    }

    private static List<ParticipationAmount> ToParticipation(IEnumerable<(Guid UserId, TransactionType Type, decimal Total)> rows) =>
        rows.GroupBy(r => r.UserId)
            .Select(g => new ParticipationAmount(
                g.Key,
                g.Where(r => r.Type == TransactionType.Expense).Sum(r => r.Total),
                g.Where(r => r.Type == TransactionType.Income).Sum(r => r.Total)))
            .ToList();

    /// <summary>Despesas pela categoria RAIZ, com o detalhamento das subcategorias (percentual da subcategoria sobre a raiz).</summary>
    private async Task<IReadOnlyList<GroupCategoryShareDto>> GetExpensesByCategoryAsync(
        IQueryable<Transaction> inPeriod,
        decimal totalExpense,
        CancellationToken cancellationToken)
    {
        var groups = await inPeriod
            .Where(t => t.Type == TransactionType.Expense)
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

        var rows = await NonCanceled(groupId)
            .Where(t => t.TransactionDate >= firstMonth && t.TransactionDate < endExclusive)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var months = new List<GroupMonthlyDto>(query.Months);
        for (var i = 0; i < query.Months; i++)
        {
            var month = firstMonth.AddMonths(i);
            months.Add(new GroupMonthlyDto(
                ReportMath.MonthKey(month),
                rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Type == TransactionType.Income).Sum(r => r.Total),
                rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Type == TransactionType.Expense).Sum(r => r.Total)));
        }

        return months;
    }

    private async Task<GroupPreviousPeriodDto> GetPreviousPeriodAsync(
        Guid groupId,
        DateOnly from,
        DateOnly to,
        decimal currentExpenses,
        decimal currentIncome,
        CancellationToken cancellationToken)
    {
        var (previousFrom, previousTo) = GroupDashboardMath.PreviousPeriod(from, to);
        var previous = await Period(groupId, previousFrom, previousTo)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var expenses = previous.Where(p => p.Type == TransactionType.Expense).Sum(p => p.Total);
        var income = previous.Where(p => p.Type == TransactionType.Income).Sum(p => p.Total);

        return new GroupPreviousPeriodDto(
            previousFrom,
            previousTo,
            expenses,
            income,
            GroupDashboardMath.ChangePercent(currentExpenses, expenses),
            GroupDashboardMath.ChangePercent(currentIncome, income));
    }
}
