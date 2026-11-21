using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Transactions;
using FluentValidation;

namespace FinanceApp.Application.Groups;

public sealed class CreateGroupTransactionRequestValidator : AbstractValidator<CreateGroupTransactionRequest>
{
    public CreateGroupTransactionRequestValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Description).TransactionDescription();
        // `type` é opcional: grupos só têm despesas; Income é recusado pelo serviço com 422 (D-046).
        RuleFor(x => x.Type).Must(t => t is null || Enum.IsDefined(t.Value)).WithMessage("Tipo de movimentação inválido.");
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A categoria é obrigatória.");
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
        RuleFor(x => x.Status).UserSelectableStatus();
        RuleFor(x => x.TransactionDate).RequiredDate("A data da movimentação é obrigatória.");
        RuleFor(x => x.Notes).TransactionNotes();
        RuleFor(x => x.Payment).SetValidator(new GroupPaymentRequestValidator(clock, amountRequired: false)!);
        RuleFor(x => x.Recurrence).SetValidator(new GroupRecurrenceRequestValidator()!);
    }
}

public sealed class GroupPaymentRequestValidator : AbstractValidator<GroupPaymentRequest>
{
    public GroupPaymentRequestValidator(IDateTimeProvider clock)
        : this(clock, amountRequired: true)
    {
    }

    internal GroupPaymentRequestValidator(IDateTimeProvider clock, bool amountRequired)
    {
        if (amountRequired)
        {
            RuleFor(x => x.Amount).NotNull().WithMessage("O valor do pagamento é obrigatório.");
        }

        RuleFor(x => x.Amount).PositiveAmount("valor do pagamento");
        RuleFor(x => x.InterestAmount).Custom((value, context) =>
        {
            if (value is not { } v)
            {
                return;
            }

            if (v < 0)
            {
                context.AddFailure("Os juros/multa não podem ser negativos.");
            }
            else if (!MoneyRules.HasValidScale(v))
            {
                context.AddFailure("Os juros/multa devem ter no máximo 2 casas decimais.");
            }
            else if (!MoneyRules.IsWithinRange(v))
            {
                context.AddFailure("Os juros/multa excedem o limite permitido.");
            }
        });
        RuleFor(x => x.PaymentDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("A data do pagamento é obrigatória.")
            .Must(d => d!.Value <= clock.Today).WithMessage("A data do pagamento não pode ser futura.");
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
    }
}

public sealed class GroupRecurrenceRequestValidator : AbstractValidator<GroupRecurrenceRequest>
{
    public GroupRecurrenceRequestValidator()
    {
        RuleFor(x => x.Frequency)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("A frequência da recorrência é obrigatória.")
            .Must(f => GroupTransactionSeries.IsSupported(f!.Value))
            .WithMessage("Frequência inválida. Use Weekly, Biweekly, Monthly, Bimonthly, Quarterly, Semiannual ou Annual.");
        RuleFor(x => x.Occurrences)
            .InclusiveBetween(GroupTransactionSeries.MinOccurrences, GroupTransactionSeries.MaxOccurrences)
            .WithMessage($"O número de ocorrências deve estar entre {GroupTransactionSeries.MinOccurrences} e {GroupTransactionSeries.MaxOccurrences}.");
    }
}

public sealed class UpdateGroupTransactionRequestValidator : AbstractValidator<UpdateGroupTransactionRequest>
{
    public UpdateGroupTransactionRequestValidator()
    {
        RuleFor(x => x.Description).TransactionDescription();
        RuleFor(x => x.Amount).PositiveAmount();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A categoria é obrigatória.");
        RuleFor(x => x.PaymentMethod).PaymentMethodRule();
        RuleFor(x => x.Status).UserSelectableStatus();
        RuleFor(x => x.TransactionDate).RequiredDate("A data da movimentação é obrigatória.");
        RuleFor(x => x.Notes).TransactionNotes();
    }
}

public sealed class GroupTransactionListQueryValidator : AbstractValidator<GroupTransactionListQuery>
{
    public GroupTransactionListQueryValidator()
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
        RuleFor(x => x.Status).Must(s => s is null || Enum.IsDefined(s.Value)).WithMessage("Estado inválido.");
        RuleFor(x => x.Search)
            .MaximumLength(TransactionListQueryValidator.SearchMaxLength)
            .WithMessage($"A busca deve ter no máximo {TransactionListQueryValidator.SearchMaxLength} caracteres.");
    }
}
