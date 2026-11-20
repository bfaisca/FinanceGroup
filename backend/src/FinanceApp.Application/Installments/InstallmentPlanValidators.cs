using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Transactions;
using FluentValidation;

namespace FinanceApp.Application.Installments;

internal static class DateSanity
{
    public const int MinYear = 1900;
    public const int MaxYear = 2200;

    public static bool IsReasonable(DateOnly date) => date.Year is >= MinYear and <= MaxYear;
}

public sealed class CreateInstallmentPlanRequestValidator : AbstractValidator<CreateInstallmentPlanRequest>
{
    public CreateInstallmentPlanRequestValidator()
    {
        RuleFor(x => x.Description).TransactionDescription();
        RuleFor(x => x.TotalAmount).PositiveAmount("valor total");
        RuleFor(x => x.InstallmentCount)
            .InclusiveBetween(InstallmentCalculator.MinCount, InstallmentCalculator.MaxCount)
            .WithMessage($"O número de parcelas deve estar entre {InstallmentCalculator.MinCount} e {InstallmentCalculator.MaxCount}.");
        RuleFor(x => x.TotalAmount)
            .Must((x, total) => x.InstallmentCount is < InstallmentCalculator.MinCount or > InstallmentCalculator.MaxCount
                                || total <= 0
                                || total >= 0.01m * x.InstallmentCount)
            .WithMessage("O valor total deve ser de ao menos R$ 0,01 por parcela.");
        RuleFor(x => x.FirstDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("A data da primeira parcela é obrigatória.")
            .Must(d => DateSanity.IsReasonable(d!.Value)).WithMessage("A data da primeira parcela é inválida.");
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A categoria é obrigatória.");
        RuleFor(x => x.AccountId).NoAccountWithCard(x => x.CreditCardId is not null);
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
        RuleFor(x => x.PaymentMethod).CardPaymentMethod(x => x.CreditCardId is not null);
        RuleFor(x => x.Notes).TransactionNotes();
    }
}

public sealed class InstallmentPlanListQueryValidator : AbstractValidator<InstallmentPlanListQuery>
{
    public InstallmentPlanListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("A página deve ser maior ou igual a 1.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, InstallmentPlanListQuery.MaxPageSize)
            .WithMessage($"O tamanho da página deve estar entre 1 e {InstallmentPlanListQuery.MaxPageSize}.");
        RuleFor(x => x.Sort)
            .Must(s => string.IsNullOrWhiteSpace(s)
                       || InstallmentPlanListQuery.SortFields.Contains(s.Trim().TrimStart('-'), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Campo de ordenação inválido. Use: {string.Join(", ", InstallmentPlanListQuery.SortFields)} (prefixo '-' para descendente).");
        RuleFor(x => x.Status).Must(s => s is null || Enum.IsDefined(s.Value)).WithMessage("Estado inválido.");
    }
}
