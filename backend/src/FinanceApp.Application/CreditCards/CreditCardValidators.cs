using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.CreditCards;
using FluentValidation;

namespace FinanceApp.Application.CreditCards;

internal static class CreditCardRules
{
    public static IRuleBuilderOptions<T, string> CardName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O nome do cartão é obrigatório.")
            .Must(n => n.Trim().Length is >= 1 and <= CreditCard.NameMaxLength)
            .WithMessage($"O nome do cartão deve ter entre 1 e {CreditCard.NameMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> CardInstitution<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(i => string.IsNullOrWhiteSpace(i) || i.Trim().Length <= CreditCard.InstitutionMaxLength)
            .WithMessage($"A instituição deve ter no máximo {CreditCard.InstitutionMaxLength} caracteres.");

    public static IRuleBuilderOptionsConditions<T, decimal?> CardLimit<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.Custom((value, context) =>
        {
            if (value is not { } limit)
            {
                context.AddFailure("O limite do cartão é obrigatório.");
            }
            else if (limit < 0)
            {
                context.AddFailure("O limite do cartão não pode ser negativo.");
            }
            else if (!Domain.Common.MoneyRules.HasValidScale(limit))
            {
                context.AddFailure("O limite do cartão deve ter no máximo 2 casas decimais.");
            }
            else if (!Domain.Common.MoneyRules.IsWithinRange(limit))
            {
                context.AddFailure("O limite do cartão excede o valor permitido.");
            }
        });

    public static IRuleBuilderOptions<T, int?> BillingDay<T>(this IRuleBuilderInitial<T, int?> rule, string label) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage($"O dia de {label} é obrigatório.")
            .InclusiveBetween(BillingCycle.MinDay, BillingCycle.MaxDay).WithMessage($"O dia de {label} deve estar entre 1 e 31.");
}

public sealed class CreateCreditCardRequestValidator : AbstractValidator<CreateCreditCardRequest>
{
    public CreateCreditCardRequestValidator()
    {
        RuleFor(x => x.Name).CardName();
        RuleFor(x => x.Institution).CardInstitution();
        RuleFor(x => x.CreditLimit).CardLimit();
        RuleFor(x => x.ClosingDay).BillingDay("fechamento");
        RuleFor(x => x.DueDay).BillingDay("vencimento");
    }
}

public sealed class UpdateCreditCardRequestValidator : AbstractValidator<UpdateCreditCardRequest>
{
    public UpdateCreditCardRequestValidator()
    {
        RuleFor(x => x.Name).CardName();
        RuleFor(x => x.Institution).CardInstitution();
        RuleFor(x => x.CreditLimit).CardLimit();
        RuleFor(x => x.ClosingDay).BillingDay("fechamento");
        RuleFor(x => x.DueDay).BillingDay("vencimento");
    }
}
