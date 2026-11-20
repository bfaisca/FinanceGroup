using FinanceApp.Application.Reports;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Users;

namespace FinanceApp.Application.Groups;

/// <summary>Soma por usuário (autoria ou pagamentos) antes de virar item de participação.</summary>
internal sealed record ParticipationAmount(Guid UserId, decimal Expense, decimal Income);

/// <summary>Cálculos puros do dashboard do grupo (D-034).</summary>
internal static class GroupDashboardMath
{
    /// <summary>
    /// Variação percentual em relação ao período anterior (2 casas, arredondamento comercial); base zero → <c>null</c>.
    /// </summary>
    public static decimal? ChangePercent(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) / previous * 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Itens de participação: percentual sobre o total do MESMO tipo (soma dos valores informados), ordenados pelo maior valor
    /// total (despesa + receita) e depois por nome; apenas quem tem valor no período.
    /// </summary>
    public static IReadOnlyList<GroupParticipationItemDto> BuildParticipation(
        IReadOnlyCollection<ParticipationAmount> amounts,
        IReadOnlyDictionary<Guid, string> names,
        IReadOnlyDictionary<Guid, GroupMemberStatus> statuses)
    {
        var totalExpense = amounts.Sum(a => a.Expense);
        var totalIncome = amounts.Sum(a => a.Income);

        return amounts
            .Where(a => a.Expense != 0 || a.Income != 0)
            .Select(a => new GroupParticipationItemDto(
                a.UserId,
                names.GetValueOrDefault(a.UserId, User.AnonymizedName),
                statuses.GetValueOrDefault(a.UserId, GroupMemberStatus.Left),
                a.Expense,
                ReportMath.Percentage(a.Expense, totalExpense),
                a.Income,
                ReportMath.Percentage(a.Income, totalIncome)))
            .OrderByDescending(i => i.ExpenseAmount + i.IncomeAmount)
            .ThenBy(i => i.Name, StringComparer.CurrentCulture)
            .ThenBy(i => i.UserId)
            .ToList();
    }

    /// <summary>Período anterior imediatamente antes de <paramref name="from"/>, com a mesma duração (em dias inclusivos).</summary>
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        var previousTo = from.AddDays(-1);
        return (previousTo.AddDays(-(days - 1)), previousTo);
    }
}
