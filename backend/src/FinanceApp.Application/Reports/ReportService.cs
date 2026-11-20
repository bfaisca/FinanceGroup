using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Installments;
using FinanceApp.Application.Recurrences;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Reports;

/// <summary>
/// Relatórios individuais (contrato §14, D-028). Base: movimentações NÃO canceladas do usuário autenticado com
/// <c>transactionDate</c> no período (mesma base do dashboard). Agregação no banco; percentuais 0–100 com 2 casas.
/// </summary>
public sealed class ReportService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IValidator<SummaryQuery> summaryValidator,
    IValidator<TypedPeriodQuery> typedValidator,
    IValidator<MonthlyEvolutionQuery> evolutionValidator,
    IValidator<InstallmentsReportQuery> installmentsValidator)
{
    public const string NoAccountName = "Sem conta";

    private const int UpcomingRecurrenceMonths = 12;

    // ------------------------------------------------------------------ resumo

    public async Task<SummaryReportDto> GetSummaryAsync(SummaryQuery query, CancellationToken cancellationToken)
    {
        await summaryValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var from = query.From!.Value;
        var to = query.To!.Value;
        var byDay = query.GroupBy == ReportGroupBy.Day;

        var baseQuery = PeriodQuery(userId, from, to);
        var totals = await TransactionAggregates.GetTotalsAsync(baseQuery, cancellationToken).ConfigureAwait(false);

        var series = new List<SummarySeriesItemDto>();
        if (byDay)
        {
            var rows = await baseQuery
                .GroupBy(t => new { t.TransactionDate, t.Type })
                .Select(g => new { g.Key.TransactionDate, g.Key.Type, Total = g.Sum(t => t.Amount) })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var income = rows.Where(r => r.TransactionDate == day && r.Type == TransactionType.Income).Sum(r => r.Total);
                var expense = rows.Where(r => r.TransactionDate == day && r.Type == TransactionType.Expense).Sum(r => r.Total);
                series.Add(new SummarySeriesItemDto(ReportMath.DayKey(day), income, expense, income - expense));
            }
        }
        else
        {
            var rows = await MonthlyTotalsAsync(baseQuery, cancellationToken).ConfigureAwait(false);
            var last = new DateOnly(to.Year, to.Month, 1);
            for (var month = new DateOnly(from.Year, from.Month, 1); month <= last; month = month.AddMonths(1))
            {
                var (income, expense) = SumMonth(rows, month);
                series.Add(new SummarySeriesItemDto(ReportMath.MonthKey(month), income, expense, income - expense));
            }
        }

        return new SummaryReportDto(new DashboardPeriodDto(from, to), totals, series);
    }

    // ------------------------------------------------------------------ por categoria

    public async Task<CategoryReportDto> GetByCategoryAsync(TypedPeriodQuery query, CancellationToken cancellationToken)
    {
        await typedValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var type = ReportDefaults.TypeOrDefault(query.Type);

        var groups = await PeriodQuery(userId, query.From!.Value, query.To!.Value)
            .Where(t => t.Type == type)
            .GroupBy(t => new { RootId = t.Category!.ParentCategoryId ?? t.CategoryId, t.CategoryId })
            .Select(g => new { g.Key.RootId, g.Key.CategoryId, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = groups.Sum(g => g.Total);
        if (groups.Count == 0)
        {
            return new CategoryReportDto(new DashboardPeriodDto(query.From.Value, query.To.Value), type, 0m, []);
        }

        var ids = groups.Select(g => g.RootId).Concat(groups.Select(g => g.CategoryId)).Distinct().ToList();
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Color })
            .ToDictionaryAsync(c => c.Id, cancellationToken)
            .ConfigureAwait(false);

        var items = groups
            .GroupBy(g => g.RootId)
            .Select(root =>
            {
                var rootTotal = root.Sum(g => g.Total);
                var subs = root
                    .Where(g => g.CategoryId != root.Key)
                    .Select(g => new SubcategoryShareDto(
                        g.CategoryId, categories[g.CategoryId].Name, g.Total, ReportMath.Percentage(g.Total, rootTotal)))
                    .OrderByDescending(s => s.Total)
                    .ThenBy(s => s.CategoryName, StringComparer.CurrentCulture)
                    .ToList();
                return new CategoryShareDto(
                    root.Key, categories[root.Key].Name, categories[root.Key].Color, rootTotal, ReportMath.Percentage(rootTotal, total), subs);
            })
            .OrderByDescending(i => i.Total)
            .ThenBy(i => i.CategoryName, StringComparer.CurrentCulture)
            .ToList();

        return new CategoryReportDto(new DashboardPeriodDto(query.From.Value, query.To.Value), type, total, items);
    }

    // ------------------------------------------------------------------ por conta

    /// <summary>Conta efetiva (D-025): a da movimentação ou a do cartão; sem nenhuma → "Sem conta".</summary>
    public async Task<AccountReportDto> GetByAccountAsync(TypedPeriodQuery query, CancellationToken cancellationToken)
    {
        await typedValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var type = ReportDefaults.TypeOrDefault(query.Type);

        var groups = await PeriodQuery(userId, query.From!.Value, query.To!.Value)
            .Where(t => t.Type == type)
            .GroupBy(t => t.AccountId ?? (t.CreditCard == null ? null : t.CreditCard.AccountId))
            .Select(g => new { AccountId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = groups.Sum(g => g.Total);
        var ids = groups.Where(g => g.AccountId is not null).Select(g => g.AccountId!.Value).ToList();
        var names = ids.Count == 0
            ? []
            : await db.Accounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken).ConfigureAwait(false);

        var items = groups
            .Select(g => new AccountShareDto(
                g.AccountId,
                g.AccountId is { } id ? names[id] : NoAccountName,
                g.Total,
                ReportMath.Percentage(g.Total, total)))
            .OrderByDescending(i => i.Total)
            .ThenBy(i => i.AccountName, StringComparer.CurrentCulture)
            .ToList();

        return new AccountReportDto(new DashboardPeriodDto(query.From.Value, query.To.Value), type, total, items);
    }

    // ------------------------------------------------------------------ por forma de pagamento

    public async Task<PaymentMethodReportDto> GetByPaymentMethodAsync(TypedPeriodQuery query, CancellationToken cancellationToken)
    {
        await typedValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var type = ReportDefaults.TypeOrDefault(query.Type);

        var groups = await PeriodQuery(userId, query.From!.Value, query.To!.Value)
            .Where(t => t.Type == type)
            .GroupBy(t => t.PaymentMethod)
            .Select(g => new { Method = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = groups.Sum(g => g.Total);
        var items = groups
            .Select(g => new PaymentMethodShareDto(g.Method, g.Total, ReportMath.Percentage(g.Total, total)))
            .OrderByDescending(i => i.Total)
            .ThenBy(i => i.PaymentMethod?.ToString(), StringComparer.Ordinal)
            .ToList();

        return new PaymentMethodReportDto(new DashboardPeriodDto(query.From.Value, query.To.Value), type, total, items);
    }

    // ------------------------------------------------------------------ evolução mensal

    public async Task<MonthlyEvolutionReportDto> GetMonthlyEvolutionAsync(MonthlyEvolutionQuery query, CancellationToken cancellationToken)
    {
        await evolutionValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();

        var endMonth = ReportMath.TryParseMonth(query.EndMonth, out var parsed)
            ? parsed
            : new DateOnly(clock.Today.Year, clock.Today.Month, 1);
        var startMonth = endMonth.AddMonths(-(query.Months - 1));
        var endExclusive = endMonth.AddMonths(1);

        var rows = await MonthlyTotalsAsync(
                NonCanceled(userId).Where(t => t.TransactionDate >= startMonth && t.TransactionDate < endExclusive), cancellationToken)
            .ConfigureAwait(false);

        var months = Enumerable.Range(0, query.Months).Select(i => startMonth.AddMonths(i)).ToList();
        var sums = months.Select(m => SumMonth(rows, m)).ToList();
        var cumulative = ReportMath.CumulativeSum(sums.Select(s => s.Income - s.Expense));

        var items = months
            .Select((m, i) => new MonthlyEvolutionItemDto(
                ReportMath.MonthKey(m), sums[i].Income, sums[i].Expense, sums[i].Income - sums[i].Expense, cumulative[i]))
            .ToList();
        return new MonthlyEvolutionReportDto(items);
    }

    // ------------------------------------------------------------------ parcelamentos

    /// <summary>
    /// Comprometimento futuro (D-028): restante das parcelas em aberto (não canceladas) pela data efetiva
    /// <c>dueDate ?? transactionDate</c>, nos <c>months</c> meses a partir do mês ATUAL; as atrasadas entram no mês corrente.
    /// </summary>
    public async Task<InstallmentsReportDto> GetInstallmentsAsync(InstallmentsReportQuery query, CancellationToken cancellationToken)
    {
        await installmentsValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var today = clock.Today;
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var windowEnd = currentMonth.AddMonths(query.Months);

        var rows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.CreatedByUserId == userId && t.InstallmentPlanId != null
                        && t.Status != TransactionStatus.Canceled && t.PaidAmount < t.Amount)
            .Select(t => new { Effective = t.DueDate ?? t.TransactionDate, Remaining = t.Amount - t.PaidAmount })
            .Where(x => x.Effective < windowEnd)
            .GroupBy(x => new { x.Effective.Year, x.Effective.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(x => x.Remaining), Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var commitment = new List<CommitmentMonthDto>(query.Months);
        for (var i = 0; i < query.Months; i++)
        {
            var month = currentMonth.AddMonths(i);
            var inMonth = rows.Where(r => r.Year == month.Year && r.Month == month.Month).ToList();
            var amount = inMonth.Sum(r => r.Amount);
            var count = inMonth.Sum(r => r.Count);
            if (i == 0)
            {
                // Parcelas em atraso (meses anteriores) pesam no mês corrente.
                var overdue = rows.Where(r => new DateOnly(r.Year, r.Month, 1) < currentMonth).ToList();
                amount += overdue.Sum(r => r.Amount);
                count += overdue.Sum(r => r.Count);
            }

            commitment.Add(new CommitmentMonthDto(ReportMath.MonthKey(month), amount, count));
        }

        var openPlans = (await db.InstallmentPlans.AsNoTracking()
                .Where(p => p.UserId == userId)
                .WhereStatus(db, InstallmentPlanStatus.Open)
                .ToRows(db)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .OrderBy(r => r.NextDueDate ?? DateOnly.MaxValue)
            .ThenBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToList();

        return new InstallmentsReportDto(
            openPlans.Count,
            openPlans.Sum(r => r.RemainingAmount),
            commitment,
            openPlans.Select(r => r.ToDto()).ToList());
    }

    // ------------------------------------------------------------------ recorrências

    /// <summary>Somente ocorrências JÁ GERADAS, não canceladas, com data &gt; hoje e ≤ hoje + 12 meses (D-028).</summary>
    public async Task<RecurrencesReportDto> GetRecurrencesAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = clock.Today;
        var limit = today.AddMonths(UpcomingRecurrenceMonths);

        var activeRules = await db.RecurrenceRules.AsNoTracking()
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderBy(r => r.Description)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var derived = await db.LoadDerivedAsync(activeRules, today, cancellationToken).ConfigureAwait(false);

        var upcoming = await db.Transactions.AsNoTracking()
            .Where(t => t.CreatedByUserId == userId && t.RecurrenceRuleId != null && t.Status != TransactionStatus.Canceled
                        && t.TransactionDate > today && t.TransactionDate <= limit)
            .GroupBy(t => new { RuleId = t.RecurrenceRuleId!.Value, t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
            .Select(g => new { g.Key.RuleId, g.Key.Year, g.Key.Month, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byMonth = upcoming
            .GroupBy(u => new { u.Year, u.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new UpcomingRecurrenceMonthDto(
                ReportMath.MonthKey(g.Key.Year, g.Key.Month),
                g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Total),
                g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Total)))
            .ToList();

        var rules = activeRules
            .Select(r => new RecurrenceReportRuleDto(
                r.Id,
                r.Description,
                r.Type,
                r.Amount,
                r.Frequency,
                r.Interval,
                derived[r.Id].NextOccurrenceDate,
                upcoming.Where(u => u.RuleId == r.Id).Sum(u => u.Total)))
            .ToList();

        return new RecurrencesReportDto(activeRules.Count, byMonth, rules);
    }

    // ------------------------------------------------------------------ auxiliares

    private IQueryable<Transaction> NonCanceled(Guid userId) =>
        db.Transactions.AsNoTracking().Where(t => t.CreatedByUserId == userId && t.Status != TransactionStatus.Canceled);

    private IQueryable<Transaction> PeriodQuery(Guid userId, DateOnly from, DateOnly to) =>
        NonCanceled(userId).Where(t => t.TransactionDate >= from && t.TransactionDate <= to);

    private sealed record MonthRow(int Year, int Month, TransactionType Type, decimal Total);

    private static async Task<List<MonthRow>> MonthlyTotalsAsync(IQueryable<Transaction> query, CancellationToken cancellationToken) =>
        await query
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
            .Select(g => new MonthRow(g.Key.Year, g.Key.Month, g.Key.Type, g.Sum(t => t.Amount)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static (decimal Income, decimal Expense) SumMonth(List<MonthRow> rows, DateOnly month) =>
        (rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Type == TransactionType.Income).Sum(r => r.Total),
         rows.Where(r => r.Year == month.Year && r.Month == month.Month && r.Type == TransactionType.Expense).Sum(r => r.Total));
}
