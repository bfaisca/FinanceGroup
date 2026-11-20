using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Domain.Installments;

/// <summary>
/// Planejamento de uma compra parcelada (D-026). Cada parcela é uma <see cref="Transaction"/> independente.
/// O plano é imutável depois de criado: só se cancela (parcelas sem pagamento) ou se exclui (sem nenhum pagamento).
/// </summary>
public sealed class InstallmentPlan
{
    public const int DescriptionMaxLength = Transaction.DescriptionMaxLength;

    private InstallmentPlan()
    {
        Description = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Description { get; private set; }

    public decimal TotalAmount { get; private set; }

    public int InstallmentCount { get; private set; }

    /// <summary>Data da compra (cartão) ou vencimento da 1ª parcela (sem cartão).</summary>
    public DateOnly FirstDate { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid? AccountId { get; private set; }

    public Guid? CreditCardId { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Category? Category { get; private set; }

    public Account? Account { get; private set; }

    public CreditCard? CreditCard { get; private set; }

    /// <summary>
    /// Cria o plano e todas as suas parcelas (despesas). Com cartão: sem conta, <c>dueDate</c> pelo cálculo da fatura de cada
    /// parcela e forma de pagamento <c>CreditCard</c>; sem cartão: <c>dueDate = transactionDate</c>.
    /// Cada parcela nasce <c>Planned</c> (data futura) ou <c>Pending</c> (data ≤ hoje).
    /// </summary>
    public static (InstallmentPlan Plan, IReadOnlyList<Transaction> Installments) Create(
        Guid userId,
        string description,
        decimal totalAmount,
        int installmentCount,
        DateOnly firstDate,
        Category category,
        Account? account,
        CreditCard? creditCard,
        PaymentMethod? paymentMethod,
        string? notes,
        DateOnly today,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);
        Guard.NotEmpty(userId, "O usuário é obrigatório.");
        var utcNow = Guard.Utc(now);

        var normalizedDescription = Guard.NotBlank(description, "A descrição é obrigatória.").Trim();
        if (normalizedDescription.Length > DescriptionMaxLength)
        {
            throw new DomainException($"A descrição deve ter no máximo {DescriptionMaxLength} caracteres.");
        }

        var amounts = InstallmentCalculator.Distribute(totalAmount, installmentCount);
        var dates = InstallmentCalculator.Dates(firstDate, installmentCount);

        if (creditCard is not null && account is not null)
        {
            throw new DomainException("Compras no cartão não aceitam conta: a conta é a associada ao cartão.");
        }

        var plan = new InstallmentPlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Description = normalizedDescription,
            TotalAmount = totalAmount,
            InstallmentCount = installmentCount,
            FirstDate = firstDate,
            CategoryId = category.Id,
            AccountId = account?.Id,
            CreditCardId = creditCard?.Id,
            PaymentMethod = creditCard is not null ? Transactions.PaymentMethod.CreditCard : paymentMethod,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = utcNow,
        };

        var installments = new List<Transaction>(installmentCount);
        for (var i = 0; i < installmentCount; i++)
        {
            var date = dates[i];
            installments.Add(Transaction.Create(
                userId,
                normalizedDescription,
                TransactionType.Expense,
                amounts[i],
                category,
                account,
                plan.PaymentMethod,
                date > today ? TransactionStatus.Planned : TransactionStatus.Pending,
                date,
                creditCard is null ? date : null,
                plan.Notes,
                utcNow,
                creditCard,
                TransactionOrigin.ForInstallment(plan.Id, i + 1)));
        }

        return (plan, installments);
    }
}
