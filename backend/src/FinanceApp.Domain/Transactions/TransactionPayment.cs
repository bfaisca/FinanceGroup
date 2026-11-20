using FinanceApp.Domain.Accounts;

namespace FinanceApp.Domain.Transactions;

/// <summary>Pagamento (despesa) ou recebimento (receita) associado a uma movimentação. Parcial ou integral.</summary>
public sealed class TransactionPayment
{
    private TransactionPayment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TransactionId { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid? AccountId { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Account? Account { get; private set; }

    /// <summary>Desvincula a conta do pagador (exclusão da conta do usuário, D-036): o pagamento de grupo permanece, sem conta.</summary>
    public void DetachAccount()
    {
        AccountId = null;
        Account = null;
    }

    internal static TransactionPayment Create(
        Guid transactionId,
        decimal amount,
        DateOnly paymentDate,
        Guid createdByUserId,
        Guid? accountId,
        PaymentMethod? paymentMethod,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            Amount = amount,
            PaymentDate = paymentDate,
            CreatedByUserId = createdByUserId,
            AccountId = accountId,
            PaymentMethod = paymentMethod,
            CreatedAt = now.ToUniversalTime(),
        };
}
