using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Groups;

/// <summary>Usuário exibido no grupo: id e nome (nunca e-mail). Contas excluídas aparecem como "Usuário removido".</summary>
public sealed record GroupUserRefDto(Guid UserId, string Name);

public sealed record GroupTransactionPermissionsDto(bool CanEdit, bool CanCancel, bool CanDelete, bool CanPay);

/// <summary>Recorrência do grupo (D-049): <c>Total</c> é o número ORIGINAL de ocorrências da série.</summary>
public sealed record GroupTransactionSeriesDto(Guid Id, int Number, int Total);

/// <summary>
/// Despesa do grupo (contrato §16.2). NÃO contém conta, cartão nem qualquer dado bancário (D-032/D-035). <c>Status</c> é o
/// estado APRESENTADO (inclui <c>Overdue</c>). <c>Amount</c> é o valor ORIGINAL; <c>PaidAmount</c> o principal coberto (define a
/// situação); <c>TotalPaid</c> = principal + juros (D-048). <c>Type</c> é sempre <c>Expense</c> (D-046).
/// </summary>
public sealed record GroupTransactionDto(
    Guid Id,
    Guid GroupId,
    string Description,
    TransactionType Type,
    decimal Amount,
    decimal PaidAmount,
    decimal InterestPaid,
    decimal TotalPaid,
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
    GroupTransactionSeriesDto? Series,
    GroupUserRefDto CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    GroupTransactionPermissionsDto Permissions);

public sealed record GroupTransactionPaymentDto(
    Guid Id,
    decimal Amount,
    decimal InterestAmount,
    decimal TotalAmount,
    DateOnly PaymentDate,
    PaymentMethod? PaymentMethod,
    GroupUserRefDto PaidBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanRemove);

public sealed record GroupTransactionDetailDto(
    Guid Id,
    Guid GroupId,
    string Description,
    TransactionType Type,
    decimal Amount,
    decimal PaidAmount,
    decimal InterestPaid,
    decimal TotalPaid,
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
    GroupTransactionSeriesDto? Series,
    GroupUserRefDto CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    GroupTransactionPermissionsDto Permissions,
    IReadOnlyList<GroupTransactionPaymentDto> Payments)
{
    public static GroupTransactionDetailDto From(GroupTransactionDto t, IReadOnlyList<GroupTransactionPaymentDto> payments) =>
        new(
            t.Id, t.GroupId, t.Description, t.Type, t.Amount, t.PaidAmount, t.InterestPaid, t.TotalPaid, t.RemainingAmount, t.Status,
            t.CategoryId, t.CategoryName, t.ParentCategoryName, t.CategoryColor, t.CategoryIcon, t.PaymentMethod, t.TransactionDate,
            t.DueDate, t.PaidAt, t.Notes, t.Series, t.CreatedBy, t.CreatedAt, t.UpdatedAt, t.Permissions, payments);
}

public sealed record GroupTransactionHistoryEntryDto(
    Guid Id,
    TransactionAuditAction Action,
    string? Field,
    string? OldValue,
    string? NewValue,
    string ActorName,
    DateTimeOffset CreatedAt);

/// <summary>Pagamento de uma despesa do grupo (§16.2): principal em <c>Amount</c>, juros/multa à parte (D-048).</summary>
public sealed class GroupPaymentRequest
{
    /// <summary>Principal aplicado (&gt; 0). Em <c>payment</c> da criação, o padrão é o valor da conta.</summary>
    public decimal? Amount { get; init; }

    /// <summary>Juros/multa (≥ 0, padrão 0); não entra na regra "Σ principal ≤ valor da conta".</summary>
    public decimal? InterestAmount { get; init; }

    public DateOnly? PaymentDate { get; init; }

    /// <summary>Conta do PRÓPRIO pagador (privada; nunca devolvida).</summary>
    public Guid? AccountId { get; init; }

    /// <summary>
    /// Somente na EDIÇÃO (PUT): sem <c>AccountId</c>, a conta privada atual é mantida; com <c>ClearAccount = true</c> ela é desvinculada.
    /// Ignorado na criação.
    /// </summary>
    public bool ClearAccount { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }
}

/// <summary>Recorrência de grupo (D-049): todas as ocorrências são criadas junto, de forma atômica.</summary>
public sealed class GroupRecurrenceRequest
{
    public RecurrenceFrequency? Frequency { get; init; }

    public int Occurrences { get; init; }
}

public sealed class CreateGroupTransactionRequest
{
    public string Description { get; init; } = string.Empty;

    /// <summary>Opcional e ignorado, exceto <c>Income</c> (→ 422): grupos só têm despesas (D-046).</summary>
    public TransactionType? Type { get; init; }

    public decimal Amount { get; init; }

    public Guid CategoryId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    /// <summary><c>Planned</c> ou <c>Pending</c> (padrão <c>Pending</c>).</summary>
    public TransactionStatus? Status { get; init; }

    public DateOnly? TransactionDate { get; init; }

    /// <summary>Livre: pode ser anterior a hoje e à data da movimentação (D-047).</summary>
    public DateOnly? DueDate { get; init; }

    public string? Notes { get; init; }

    /// <summary>"Já paga": o 1º pagamento do criador é gravado junto (D-047).</summary>
    public GroupPaymentRequest? Payment { get; init; }

    public GroupRecurrenceRequest? Recurrence { get; init; }
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

    /// <summary>Inclui subcategorias.</summary>
    public Guid? CategoryId { get; init; }

    public TransactionStatus? Status { get; init; }

    public string? Search { get; init; }

    public Guid? CreatedByUserId { get; init; }

    /// <summary>Movimentações com ao menos um pagamento deste usuário.</summary>
    public Guid? PaidByUserId { get; init; }
}
