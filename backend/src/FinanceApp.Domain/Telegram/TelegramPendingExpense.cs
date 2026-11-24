using FinanceApp.Domain.Common;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Domain.Telegram;

/// <summary>Despesa digitada no bot que aguarda a escolha da categoria (D-054). Curta duração; nunca vira registro financeiro sozinha.</summary>
public sealed class TelegramPendingExpense
{
    private TelegramPendingExpense()
    {
        Description = string.Empty;
    }

    /// <summary>Numérico e curto: cabe no <c>callback_data</c> do Telegram (64 bytes).</summary>
    public long Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Description { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Preenchido quando a despesa vai para um grupo (D-056); nulo = despesa individual.</summary>
    public Guid? GroupId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TelegramPendingExpense Create(Guid userId, string description, decimal amount, DateTimeOffset now, Guid? groupId = null)
    {
        if (amount <= 0)
        {
            throw new DomainException("O valor deve ser maior que zero.");
        }

        var text = Guard.NotBlank(description, "A descrição é obrigatória.").Trim();
        if (text.Length > Transaction.DescriptionMaxLength)
        {
            throw new DomainException($"A descrição deve ter no máximo {Transaction.DescriptionMaxLength} caracteres.");
        }

        return new TelegramPendingExpense
        {
            UserId = Guard.NotEmpty(userId, "O usuário é obrigatório."),
            Description = text,
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            GroupId = groupId,
            CreatedAt = Guard.Utc(now),
        };
    }
}
