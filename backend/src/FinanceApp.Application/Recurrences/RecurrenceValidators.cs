using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Installments;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using FluentValidation;

namespace FinanceApp.Application.Recurrences;

internal static class RecurrenceRules
{
    public const int MaxPastStartDays = 366;

    public static IRuleBuilderOptions<T, RecurrenceFrequency?> FrequencyRule<T>(this IRuleBuilderInitial<T, RecurrenceFrequency?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("A frequência é obrigatória.")
            .Must(f => Enum.IsDefined(f!.Value)).WithMessage("Frequência inválida.");

    public static IRuleBuilderOptions<T, int?> IntervalRule<T>(this IRuleBuilder<T, int?> rule) =>
        rule.Must(i => i is null || i.Value is >= RecurrenceSchedule.MinInterval and <= RecurrenceSchedule.MaxInterval)
            .WithMessage($"O intervalo deve estar entre {RecurrenceSchedule.MinInterval} e {RecurrenceSchedule.MaxInterval}.");

    public static IRuleBuilderOptions<T, DateOnly?> EndDateRule<T>(this IRuleBuilder<T, DateOnly?> rule, Func<T, DateOnly?> startDate) =>
        rule.Must((x, end) => end is null || DateSanity.IsReasonable(end.Value))
            .WithMessage("A data final é inválida.")
            .Must((x, end) => end is null || startDate(x) is not { } start || end.Value >= start)
            .WithMessage("A data final não pode ser anterior à data inicial.");
}

public sealed class CreateRecurrenceRequestValidator : AbstractValidator<CreateRecurrenceRequest>
{
    public CreateRecurrenceRequestValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Description).TransactionDescription();
        RuleFor(x => x.Type)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("O tipo da recorrência é obrigatório.")
            .Must(t => Enum.IsDefined(t!.Value)).WithMessage("Tipo de movimentação inválido.");
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A categoria é obrigatória.");
        RuleFor(x => x.Frequency).FrequencyRule();
        RuleFor(x => x.Interval).IntervalRule();
        RuleFor(x => x.StartDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("A data inicial é obrigatória.")
            .Must(d => DateSanity.IsReasonable(d!.Value)).WithMessage("A data inicial é inválida.")
            .Must(d => d!.Value >= clock.Today.AddDays(-RecurrenceRules.MaxPastStartDays))
            .WithMessage($"A data inicial não pode ser anterior a {RecurrenceRules.MaxPastStartDays} dias atrás.");
        RuleFor(x => x.EndDate).EndDateRule(x => x.StartDate);
        RuleFor(x => x.CreditCardId)
            .Must((x, cardId) => cardId is null || x.Type == TransactionType.Expense)
            .WithMessage("Somente despesas podem ser lançadas no cartão de crédito.");
        RuleFor(x => x.AccountId).NoAccountWithCard(x => x.CreditCardId is not null);
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
        RuleFor(x => x.PaymentMethod).CardPaymentMethod(x => x.CreditCardId is not null);
        RuleFor(x => x.Notes).TransactionNotes();
    }
}

public sealed class UpdateRecurrenceRequestValidator : AbstractValidator<UpdateRecurrenceRequest>
{
    public UpdateRecurrenceRequestValidator()
    {
        RuleFor(x => x.Description).TransactionDescription();
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A categoria é obrigatória.");
        RuleFor(x => x.Frequency).FrequencyRule();
        RuleFor(x => x.Interval).IntervalRule();
        // endDate ≥ startDate depende da regra armazenada e é verificado pelo serviço (400 em errors.endDate).
        RuleFor(x => x.EndDate).EndDateRule(_ => null);
        RuleFor(x => x.AccountId).NoAccountWithCard(x => x.CreditCardId is not null);
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
        RuleFor(x => x.PaymentMethod).CardPaymentMethod(x => x.CreditCardId is not null);
        RuleFor(x => x.Notes).TransactionNotes();
    }
}
