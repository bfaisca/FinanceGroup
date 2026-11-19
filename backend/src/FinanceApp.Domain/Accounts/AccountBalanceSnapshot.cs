using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Accounts;

/// <summary>
/// Saldo informado pelo usuário em uma data. Imutável: nunca é editado nem apagado (D-019);
/// um novo snapshot passa a ser a referência do saldo projetado.
/// </summary>
public sealed class AccountBalanceSnapshot
{
    private AccountBalanceSnapshot()
    {
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public decimal Balance { get; private set; }

    public DateOnly ReferenceDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static AccountBalanceSnapshot Create(Guid accountId, decimal balance, DateOnly referenceDate, DateTimeOffset now)
    {
        if (!MoneyRules.HasValidScale(balance))
        {
            throw new DomainException("O saldo deve ter no máximo 2 casas decimais.");
        }

        if (!MoneyRules.IsWithinRange(balance))
        {
            throw new DomainException("O saldo informado excede o limite permitido.");
        }

        return new AccountBalanceSnapshot
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Balance = balance,
            ReferenceDate = referenceDate,
            CreatedAt = Guard.Utc(now),
        };
    }
}
