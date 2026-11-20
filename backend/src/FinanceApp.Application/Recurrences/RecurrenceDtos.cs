using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Recurrences;

/// <summary>Regra de recorrência (contrato §13, D-027).</summary>
public sealed record RecurrenceDto(
    Guid Id,
    string Description,
    TransactionType Type,
    decimal Amount,
    Guid CategoryId,
    string CategoryName,
    Guid? AccountId,
    string? AccountName,
    Guid? CreditCardId,
    string? CreditCardName,
    PaymentMethod? PaymentMethod,
    string? Notes,
    RecurrenceFrequency Frequency,
    int Interval,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    DateOnly? NextOccurrenceDate,
    int GeneratedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateRecurrenceRequest
{
    public string Description { get; init; } = string.Empty;

    public TransactionType? Type { get; init; }

    public decimal Amount { get; init; }

    public Guid CategoryId { get; init; }

    public Guid? AccountId { get; init; }

    public Guid? CreditCardId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    public string? Notes { get; init; }

    public RecurrenceFrequency? Frequency { get; init; }

    /// <summary>1–365 (padrão 1). Para <c>Custom</c> é o número de dias.</summary>
    public int? Interval { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }
}

/// <summary>Mesmos campos da criação, exceto <c>type</c> e <c>startDate</c> (imutáveis).</summary>
public sealed class UpdateRecurrenceRequest
{
    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid CategoryId { get; init; }

    public Guid? AccountId { get; init; }

    public Guid? CreditCardId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    public string? Notes { get; init; }

    public RecurrenceFrequency? Frequency { get; init; }

    public int? Interval { get; init; }

    public DateOnly? EndDate { get; init; }
}
