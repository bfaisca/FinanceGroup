using FluentValidation;

namespace FinanceApp.Application.Dashboard;

public sealed class DashboardQueryValidator : AbstractValidator<DashboardQuery>
{
    public DashboardQueryValidator()
    {
        RuleFor(x => x.From).NotNull().WithMessage("A data inicial (from) é obrigatória.");
        RuleFor(x => x.To).NotNull().WithMessage("A data final (to) é obrigatória.");
        RuleFor(x => x.To)
            .Must((x, to) => x.From is null || to is null || x.From.Value <= to.Value)
            .WithMessage("A data inicial não pode ser posterior à data final.");
        RuleFor(x => x.To)
            .Must((x, to) => x.From is null || to is null || x.From.Value > to.Value
                             || to.Value.DayNumber - x.From.Value.DayNumber + 1 <= DashboardQuery.MaxDays)
            .WithMessage($"O período deve ter no máximo {DashboardQuery.MaxDays} dias.");
    }
}
