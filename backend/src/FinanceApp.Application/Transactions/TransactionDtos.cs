using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Transactions;

/// <summary>Referência da parcela dentro do parcelamento (contrato §10).</summary>
public sealed record InstallmentRefDto(Guid PlanId, int Number, int Count);

/// <summary>Movimentação (listagem). <c>Status</c> é o estado APRESENTADO (inclui <c>Overdue</c>).</summary>
public sealed record TransactionDto(
    Guid Id,
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
    Guid? AccountId,
    string? AccountName,
    PaymentMethod? PaymentMethod,
    DateOnly TransactionDate,
    DateOnly? DueDate,
    DateOnly? PaidAt,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? CreditCardId,
    string? CreditCardName,
    InstallmentRefDto? Installment,
    Guid? RecurrenceRuleId);

public sealed record TransactionPaymentDto(
    Guid Id,
    decimal Amount,
    DateOnly PaymentDate,
    Guid? AccountId,
    string? AccountName,
    PaymentMethod? PaymentMethod,
    DateTimeOffset CreatedAt);

/// <summary>Detalhe da movimentação. Repete os campos de <see cref="TransactionDto"/> e inclui os pagamentos.</summary>
public sealed record TransactionDetailDto(
    Guid Id,
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
    Guid? AccountId,
    string? AccountName,
    PaymentMethod? PaymentMethod,
    DateOnly TransactionDate,
    DateOnly? DueDate,
    DateOnly? PaidAt,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? CreditCardId,
    string? CreditCardName,
    InstallmentRefDto? Installment,
    Guid? RecurrenceRuleId,
    IReadOnlyList<TransactionPaymentDto> Payments)
{
    public static TransactionDetailDto From(TransactionDto t, IReadOnlyList<TransactionPaymentDto> payments) =>
        new(
            t.Id, t.Description, t.Type, t.Amount, t.PaidAmount, t.RemainingAmount, t.Status, t.CategoryId, t.CategoryName,
            t.ParentCategoryName, t.CategoryColor, t.CategoryIcon, t.AccountId, t.AccountName, t.PaymentMethod,
            t.TransactionDate, t.DueDate, t.PaidAt, t.Notes, t.CreatedAt, t.UpdatedAt, t.CreditCardId, t.CreditCardName, t.Installment,
            t.RecurrenceRuleId, payments);
}

public sealed record TransactionHistoryEntryDto(
    Guid Id,
    TransactionAuditAction Action,
    string? Field,
    string? OldValue,
    string? NewValue,
    DateTimeOffset CreatedAt);

public sealed class CreateTransactionRequest
{
    public string Description { get; init; } = string.Empty;

    public TransactionType? Type { get; init; }

    public decimal Amount { get; init; }

    public Guid CategoryId { get; init; }

    public Guid? AccountId { get; init; }

    /// <summary>Compra no cartão (somente despesas). Com cartão, <c>accountId</c> e <c>dueDate</c> não podem ser enviados (D-025).</summary>
    public Guid? CreditCardId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    /// <summary><c>Planned</c> ou <c>Pending</c> (padrão <c>Pending</c>).</summary>
    public TransactionStatus? Status { get; init; }

    public DateOnly? TransactionDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public string? Notes { get; init; }
}

/// <summary>Mesmos campos da criação, exceto <c>type</c> (imutável). <c>status</c> é opcional (Planned/Pending).</summary>
public sealed class UpdateTransactionRequest
{
    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid CategoryId { get; init; }

    public Guid? AccountId { get; init; }

    /// <summary>Imutável: omitido/nulo mantém o cartão atual; um valor diferente do atual → 422 (D-025).</summary>
    public Guid? CreditCardId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    public TransactionStatus? Status { get; init; }

    public DateOnly? TransactionDate { get; init; }

    public DateOnly? DueDate { get; init; }

    public string? Notes { get; init; }
}

public sealed class AddPaymentRequest
{
    public decimal Amount { get; init; }

    public DateOnly? PaymentDate { get; init; }

    public Guid? AccountId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }
}

/// <summary>Parâmetros de <c>GET /transactions</c> (contrato §8).</summary>
public sealed class TransactionListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const string DefaultSort = "-transactionDate";

    public static readonly IReadOnlyList<string> SortFields = ["transactionDate", "dueDate", "amount", "description", "createdAt"];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public string? Sort { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public DateOnly? DueFrom { get; init; }

    public DateOnly? DueTo { get; init; }

    public TransactionType? Type { get; init; }

    public Guid? CategoryId { get; init; }

    /// <summary>Inclui também compras de cartões associados à conta (conta efetiva, D-025).</summary>
    public Guid? AccountId { get; init; }

    public Guid? CreditCardId { get; init; }

    public Guid? InstallmentPlanId { get; init; }

    public Guid? RecurrenceRuleId { get; init; }

    public TransactionStatus? Status { get; init; }

    public string? Search { get; init; }
}
