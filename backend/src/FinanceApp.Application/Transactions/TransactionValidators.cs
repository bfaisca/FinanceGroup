using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Transactions;
using FluentValidation;

namespace FinanceApp.Application.Transactions;

internal static class TransactionRules
{
    public static IRuleBuilderOptions<T, string> TransactionDescription<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("A descrição é obrigatória.")
            .Must(d => d.Trim().Length is >= 1 and <= Transaction.DescriptionMaxLength)
            .WithMessage($"A descrição deve ter entre 1 e {Transaction.DescriptionMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> TransactionNotes<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(n => string.IsNullOrWhiteSpace(n) || n.Trim().Length <= Transaction.NotesMaxLength)
            .WithMessage($"As observações devem ter no máximo {Transaction.NotesMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, PaymentMethod?> PaymentMethodRule<T>(this IRuleBuilder<T, PaymentMethod?> rule) =>
        rule.Must(m => m is null || Enum.IsDefined(m.Value)).WithMessage("Forma de pagamento inválida.");

    /// <summary>Somente <c>Planned</c> ou <c>Pending</c> podem ser escolhidos pelo usuário.</summary>
    public static IRuleBuilderOptions<T, TransactionStatus?> UserSelectableStatus<T>(this IRuleBuilder<T, TransactionStatus?> rule) =>
        rule.Must(s => s is null or TransactionStatus.Planned or TransactionStatus.Pending)
            .WithMessage("O estado deve ser Planned (previsto) ou Pending (pendente).");

    public static IRuleBuilderOptions<T, DateOnly?> RequiredDate<T>(this IRuleBuilder<T, DateOnly?> rule, string message) =>
        rule.NotNull().WithMessage(message);
}

public sealed class CreateTransactionRequestValidator : AbstractValidator<CreateTransactionRequest>
{
    public CreateTransactionRequestValidator()
    {
        RuleFor(x => x.Description).TransactionDescription();
        RuleFor(x => x.Type)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("O tipo da movimentação é obrigatório.")
            .Must(t => Enum.IsDefined(t!.Value)).WithMessage("Tipo de movimentação inválido.");
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A categoria é obrigatória.");
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
        RuleFor(x => x.Status).UserSelectableStatus();
        RuleFor(x => x.TransactionDate).RequiredDate("A data da movimentação é obrigatória.");
        RuleFor(x => x.DueDate)
            .Must((x, due) => due is null || x.TransactionDate is null || due.Value >= x.TransactionDate.Value)
            .WithMessage("O vencimento não pode ser anterior à data da movimentação.");
        RuleFor(x => x.Notes).TransactionNotes();
    }
}

public sealed class UpdateTransactionRequestValidator : AbstractValidator<UpdateTransactionRequest>
{
    public UpdateTransactionRequestValidator()
    {
        RuleFor(x => x.Description).TransactionDescription();
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A categoria é obrigatória.");
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
        RuleFor(x => x.Status).UserSelectableStatus();
        RuleFor(x => x.TransactionDate).RequiredDate("A data da movimentação é obrigatória.");
        RuleFor(x => x.DueDate)
            .Must((x, due) => due is null || x.TransactionDate is null || due.Value >= x.TransactionDate.Value)
            .WithMessage("O vencimento não pode ser anterior à data da movimentação.");
        RuleFor(x => x.Notes).TransactionNotes();
    }
}

public sealed class AddPaymentRequestValidator : AbstractValidator<AddPaymentRequest>
{
    public AddPaymentRequestValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Amount).PositiveAmount("valor do pagamento");
        RuleFor(x => x.PaymentDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("A data do pagamento é obrigatória.")
            .Must(d => d!.Value <= clock.Today).WithMessage("A data do pagamento não pode ser futura.");
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
    }
}

public sealed class TransactionListQueryValidator : AbstractValidator<TransactionListQuery>
{
    public const int SearchMaxLength = 200;

    public TransactionListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("A página deve ser maior ou igual a 1.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, TransactionListQuery.MaxPageSize)
            .WithMessage($"O tamanho da página deve estar entre 1 e {TransactionListQuery.MaxPageSize}.");
        RuleFor(x => x.Sort)
            .Must(s => string.IsNullOrWhiteSpace(s)
                       || TransactionListQuery.SortFields.Contains(s.Trim().TrimStart('-'), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Campo de ordenação inválido. Use: {string.Join(", ", TransactionListQuery.SortFields)} (prefixo '-' para descendente).");
        RuleFor(x => x.To)
            .Must((x, to) => to is null || x.From is null || x.From.Value <= to.Value)
            .WithMessage("A data inicial não pode ser posterior à data final.");
        RuleFor(x => x.DueTo)
            .Must((x, to) => to is null || x.DueFrom is null || x.DueFrom.Value <= to.Value)
            .WithMessage("O vencimento inicial não pode ser posterior ao final.");
        RuleFor(x => x.Type).Must(t => t is null || Enum.IsDefined(t.Value)).WithMessage("Tipo de movimentação inválido.");
        RuleFor(x => x.Status).Must(s => s is null || Enum.IsDefined(s.Value)).WithMessage("Estado inválido.");
        RuleFor(x => x.Search).MaximumLength(SearchMaxLength).WithMessage($"A busca deve ter no máximo {SearchMaxLength} caracteres.");
    }
}
