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

    /// <summary>Principal aplicado à movimentação (só ele conta para a situação da conta, D-048).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Juros/multa pagos além do principal (≥ 0; só em pagamentos de grupo, D-048).</summary>
    public decimal InterestAmount { get; private set; }

    /// <summary>Principal + juros: o valor efetivamente pago.</summary>
    public decimal TotalAmount => Amount + InterestAmount;

    public DateOnly PaymentDate { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid? AccountId { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

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
        DateTimeOffset now,
        decimal interestAmount = 0m) =>
        new()
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            Amount = amount,
            InterestAmount = interestAmount,
            PaymentDate = paymentDate,
            CreatedByUserId = createdByUserId,
            AccountId = accountId,
            PaymentMethod = paymentMethod,
            CreatedAt = now.ToUniversalTime(),
            UpdatedAt = now.ToUniversalTime(),
        };

    internal void Apply(decimal amount, decimal interestAmount, DateOnly paymentDate, Guid? accountId, PaymentMethod? paymentMethod, DateTimeOffset now)
    {
        Amount = amount;
        InterestAmount = interestAmount;
        PaymentDate = paymentDate;
        AccountId = accountId;
        PaymentMethod = paymentMethod;
        UpdatedAt = now.ToUniversalTime();
    }
}
