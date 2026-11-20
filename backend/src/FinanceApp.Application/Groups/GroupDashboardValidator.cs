using FinanceApp.Application.Reports;
using FluentValidation;

namespace FinanceApp.Application.Groups;

public sealed class GroupDashboardQueryValidator : AbstractValidator<GroupDashboardQuery>
{
    public GroupDashboardQueryValidator()
    {
        RuleFor(x => x.From).NotNull().WithMessage("A data inicial (from) é obrigatória.");
        RuleFor(x => x.To).NotNull().WithMessage("A data final (to) é obrigatória.");
        RuleFor(x => x.To)
            .Must((x, to) => x.From is null || to is null || x.From.Value <= to.Value)
            .WithMessage("A data inicial não pode ser posterior à data final.");
        RuleFor(x => x.To)
            .Must((x, to) => x.From is null || to is null || x.From.Value > to.Value
                             || to.Value.DayNumber - x.From.Value.DayNumber + 1 <= GroupDashboardQuery.MaxDays)
            .WithMessage($"O período deve ter no máximo {GroupDashboardQuery.MaxDays} dias.");
        RuleFor(x => x.Months)
            .InclusiveBetween(1, GroupDashboardQuery.MaxMonths)
            .WithMessage($"O número de meses deve estar entre 1 e {GroupDashboardQuery.MaxMonths}.");
        RuleFor(x => x.EndMonth)
            .Must(m => m is null || ReportMath.TryParseMonth(m, out _))
            .WithMessage("O mês final (endMonth) deve estar no formato YYYY-MM.");
    }
}
