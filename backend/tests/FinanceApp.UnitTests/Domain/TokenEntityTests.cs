using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Common;

namespace FinanceApp.UnitTests.Domain;

public class TokenEntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RefreshToken_IsActiveUntilExpiry()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Guid.NewGuid(), Now.AddDays(7), Now, "127.0.0.1");

        Assert.True(token.IsActive(Now));
        Assert.True(token.IsActive(Now.AddDays(7).AddSeconds(-1)));
        Assert.False(token.IsActive(Now.AddDays(7)));
    }

    [Fact]
    public void RefreshToken_Rotation_RequiresReplacementAndIsIdempotent()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Guid.NewGuid(), Now.AddDays(7), Now, null);

        Assert.Throws<DomainException>(() => token.Revoke(Now, RefreshTokenRevocationReason.Rotated));

        var replacement = Guid.NewGuid();
        token.Revoke(Now, RefreshTokenRevocationReason.Rotated, replacement);
        token.Revoke(Now.AddMinutes(1), RefreshTokenRevocationReason.Logout);

        Assert.True(token.WasRotated);
        Assert.Equal(replacement, token.ReplacedByTokenId);
        Assert.Equal(Now, token.RevokedAt);
        Assert.False(token.IsActive(Now));
    }

    [Fact]
    public void RefreshToken_ExpiryMustBeInFuture()
    {
        Assert.Throws<DomainException>(() => RefreshToken.Create(Guid.NewGuid(), "hash", Guid.NewGuid(), Now, Now, null));
    }

    [Fact]
    public void PasswordResetToken_IsSingleUse()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now.AddHours(1), Now);

        Assert.True(token.IsValid(Now));
        token.MarkAsUsed(Now.AddMinutes(1));

        Assert.False(token.IsValid(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => token.MarkAsUsed(Now.AddMinutes(2)));
    }

    [Fact]
    public void PasswordResetToken_Expires()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now.AddHours(1), Now);

        Assert.False(token.IsValid(Now.AddHours(1)));
        Assert.Throws<DomainException>(() => token.MarkAsUsed(Now.AddHours(2)));
    }
}
