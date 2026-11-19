namespace FinanceApp.Application.Common;

/// <summary>Resposta paginada padrão (docs/API_CONTRACTS.md §1.1).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public static class PagedResult
{
    public static PagedResult<T> Create<T>(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount, totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
}
