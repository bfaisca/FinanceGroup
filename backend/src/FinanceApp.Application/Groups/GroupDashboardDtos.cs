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

/// <summary>Totais do período (movimentações NÃO canceladas). Pendente = valor − pago; nunca conta como pago.</summary>
public sealed record GroupDashboardTotalsDto(
    decimal Expenses,
    decimal ExpensesPaid,
    decimal ExpensesPending,
    decimal Income,
    decimal IncomeReceived,
    decimal IncomePending,
    decimal Balance);

/// <summary>Participação (indicador estatístico, não é dívida): percentuais 0–100 com 2 casas.</summary>
public sealed record GroupParticipationItemDto(
    Guid UserId,
    string Name,
    GroupMemberStatus Status,
    decimal ExpenseAmount,
    decimal ExpensePercent,
    decimal IncomeAmount,
    decimal IncomePercent);

public sealed record GroupCategoryChildDto(Guid CategoryId, string Name, decimal Amount, decimal Percent);

public sealed record GroupCategoryShareDto(
    Guid CategoryId,
    string Name,
    string? Color,
    decimal Amount,
    decimal Percent,
    IReadOnlyList<GroupCategoryChildDto> Children);

public sealed record GroupMonthlyDto(string Month, decimal Income, decimal Expenses);

/// <summary>Comparação com o período imediatamente anterior de mesma duração; variação nula quando a base é zero.</summary>
public sealed record GroupPreviousPeriodDto(
    DateOnly From,
    DateOnly To,
    decimal Expenses,
    decimal Income,
    decimal? ExpensesChangePercent,
    decimal? IncomeChangePercent);

public sealed record GroupDashboardDto(
    DashboardPeriodDto Period,
    GroupDashboardTotalsDto Totals,
    IReadOnlyList<GroupParticipationItemDto> CreatedBy,
    IReadOnlyList<GroupParticipationItemDto> PaidBy,
    IReadOnlyList<GroupCategoryShareDto> ExpensesByCategory,
    IReadOnlyList<GroupMonthlyDto> Monthly,
    GroupPreviousPeriodDto PreviousPeriod);
