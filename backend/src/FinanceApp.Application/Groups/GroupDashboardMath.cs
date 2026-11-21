using FinanceApp.Application.Reports;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Users;

namespace FinanceApp.Application.Groups;

/// <summary>Pagamentos somados por pagador (principal e juros) antes de virar item de "Contas Pagas".</summary>
internal sealed record PayerAmount(Guid UserId, decimal Principal, decimal Interest);

/// <summary>Cálculos puros do dashboard do grupo (D-034, emendada por D-046/D-048).</summary>
internal static class GroupDashboardMath
{
    /// <summary>Pendente = originais − principal pago, nunca negativo; pago = principal + juros.</summary>
    public static GroupDashboardTotalsDto Totals(decimal expenses, decimal paidPrincipal, decimal interest) =>
        new(expenses, paidPrincipal, interest, paidPrincipal + interest, Math.Max(0m, expenses - paidPrincipal));

    /// <summary>
    /// Variação percentual em relação ao período anterior (2 casas, arredondamento comercial); base zero → <c>null</c>.
    /// </summary>
    public static decimal? ChangePercent(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) / previous * 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// "Contas Pagas": quem pagou algo (principal + juros), percentual sobre o total efetivamente pago, ordenado do maior para o
    /// menor valor e depois por nome.
    /// </summary>
    public static IReadOnlyList<GroupPaidByItemDto> BuildPaidBy(
        IReadOnlyCollection<PayerAmount> amounts,
        IReadOnlyDictionary<Guid, string> names,
        IReadOnlyDictionary<Guid, GroupMemberStatus> statuses)
    {
        var totalPaid = amounts.Sum(a => a.Principal + a.Interest);
        return amounts
            .Where(a => a.Principal + a.Interest > 0)
            .Select(a => new GroupPaidByItemDto(
                a.UserId,
                names.GetValueOrDefault(a.UserId, User.AnonymizedName),
                statuses.GetValueOrDefault(a.UserId, GroupMemberStatus.Left),
                a.Principal + a.Interest,
                a.Interest,
                ReportMath.Percentage(a.Principal + a.Interest, totalPaid)))
            .OrderByDescending(i => i.PaidAmount)
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
