using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Installments;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Reports;

/// <summary>Totais do período (mesma base e regras do dashboard, D-028).</summary>
public sealed record ReportTotalsDto(
    decimal Income,
    decimal Expense,
    decimal Result,
    decimal ReceivedIncome,
    decimal PendingIncome,
    decimal PaidExpense,
    decimal PendingExpense);

public sealed record SummarySeriesItemDto(string Key, decimal Income, decimal Expense, decimal Result);

public sealed record SummaryReportDto(DashboardPeriodDto Period, ReportTotalsDto Totals, IReadOnlyList<SummarySeriesItemDto> Series);

public sealed record SubcategoryShareDto(Guid CategoryId, string CategoryName, decimal Total, decimal Percentage);

public sealed record CategoryShareDto(
    Guid CategoryId,
    string CategoryName,
    string? Color,
    decimal Total,
    decimal Percentage,
    IReadOnlyList<SubcategoryShareDto> Subcategories);

public sealed record CategoryReportDto(DashboardPeriodDto Period, TransactionType Type, decimal Total, IReadOnlyList<CategoryShareDto> Items);

public sealed record AccountShareDto(Guid? AccountId, string AccountName, decimal Total, decimal Percentage);

public sealed record AccountReportDto(DashboardPeriodDto Period, TransactionType Type, decimal Total, IReadOnlyList<AccountShareDto> Items);

public sealed record PaymentMethodShareDto(PaymentMethod? PaymentMethod, decimal Total, decimal Percentage);

public sealed record PaymentMethodReportDto(
    DashboardPeriodDto Period,
    TransactionType Type,
    decimal Total,
    IReadOnlyList<PaymentMethodShareDto> Items);

public sealed record MonthlyEvolutionItemDto(string Month, decimal Income, decimal Expense, decimal Result, decimal CumulativeResult);

public sealed record MonthlyEvolutionReportDto(IReadOnlyList<MonthlyEvolutionItemDto> Items);

public sealed record CommitmentMonthDto(string Month, decimal Amount, int InstallmentsCount);

public sealed record InstallmentsReportDto(
    int OpenPlans,
    decimal TotalRemaining,
    IReadOnlyList<CommitmentMonthDto> CommitmentByMonth,
    IReadOnlyList<InstallmentPlanDto> Plans);

public sealed record UpcomingRecurrenceMonthDto(string Month, decimal Income, decimal Expense);

public sealed record RecurrenceReportRuleDto(
    Guid Id,
    string Description,
    TransactionType Type,
    decimal Amount,
    RecurrenceFrequency Frequency,
    int Interval,
    DateOnly? NextOccurrenceDate,
    decimal UpcomingTotal12m);

public sealed record RecurrencesReportDto(
    int ActiveRules,
    IReadOnlyList<UpcomingRecurrenceMonthDto> UpcomingByMonth,
    IReadOnlyList<RecurrenceReportRuleDto> Rules);

// ---------------------------------------------------------------- parâmetros

/// <summary><c>from</c>/<c>to</c> obrigatórios, <c>from ≤ to</c>, até 366 dias (contrato §14).</summary>
public class PeriodQuery
{
    public const int MaxDays = 366;

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}

public enum ReportGroupBy
{
    Day,
    Month,
}

public sealed class SummaryQuery : PeriodQuery
{
    /// <summary><c>day</c> ou <c>month</c> (padrão <c>month</c>).</summary>
    public ReportGroupBy? GroupBy { get; init; }
}

/// <summary>Relatórios por categoria/conta/forma de pagamento: <c>type</c> padrão <c>Expense</c>.</summary>
public sealed class TypedPeriodQuery : PeriodQuery
{
    public TransactionType? Type { get; init; }
}

public sealed class MonthlyEvolutionQuery
{
    public const int DefaultMonths = 12;
    public const int MaxMonths = 36;

    public int Months { get; init; } = DefaultMonths;

    /// <summary><c>YYYY-MM</c> (padrão: mês atual).</summary>
    public string? EndMonth { get; init; }
}

public sealed class InstallmentsReportQuery
{
    public int Months { get; init; } = MonthlyEvolutionQuery.DefaultMonths;
}
