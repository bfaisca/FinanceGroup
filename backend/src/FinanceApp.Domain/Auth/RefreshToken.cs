using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Auth;

/// <summary>
/// Refresh token opaco. Somente o hash SHA-256 é persistido. Tokens da mesma sessão
/// compartilham <see cref="FamilyId"/>, permitindo revogar a cadeia inteira em caso de reutilização.
/// </summary>
public sealed class RefreshToken
{
    public const int TokenHashMaxLength = 128;
    public const int IpMaxLength = 64;

    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public Guid FamilyId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public RefreshTokenRevocationReason? RevocationReason { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Indica que o token foi trocado por outro na rotação (reutilizá-lo é suspeito).</summary>
    public bool WasRotated => RevocationReason == RefreshTokenRevocationReason.Rotated;

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        string? createdByIp)
    {
        var utcNow = Guard.Utc(now);
        var utcExpires = Guard.Utc(expiresAt);
        if (utcExpires <= utcNow)
        {
            throw new DomainException("A expiração do token deve ser posterior à criação.");
        }

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guard.NotEmpty(userId, "O usuário do token é obrigatório."),
            TokenHash = Guard.NotBlank(tokenHash, "O hash do token é obrigatório."),
            FamilyId = Guard.NotEmpty(familyId, "A família do token é obrigatória."),
            ExpiresAt = utcExpires,
            CreatedAt = utcNow,
            CreatedByIp = Truncate(createdByIp, IpMaxLength),
        };
    }

    public bool IsExpired(DateTimeOffset now) => Guard.Utc(now) >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>Revoga o token. Idempotente: um token já revogado mantém o motivo original.</summary>
    public void Revoke(DateTimeOffset now, RefreshTokenRevocationReason reason, Guid? replacedByTokenId = null)
    {
        if (IsRevoked)
        {
            return;
        }

        if (reason == RefreshTokenRevocationReason.Rotated && replacedByTokenId is null)
        {
            throw new DomainException("A rotação exige o token substituto.");
        }

        RevokedAt = Guard.Utc(now);
        RevocationReason = reason;
        ReplacedByTokenId = replacedByTokenId;
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
