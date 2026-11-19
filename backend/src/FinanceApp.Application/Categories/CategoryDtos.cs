using FinanceApp.Domain.Categories;

namespace FinanceApp.Application.Categories;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    CategoryType Type,
    Guid? ParentCategoryId,
    string? Icon,
    string? Color,
    bool IsDefault,
    bool IsActive)
{
    public static CategoryDto From(Category c) =>
        new(c.Id, c.Name, c.Type, c.ParentCategoryId, c.Icon, c.Color, c.IsDefault, c.IsActive);
}

public sealed class CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;

    public CategoryType? Type { get; init; }

    public Guid? ParentCategoryId { get; init; }

    public string? Icon { get; init; }

    public string? Color { get; init; }
}

public sealed class UpdateCategoryRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Icon { get; init; }

    public string? Color { get; init; }
}
