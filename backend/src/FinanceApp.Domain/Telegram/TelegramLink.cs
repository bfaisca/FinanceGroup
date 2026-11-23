using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Telegram;

/// <summary>Vínculo de um usuário com um chat privado do Telegram (D-053). No máximo um por usuário e um por chat.</summary>
public sealed class TelegramLink
{
    private TelegramLink()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public long ChatId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TelegramLink Create(Guid userId, long chatId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guard.NotEmpty(userId, "O usuário do vínculo é obrigatório."),
        ChatId = chatId,
        CreatedAt = Guard.Utc(now),
    };

    /// <summary>Quem vincula de novo troca o chat anterior.</summary>
    public void MoveToChat(long chatId, DateTimeOffset now)
    {
        ChatId = chatId;
        CreatedAt = Guard.Utc(now);
    }
}
