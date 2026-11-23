using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Telegram;

/// <summary>Código de vínculo de uso único e curta duração (D-053). Somente o hash é persistido.</summary>
public sealed class TelegramLinkCode
{
    public const int HashMaxLength = 64;

    private TelegramLinkCode()
    {
        CodeHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TelegramLinkCode Create(Guid userId, string codeHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        var utcExpires = Guard.Utc(expiresAt);
        if (utcExpires <= utcNow)
        {
            throw new DomainException("A expiração do código deve ser posterior à criação.");
        }

        return new TelegramLinkCode
        {
            Id = Guid.NewGuid(),
            UserId = Guard.NotEmpty(userId, "O usuário do código é obrigatório."),
            CodeHash = Guard.NotBlank(codeHash, "O hash do código é obrigatório."),
            ExpiresAt = utcExpires,
            CreatedAt = utcNow,
        };
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
