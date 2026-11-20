using FinanceApp.Domain.Transactions;
using FluentValidation;

namespace FinanceApp.Application.Reports;

public abstract class PeriodQueryValidator<T> : AbstractValidator<T>
    where T : PeriodQuery
{
    protected PeriodQueryValidator()
    {
        RuleFor(x => x.From).NotNull().WithMessage("A data inicial (from) é obrigatória.");
        RuleFor(x => x.To).NotNull().WithMessage("A data final (to) é obrigatória.");
        RuleFor(x => x.To)
            .Must((x, to) => x.From is null || to is null || x.From.Value <= to.Value)
            .WithMessage("A data inicial não pode ser posterior à data final.");
        RuleFor(x => x.To)
            .Must((x, to) => x.From is null || to is null || x.From.Value > to.Value
                             || to.Value.DayNumber - x.From.Value.DayNumber + 1 <= PeriodQuery.MaxDays)
            .WithMessage($"O período deve ter no máximo {PeriodQuery.MaxDays} dias.");
    }
}

public sealed class SummaryQueryValidator : PeriodQueryValidator<SummaryQuery>
{
    public SummaryQueryValidator() =>
        RuleFor(x => x.GroupBy).Must(g => g is null || Enum.IsDefined(g.Value)).WithMessage("groupBy deve ser 'day' ou 'month'.");
}

public sealed class TypedPeriodQueryValidator : PeriodQueryValidator<TypedPeriodQuery>
{
    public TypedPeriodQueryValidator() =>
        RuleFor(x => x.Type).Must(t => t is null || Enum.IsDefined(t.Value)).WithMessage("Tipo inválido. Use Expense ou Income.");
}

public sealed class MonthlyEvolutionQueryValidator : AbstractValidator<MonthlyEvolutionQuery>
{
    public MonthlyEvolutionQueryValidator()
    {
        RuleFor(x => x.Months)
            .InclusiveBetween(1, MonthlyEvolutionQuery.MaxMonths)
            .WithMessage($"O número de meses deve estar entre 1 e {MonthlyEvolutionQuery.MaxMonths}.");
        RuleFor(x => x.EndMonth)
            .Must(m => m is null || ReportMath.TryParseMonth(m, out _))
            .WithMessage("O mês final (endMonth) deve estar no formato YYYY-MM.");
    }
}

public sealed class InstallmentsReportQueryValidator : AbstractValidator<InstallmentsReportQuery>
{
    public InstallmentsReportQueryValidator() =>
        RuleFor(x => x.Months)
            .InclusiveBetween(1, MonthlyEvolutionQuery.MaxMonths)
            .WithMessage($"O número de meses deve estar entre 1 e {MonthlyEvolutionQuery.MaxMonths}.");
}

internal static class ReportDefaults
{
    public static TransactionType TypeOrDefault(TransactionType? type) => type ?? TransactionType.Expense;
}
