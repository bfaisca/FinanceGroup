using FinanceApp.Application.CreditCards;
using FinanceApp.Application.Transactions;

namespace FinanceApp.Application.Dashboard;

public sealed record DashboardPeriodDto(DateOnly From, DateOnly To);

/// <summary>Saldos das contas ativas com snapshot. <c>ProjectedTotal</c> é PROJETADO (D-019), não saldo bancário.</summary>
public sealed record DashboardBalanceDto(
    decimal? ReportedTotal,
    decimal? ProjectedTotal,
    DateOnly ProjectedAsOf,
    int AccountsWithBalance,
    int AccountsWithoutBalance);

public sealed record DashboardTotalsDto(
    decimal Income,
    decimal Expense,
    decimal Result,
    decimal ReceivedIncome,
    decimal PendingIncome,
    decimal PaidExpense,
    decimal PendingExpense);

public sealed record ExpenseByCategoryDto(Guid CategoryId, string CategoryName, string? Color, decimal Total, decimal Percentage);

public sealed record MonthlyHistoryDto(string Month, decimal Income, decimal Expense, decimal Result);

public sealed record DashboardDto(
    DashboardPeriodDto Period,
    DashboardBalanceDto Balance,
    DashboardTotalsDto Totals,
    IReadOnlyList<ExpenseByCategoryDto> ExpensesByCategory,
    IReadOnlyList<TransactionDto> UpcomingDues,
    IReadOnlyList<MonthlyHistoryDto> MonthlyHistory,
    DateOnly Today,
    IReadOnlyList<DashboardCreditCardDto> CreditCards);

/// <summary>Resumo de cartão ativo no dashboard (contrato §15).</summary>
public sealed record DashboardCreditCardDto(
    Guid Id,
    string Name,
    decimal CreditLimit,
    decimal UsedLimit,
    decimal AvailableLimit,
    OpenInvoiceDto OpenInvoice);

/// <summary>Parâmetros de <c>GET /dashboard</c>: <c>from</c> e <c>to</c> obrigatórios.</summary>
public sealed class DashboardQuery
{
    public const int MaxDays = 366;

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}
