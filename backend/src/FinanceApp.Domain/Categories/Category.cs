using System.Text.RegularExpressions;
using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Categories;

/// <summary>
/// Categoria de despesa ou receita. <see cref="UserId"/> nulo = categoria padrão do sistema (somente leitura).
/// Subcategorias têm o mesmo tipo do pai e profundidade máxima 1 (D-022).
/// </summary>
public sealed partial class Category
{
    public const int NameMaxLength = 60;
    public const int IconMaxLength = 50;
    public const int ColorLength = 7;

    private Category()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string Name { get; private set; }

    public CategoryType Type { get; private set; }

    public Guid? ParentCategoryId { get; private set; }

    public string? Icon { get; private set; }

    public string? Color { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public Category? ParentCategory { get; private set; }

    public static Category CreateCustom(
        Guid userId,
        string name,
        CategoryType type,
        Category? parent,
        string? icon,
        string? color)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Tipo de categoria inválido.");
        }

        if (parent is not null)
        {
            if (parent.ParentCategoryId is not null)
            {
                throw new DomainException("Subcategorias não podem ter subcategorias (profundidade máxima 1).");
            }

            if (parent.Type != type)
            {
                throw new DomainException("A subcategoria deve ter o mesmo tipo da categoria pai.");
            }

            if (!parent.IsActive)
            {
                throw new DomainException("A categoria pai está inativa.");
            }

            if (!parent.IsDefault && parent.UserId != userId)
            {
                throw new DomainException("A categoria pai não pertence ao usuário.");
            }
        }

        return new Category
        {
            Id = Guid.NewGuid(),
            UserId = Guard.NotEmpty(userId, "O usuário é obrigatório."),
            Name = NormalizeName(name),
            Type = type,
            ParentCategoryId = parent?.Id,
            Icon = NormalizeIcon(icon),
            Color = NormalizeColor(color),
            IsDefault = false,
            IsActive = true,
        };
    }

    public void Update(string name, string? icon, string? color)
    {
        EnsureEditable();
        Name = NormalizeName(name);
        Icon = NormalizeIcon(icon);
        Color = NormalizeColor(color);
    }

    public void Deactivate()
    {
        EnsureEditable();
        IsActive = false;
    }

    /// <summary>Reativa a categoria; uma subcategoria só pode ser reativada com o pai ativo.</summary>
    public void Activate(Category? parent)
    {
        EnsureEditable();
        if (ParentCategoryId is not null && parent is not { IsActive: true })
        {
            throw new DomainException("Reative a categoria pai antes de reativar esta subcategoria.");
        }

        IsActive = true;
    }

    /// <summary>Indica se a categoria pode classificar movimentações do tipo informado.</summary>
    public bool Accepts(Transactions.TransactionType transactionType) =>
        Type == CategoryType.Expense
            ? transactionType == Transactions.TransactionType.Expense
            : transactionType == Transactions.TransactionType.Income;

    private void EnsureEditable()
    {
        if (IsDefault)
        {
            throw new DomainException("Categorias padrão do sistema não podem ser alteradas nem desativadas.");
        }
    }

    private static string NormalizeName(string name)
    {
        var trimmed = Guard.NotBlank(name, "O nome da categoria é obrigatório.").Trim();
        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"O nome da categoria deve ter no máximo {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeIcon(string? icon)
    {
        var trimmed = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        if (trimmed is not null && (trimmed.Length > IconMaxLength || !IconRegex().IsMatch(trimmed)))
        {
            throw new DomainException("Ícone inválido.");
        }

        return trimmed;
    }

    private static string? NormalizeColor(string? color)
    {
        var trimmed = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        if (trimmed is not null && !ColorRegex().IsMatch(trimmed))
        {
            throw new DomainException("A cor deve estar no formato #RRGGBB.");
        }

        return trimmed;
    }

    public static bool IsValidIcon(string icon) => icon.Length <= IconMaxLength && IconRegex().IsMatch(icon);

    public static bool IsValidColor(string color) => ColorRegex().IsMatch(color);

    [GeneratedRegex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex IconRegex();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex ColorRegex();
}
