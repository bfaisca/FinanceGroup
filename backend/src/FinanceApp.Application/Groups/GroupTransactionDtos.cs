using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Groups;

/// <summary>Usuário exibido no grupo: id e nome (nunca e-mail). Contas excluídas aparecem como "Usuário removido".</summary>
public sealed record GroupUserRefDto(Guid UserId, string Name);

public sealed record GroupTransactionPermissionsDto(bool CanEdit, bool CanCancel, bool CanDelete, bool CanPay);

/// <summary>
/// Movimentação do grupo. NÃO contém conta, cartão nem qualquer dado bancário (D-032/D-035). <c>Status</c> é o estado
/// APRESENTADO (inclui <c>Overdue</c>).
/// </summary>
public sealed record GroupTransactionDto(
    Guid Id,
    Guid GroupId,
    string Description,
    TransactionType Type,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    TransactionStatus Status,
    Guid CategoryId,
    string CategoryName,
    string? ParentCategoryName,
    string? CategoryColor,
    string? CategoryIcon,
    PaymentMethod? PaymentMethod,
    DateOnly TransactionDate,
    DateOnly? DueDate,
    DateOnly? PaidAt,
    string? Notes,
    GroupUserRefDto CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    GroupTransactionPermissionsDto Permissions);

public sealed record GroupTransactionPaymentDto(
    Guid Id,
    decimal Amount,
    DateOnly PaymentDate,
    PaymentMethod? PaymentMethod,
    GroupUserRefDto PaidBy,
    DateTimeOffset CreatedAt,
    bool CanRemove);

public sealed record GroupTransactionDetailDto(
    Guid Id,
    Guid GroupId,
    string Description,
    TransactionType Type,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    TransactionStatus Status,
    Guid CategoryId,
    string CategoryName,
    string? ParentCategoryName,
    string? CategoryColor,
    string? CategoryIcon,
    PaymentMethod? PaymentMethod,
    DateOnly TransactionDate,
    DateOnly? DueDate,
    DateOnly? PaidAt,
    string? Notes,
    GroupUserRefDto CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    GroupTransactionPermissionsDto Permissions,
    IReadOnlyList<GroupTransactionPaymentDto> Payments)
{
    public static GroupTransactionDetailDto From(GroupTransactionDto t, IReadOnlyList<GroupTransactionPaymentDto> payments) =>
        new(
            t.Id, t.GroupId, t.Description, t.Type, t.Amount, t.PaidAmount, t.RemainingAmount, t.Status, t.CategoryId, t.CategoryName,
            t.ParentCategoryName, t.CategoryColor, t.CategoryIcon, t.PaymentMethod, t.TransactionDate, t.DueDate, t.PaidAt, t.Notes,
            t.CreatedBy, t.CreatedAt, t.UpdatedAt, t.Permissions, payments);
}

public sealed record GroupTransactionHistoryEntryDto(
    Guid Id,
    TransactionAuditAction Action,
    string? Field,
    string? OldValue,
    string? NewValue,
    string ActorName,
    DateTimeOffset CreatedAt);

public sealed class CreateGroupTransactionRequest
{
    public string Description { get; init; } = string.Empty;

    public TransactionType? Type { get; init; }

    public decimal Amount { get; init; }

    public Guid CategoryId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    /// <summary><c>Planned</c> ou <c>Pending</c> (padrão <c>Pending</c>).</summary>
    public TransactionStatus? Status { get; init; }

    public DateOnly? TransactionDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public string? Notes { get; init; }
}

/// <summary>Mesmos campos da criação, exceto <c>type</c> (imutável).</summary>
public sealed class UpdateGroupTransactionRequest
{
    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid CategoryId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    public TransactionStatus? Status { get; init; }

    public DateOnly? TransactionDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public string? Notes { get; init; }
}

/// <summary>Parâmetros de <c>GET /groups/{id}/transactions</c> (contrato §16.2).</summary>
public sealed class GroupTransactionListQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = Transactions.TransactionListQuery.DefaultPageSize;

    public string? Sort { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public TransactionType? Type { get; init; }

    /// <summary>Inclui subcategorias.</summary>
    public Guid? CategoryId { get; init; }

    public TransactionStatus? Status { get; init; }

    public string? Search { get; init; }

    public Guid? CreatedByUserId { get; init; }

    /// <summary>Movimentações com ao menos um pagamento deste usuário.</summary>
    public Guid? PaidByUserId { get; init; }
}
