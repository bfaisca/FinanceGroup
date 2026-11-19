using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Accounts;
using FluentValidation;

namespace FinanceApp.Application.Accounts;

internal static class AccountRules
{
    public static IRuleBuilderOptions<T, string> AccountName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O nome da conta é obrigatório.")
            .Must(n => n.Trim().Length is >= 1 and <= Account.NameMaxLength)
            .WithMessage($"O nome da conta deve ter entre 1 e {Account.NameMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> AccountInstitution<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(i => string.IsNullOrWhiteSpace(i) || i.Trim().Length <= Account.InstitutionMaxLength)
            .WithMessage($"A instituição deve ter no máximo {Account.InstitutionMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, AccountType?> AccountTypeRule<T>(this IRuleBuilderInitial<T, AccountType?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("O tipo da conta é obrigatório.")
            .Must(t => Enum.IsDefined(t!.Value)).WithMessage("Tipo de conta inválido.");

    public static IRuleBuilderOptions<T, DateOnly?> NotFutureDate<T>(this IRuleBuilder<T, DateOnly?> rule, IDateTimeProvider clock, string message) =>
        rule.Must(d => d is null || d.Value <= clock.Today).WithMessage(message);
}

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Name).AccountName();
        RuleFor(x => x.Institution).AccountInstitution();
        RuleFor(x => x.Type).AccountTypeRule();
        RuleFor(x => x.InitialBalance).SignedAmount("saldo inicial");
        RuleFor(x => x.BalanceReferenceDate).NotFutureDate(clock, "A data de referência do saldo não pode ser futura.");
        RuleFor(x => x.InitialBalance)
            .NotNull()
            .When(x => x.BalanceReferenceDate is not null)
            .WithMessage("Informe o saldo inicial junto com a data de referência.");
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x.Name).AccountName();
        RuleFor(x => x.Institution).AccountInstitution();
        RuleFor(x => x.Type).AccountTypeRule();
    }
}

public sealed class CreateBalanceSnapshotRequestValidator : AbstractValidator<CreateBalanceSnapshotRequest>
{
    public CreateBalanceSnapshotRequestValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Balance).NotNull().WithMessage("O saldo é obrigatório.");
        RuleFor(x => x.Balance).SignedAmount();
        RuleFor(x => x.ReferenceDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("A data de referência é obrigatória.")
            .NotFutureDate(clock, "A data de referência do saldo não pode ser futura.");
    }
}
