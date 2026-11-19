namespace FinanceApp.Domain.Transactions;

/// <summary>Linha do histórico de uma movimentação (uma linha por campo alterado).</summary>
public sealed class TransactionAuditEntry
{
    public const int ValueMaxLength = 1000;
    public const int FieldMaxLength = 50;

    private TransactionAuditEntry()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Gerado pelo banco; garante ordem estável entre linhas criadas no mesmo instante.</summary>
    public long Sequence { get; private set; }

    public Guid TransactionId { get; private set; }

    public Guid UserId { get; private set; }

    public TransactionAuditAction Action { get; private set; }

    public string? Field { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static TransactionAuditEntry Create(
        Guid transactionId,
        Guid userId,
        TransactionAuditAction action,
        string? field,
        string? oldValue,
        string? newValue,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            UserId = userId,
            Action = action,
            Field = field,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            CreatedAt = now.ToUniversalTime(),
        };

    private static string? Truncate(string? value) =>
        value is { Length: > ValueMaxLength } ? value[..ValueMaxLength] : value;
}
