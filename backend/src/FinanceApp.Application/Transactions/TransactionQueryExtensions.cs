using System.Linq.Expressions;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Transactions;

/// <summary>Filtros e ordenação de movimentações compartilhados entre a listagem individual (§8) e a de grupo (§16.2).</summary>
internal static class TransactionQueryExtensions
{
    /// <summary>Inclui subcategorias da categoria informada.</summary>
    public static IQueryable<Transaction> WhereCategoryOrChildren(this IQueryable<Transaction> query, Guid categoryId) =>
        query.Where(t => t.CategoryId == categoryId || t.Category!.ParentCategoryId == categoryId);

    /// <summary>Filtro pelo estado APRESENTADO (<c>Overdue</c> é calculado: Planned/Pending com vencimento anterior a hoje).</summary>
    public static IQueryable<Transaction> WherePresentedStatus(this IQueryable<Transaction> query, TransactionStatus status, DateOnly today) =>
        status switch
        {
            TransactionStatus.Overdue => query.Where(t =>
                (t.Status == TransactionStatus.Planned || t.Status == TransactionStatus.Pending)
                && t.DueDate != null && t.DueDate < today),
            TransactionStatus.Planned or TransactionStatus.Pending => query.Where(t =>
                t.Status == status && !(t.DueDate != null && t.DueDate < today)),
            _ => query.Where(t => t.Status == status),
        };

    public static IQueryable<Transaction> WhereDescriptionContains(this IQueryable<Transaction> query, string search)
    {
        var term = search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // Traduzido para SQL (lower/strpos): o provedor só traduz ToLower() e não StringComparison.
        return query.Where(t => t.Description.ToLower().Contains(term));
#pragma warning restore CA1311, CA1304, CA1862
    }

    /// <summary>Ordenação pelos campos do contrato (<c>sort</c>, prefixo <c>-</c> = descendente) com desempate estável.</summary>
    public static IOrderedQueryable<Transaction> ApplyContractSort(this IQueryable<Transaction> query, string? sort)
    {
        var spec = string.IsNullOrWhiteSpace(sort) ? TransactionListQuery.DefaultSort : sort.Trim();
        var descending = spec.StartsWith('-');
        var field = spec.TrimStart('-').ToLowerInvariant();

        var ordered = field switch
        {
            "transactiondate" => Order(query, t => t.TransactionDate, descending),
            "duedate" => Order(query, t => t.DueDate, descending),
            "amount" => Order(query, t => t.Amount, descending),
            "description" => Order(query, t => t.Description, descending),
            "createdat" => Order(query, t => t.CreatedAt, descending),
            _ => Order(query, t => t.TransactionDate, descending: true),
        };

        // Desempate determinístico para paginação estável.
        return ordered.ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id);
    }

    private static IOrderedQueryable<Transaction> Order<TKey>(
        IQueryable<Transaction> query,
        Expression<Func<Transaction, TKey>> key,
        bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);
}
