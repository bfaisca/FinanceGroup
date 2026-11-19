namespace FinanceApp.Domain.Transactions;

public enum TransactionType
{
    Expense,
    Income,
}

/// <summary>
/// Estados da movimentação (docs/STATE_MACHINE.md). <see cref="Overdue"/> nunca é persistido:
/// é calculado na leitura por <see cref="Transaction.GetPresentedStatus"/> (D-009).
/// </summary>
public enum TransactionStatus
{
    Planned,
    Pending,
    Paid,
    Received,
    Overdue,
    Canceled,
}

public enum PaymentMethod
{
    Cash,
    DebitCard,
    CreditCard,
    Pix,
    BankTransfer,
    BankSlip,
    Other,
}

public enum TransactionAuditAction
{
    Created,
    Updated,
    PaymentAdded,
    PaymentRemoved,
    Canceled,
    Reopened,
}
