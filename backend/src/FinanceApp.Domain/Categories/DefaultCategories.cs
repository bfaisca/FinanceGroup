namespace FinanceApp.Domain.Categories;

/// <summary>
/// Categorias padrão do sistema (D-022). Os IDs são fixos para que o seed por migration seja idempotente
/// e estável entre ambientes.
/// </summary>
public static class DefaultCategories
{
    public sealed record Definition(Guid Id, string Name, CategoryType Type, string Icon, string Color);

    public static IReadOnlyList<Definition> All { get; } =
    [
        // Despesas
        Expense(1, "Alimentação", "pi-shopping-cart", "#F97316"),
        Expense(2, "Saúde", "pi-heart", "#EF4444"),
        Expense(3, "Educação", "pi-book", "#3B82F6"),
        Expense(4, "Lazer", "pi-ticket", "#A855F7"),
        Expense(5, "Assinaturas", "pi-sync", "#6366F1"),
        Expense(6, "Moradia", "pi-home", "#14B8A6"),
        Expense(7, "Transporte", "pi-car", "#EAB308"),
        Expense(8, "Compras", "pi-shopping-bag", "#EC4899"),
        Expense(9, "Outros", "pi-ellipsis-h", "#64748B"),

        // Receitas
        Income(1, "Salário", "pi-wallet", "#22C55E"),
        Income(2, "Freelance", "pi-briefcase", "#0EA5E9"),
        Income(3, "Investimentos", "pi-chart-line", "#10B981"),
        Income(4, "Reembolsos", "pi-replay", "#84CC16"),
        Income(5, "Outros", "pi-ellipsis-h", "#64748B"),
    ];

    private static Definition Expense(int n, string name, string icon, string color) =>
        new(new Guid($"00000000-0000-0000-0001-{n:D12}"), name, CategoryType.Expense, icon, color);

    private static Definition Income(int n, string name, string icon, string color) =>
        new(new Guid($"00000000-0000-0000-0002-{n:D12}"), name, CategoryType.Income, icon, color);
}
