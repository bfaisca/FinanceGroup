using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Auth;

/// <summary>Token de redefinição de senha de uso único. Somente o hash é persistido.</summary>
public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsUsed => UsedAt is not null;

    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        var utcExpires = Guard.Utc(expiresAt);
        if (utcExpires <= utcNow)
        {
            throw new DomainException("A expiração do token deve ser posterior à criação.");
        }

        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = Guard.NotEmpty(userId, "O usuário do token é obrigatório."),
            TokenHash = Guard.NotBlank(tokenHash, "O hash do token é obrigatório."),
            ExpiresAt = utcExpires,
            CreatedAt = utcNow,
        };
    }

    public bool IsValid(DateTimeOffset now) => !IsUsed && Guard.Utc(now) < ExpiresAt;

    public void MarkAsUsed(DateTimeOffset now)
    {
        if (!IsValid(now))
        {
            throw new DomainException("O token de redefinição é inválido ou expirou.");
        }

        UsedAt = Guard.Utc(now);
    }
}
