using FinanceApp.Domain.Categories;
using FluentValidation;

namespace FinanceApp.Application.Categories;

internal static class CategoryRules
{
    public static IRuleBuilderOptions<T, string> CategoryName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O nome da categoria é obrigatório.")
            .Must(n => n.Trim().Length is >= 1 and <= Category.NameMaxLength)
            .WithMessage($"O nome da categoria deve ter entre 1 e {Category.NameMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> CategoryIcon<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(i => string.IsNullOrWhiteSpace(i) || Category.IsValidIcon(i.Trim()))
            .WithMessage($"O ícone deve ser o nome de um ícone (ex.: pi-home), com até {Category.IconMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> CategoryColor<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(c => string.IsNullOrWhiteSpace(c) || Category.IsValidColor(c.Trim()))
            .WithMessage("A cor deve estar no formato #RRGGBB.");
}

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).CategoryName();
        RuleFor(x => x.Type)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("O tipo da categoria é obrigatório.")
            .Must(t => Enum.IsDefined(t!.Value)).WithMessage("Tipo de categoria inválido.");
        RuleFor(x => x.Icon).CategoryIcon();
        RuleFor(x => x.Color).CategoryColor();
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).CategoryName();
        RuleFor(x => x.Icon).CategoryIcon();
        RuleFor(x => x.Color).CategoryColor();
    }
}
