using FinanceApp.Application.Dashboard;
using FinanceApp.Domain.Groups;

namespace FinanceApp.Application.Groups;

/// <summary>Parâmetros de <c>GET /groups/{id}/dashboard</c> (contrato §16.3).</summary>
public sealed class GroupDashboardQuery
{
    public const int MaxDays = 366;
    public const int DefaultMonths = 6;
    public const int MaxMonths = 36;

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public int Months { get; init; } = DefaultMonths;

    /// <summary><c>YYYY-MM</c> (padrão: mês de <c>to</c>).</summary>
    public string? EndMonth { get; init; }
}

/// <summary>
/// Totais do período (despesas NÃO canceladas, D-046/D-048). <c>Expenses</c> = Σ valores originais; <c>ExpensesPaidPrincipal</c> =
/// Σ principal pago; <c>ExpensesPaid</c> = principal + juros (efetivamente pago); <c>ExpensesPending</c> = originais − principal
/// pago (só principal; nunca negativo).
/// </summary>
public sealed record GroupDashboardTotalsDto(
    decimal Expenses,
    decimal ExpensesPaidPrincipal,
    decimal InterestPaid,
    decimal ExpensesPaid,
    decimal ExpensesPending);

/// <summary>"Contas Pagas": quanto cada pessoa efetivamente pagou (principal + juros). Indicador estatístico, não é dívida.</summary>
public sealed record GroupPaidByItemDto(
    Guid UserId,
    string Name,
    GroupMemberStatus Status,
    decimal PaidAmount,
    decimal InterestAmount,
    decimal Percent);

public sealed record GroupCategoryChildDto(Guid CategoryId, string Name, decimal Amount, decimal Percent);

public sealed record GroupCategoryShareDto(
    Guid CategoryId,
    string Name,
    string? Color,
    decimal Amount,
    decimal Percent,
    IReadOnlyList<GroupCategoryChildDto> Children);

/// <summary>Por mês de <c>transactionDate</c>; <c>Paid</c> = efetivamente pago (principal + juros) das contas do mês.</summary>
public sealed record GroupMonthlyDto(string Month, decimal Expenses, decimal Paid);

/// <summary>Comparação com o período imediatamente anterior de mesma duração; variação nula quando a base é zero.</summary>
public sealed record GroupPreviousPeriodDto(
    DateOnly From,
    DateOnly To,
    decimal Expenses,
    decimal? ExpensesChangePercent);

public sealed record GroupDashboardDto(
    DashboardPeriodDto Period,
    GroupDashboardTotalsDto Totals,
    IReadOnlyList<GroupPaidByItemDto> PaidBy,
    IReadOnlyList<GroupCategoryShareDto> ExpensesByCategory,
    IReadOnlyList<GroupMonthlyDto> Monthly,
    GroupPreviousPeriodDto PreviousPeriod);
