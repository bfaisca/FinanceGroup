using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.CreditCards;

/// <summary>
/// Cartão de crédito individual (privado). Não há gestão de faturas no MVP: o cartão só define o ciclo
/// (fechamento/vencimento), o limite e a conta opcional onde a fatura é debitada (D-025).
/// </summary>
public sealed class CreditCard
{
    public const int NameMaxLength = 100;
    public const int InstitutionMaxLength = 100;

    private CreditCard()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Conta associada (opcional): recebe, no saldo projetado, as compras do cartão na data de vencimento da fatura.</summary>
    public Guid? AccountId { get; private set; }

    public string Name { get; private set; }

    public string? Institution { get; private set; }

    public decimal CreditLimit { get; private set; }

    public int ClosingDay { get; private set; }

    public int DueDay { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Accounts.Account? Account { get; private set; }

    public static CreditCard Create(
        Guid userId,
        string name,
        string? institution,
        decimal creditLimit,
        int closingDay,
        int dueDay,
        Accounts.Account? account,
        DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        var card = new CreditCard
        {
            Id = Guid.NewGuid(),
            UserId = Guard.NotEmpty(userId, "O usuário é obrigatório."),
            IsActive = true,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
        card.Apply(name, institution, creditLimit, closingDay, dueDay, account, accountChanged: true);
        return card;
    }

    /// <summary>
    /// Atualiza os dados. Alterar fechamento/vencimento não recalcula compras já feitas (D-025). A conta só é
    /// revalidada (ativa) quando trocada.
    /// </summary>
    public void Update(
        string name,
        string? institution,
        decimal creditLimit,
        int closingDay,
        int dueDay,
        Accounts.Account? account,
        DateTimeOffset now)
    {
        Apply(name, institution, creditLimit, closingDay, dueDay, account, accountChanged: account?.Id != AccountId);
        UpdatedAt = Guard.Utc(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = Guard.Utc(now);
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = Guard.Utc(now);
    }

    /// <summary>Garante que o cartão pode receber novas compras, parcelamentos e recorrências.</summary>
    public void EnsureActive(string message)
    {
        if (!IsActive)
        {
            throw new DomainException(message);
        }
    }

    /// <summary>Fechamento e vencimento da fatura que recebe uma compra feita em <paramref name="purchaseDate"/>.</summary>
    public InvoiceDates GetInvoice(DateOnly purchaseDate) => BillingCycle.Calculate(purchaseDate, ClosingDay, DueDay);

    public DateOnly CalculateDueDate(DateOnly purchaseDate) => GetInvoice(purchaseDate).DueDate;

    private void Apply(
        string name,
        string? institution,
        decimal creditLimit,
        int closingDay,
        int dueDay,
        Accounts.Account? account,
        bool accountChanged)
    {
        var normalizedName = Guard.NotBlank(name, "O nome do cartão é obrigatório.").Trim();
        if (normalizedName.Length > NameMaxLength)
        {
            throw new DomainException($"O nome do cartão deve ter no máximo {NameMaxLength} caracteres.");
        }

        var normalizedInstitution = string.IsNullOrWhiteSpace(institution) ? null : institution.Trim();
        if (normalizedInstitution is { Length: > InstitutionMaxLength })
        {
            throw new DomainException($"A instituição deve ter no máximo {InstitutionMaxLength} caracteres.");
        }

        if (creditLimit < 0)
        {
            throw new DomainException("O limite do cartão não pode ser negativo.");
        }

        if (!MoneyRules.HasValidScale(creditLimit) || !MoneyRules.IsWithinRange(creditLimit))
        {
            throw new DomainException("O limite do cartão é inválido (máximo de 2 casas decimais e dentro do limite permitido).");
        }

        if (closingDay is < BillingCycle.MinDay or > BillingCycle.MaxDay || dueDay is < BillingCycle.MinDay or > BillingCycle.MaxDay)
        {
            throw new DomainException("Os dias de fechamento e vencimento devem estar entre 1 e 31.");
        }

        if (account is not null)
        {
            if (account.UserId != UserId)
            {
                throw new DomainException("A conta associada não pertence ao usuário.");
            }

            if (accountChanged)
            {
                account.EnsureActive("A conta selecionada está desativada.");
            }
        }

        Name = normalizedName;
        Institution = normalizedInstitution;
        CreditLimit = creditLimit;
        ClosingDay = closingDay;
        DueDay = dueDay;
        AccountId = account?.Id;
        Account = account;
    }
}
