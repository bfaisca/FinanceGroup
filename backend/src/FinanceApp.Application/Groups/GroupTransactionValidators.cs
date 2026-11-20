using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Transactions;
using FluentValidation;

namespace FinanceApp.Application.Groups;

public sealed class CreateGroupTransactionRequestValidator : AbstractValidator<CreateGroupTransactionRequest>
{
    public CreateGroupTransactionRequestValidator()
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
        RuleFor(x => x.DueDate)
            .Must((x, due) => due is null || x.TransactionDate is null || due.Value >= x.TransactionDate.Value)
            .WithMessage("O vencimento não pode ser anterior à data da movimentação.");
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
        RuleFor(x => x.Type).Must(t => t is null || Enum.IsDefined(t.Value)).WithMessage("Tipo de movimentação inválido.");
        RuleFor(x => x.Status).Must(s => s is null || Enum.IsDefined(s.Value)).WithMessage("Estado inválido.");
        RuleFor(x => x.Search)
            .MaximumLength(TransactionListQueryValidator.SearchMaxLength)
            .WithMessage($"A busca deve ter no máximo {TransactionListQueryValidator.SearchMaxLength} caracteres.");
    }
}
