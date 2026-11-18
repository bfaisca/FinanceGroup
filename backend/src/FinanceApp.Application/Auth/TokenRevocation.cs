using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Auth;

internal static class TokenRevocation
{
    /// <summary>Revoga (rastreado, salvo no próximo SaveChanges) os refresh tokens ativos do usuário.</summary>
    public static async Task<int> RevokeActiveRefreshTokensAsync(
        this IAppDbContext db,
        Guid userId,
        DateTimeOffset now,
        RefreshTokenRevocationReason reason,
        CancellationToken cancellationToken,
        Guid? exceptFamilyId = null)
    {
        var query = db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now);
        if (exceptFamilyId is { } keep)
        {
            query = query.Where(t => t.FamilyId != keep);
        }

        var tokens = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var token in tokens)
        {
            token.Revoke(now, reason);
        }

        return tokens.Count;
    }

    /// <summary>Remove tokens de redefinição ainda não utilizados (invalidação).</summary>
    public static async Task RemoveUnusedPasswordResetTokensAsync(
        this IAppDbContext db,
        Guid userId,
        CancellationToken cancellationToken,
        Guid? exceptTokenId = null)
    {
        var tokens = await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && (exceptTokenId == null || t.Id != exceptTokenId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.PasswordResetTokens.RemoveRange(tokens);
    }
}
