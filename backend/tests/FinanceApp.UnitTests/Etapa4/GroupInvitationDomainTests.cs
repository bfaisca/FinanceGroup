using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa4;

public class GroupInvitationDomainTests
{
    private static readonly Guid GroupId = Guid.NewGuid();
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static GroupInvitation New(string email = " Ana@Exemplo.com ") =>
        GroupInvitation.Create(GroupId, UserId, email, Hash, Now);

    [Fact]
    public void Create_NormalizesEmail_StoresOnlyTheHash_AndExpiresInSevenDays()
    {
        var invitation = New();

        Assert.Equal("ana@exemplo.com", invitation.Email);
        Assert.Equal(Hash, invitation.TokenHash);
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAt);
        Assert.Equal(GroupInvitationStatus.Pending, invitation.Status);
        Assert.Equal(GroupInvitationStatus.Pending, invitation.GetEffectiveStatus(Now.AddDays(7).AddTicks(-1)));
    }

    [Fact]
    public void ExpirationIsDerived_AndCanBePersisted()
    {
        var invitation = New();
        var after = Now.AddDays(7);

        Assert.Equal(GroupInvitationStatus.Pending, invitation.Status);
        Assert.Equal(GroupInvitationStatus.Expired, invitation.GetEffectiveStatus(after));
        Assert.False(invitation.MarkExpiredIfDue(Now.AddDays(1)));
        Assert.True(invitation.MarkExpiredIfDue(after));
        Assert.Equal(GroupInvitationStatus.Expired, invitation.Status);
        Assert.False(invitation.MarkExpiredIfDue(after));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_RejectsBlankTokenHash(string hash) =>
        Assert.Throws<DomainException>(() => GroupInvitation.Create(GroupId, UserId, "a@b.com", hash, Now));

    [Fact]
    public void Accept_WithTheInvitedEmail_IgnoringCase_Works()
    {
        var invitation = New();

        invitation.Accept("ANA@exemplo.COM", Now.AddDays(1));

        Assert.Equal(GroupInvitationStatus.Accepted, invitation.Status);
        Assert.Equal(Now.AddDays(1), invitation.RespondedAt);
    }

    [Fact]
    public void Accept_WithAnotherEmail_IsForbidden_AndKeepsPending()
    {
        var invitation = New();

        Assert.Throws<PermissionDeniedException>(() => invitation.Accept("outra@exemplo.com", Now));
        Assert.Throws<PermissionDeniedException>(() => invitation.Decline("outra@exemplo.com", Now));
        Assert.Equal(GroupInvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Accept_Expired_IsRejected()
    {
        var invitation = New();

        var ex = Assert.Throws<DomainException>(() => invitation.Accept("ana@exemplo.com", Now.AddDays(7)));
        Assert.Contains("expirou", ex.Message);
        Assert.Equal(GroupInvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Accept_Twice_IsRejected_AsAlreadyUsed()
    {
        var invitation = New();
        invitation.Accept("ana@exemplo.com", Now);

        var ex = Assert.Throws<DomainException>(() => invitation.Accept("ana@exemplo.com", Now));
        Assert.Contains("já foi utilizado", ex.Message);
        Assert.Throws<DomainException>(() => invitation.Decline("ana@exemplo.com", Now));
    }

    [Fact]
    public void Accept_Revoked_IsRejected()
    {
        var invitation = New();
        invitation.Revoke(Now);

        var ex = Assert.Throws<DomainException>(() => invitation.Accept("ana@exemplo.com", Now));
        Assert.Contains("revogado", ex.Message);
    }

    [Fact]
    public void Decline_ThenAccept_IsRejected()
    {
        var invitation = New();
        invitation.Decline("ana@exemplo.com", Now);

        Assert.Equal(GroupInvitationStatus.Declined, invitation.Status);
        Assert.Throws<DomainException>(() => invitation.Accept("ana@exemplo.com", Now));
    }

    [Fact]
    public void Revoke_OnlyPendingAndValid()
    {
        var accepted = New();
        accepted.Accept("ana@exemplo.com", Now);
        var expired = New();

        Assert.Throws<DomainException>(() => accepted.Revoke(Now));
        Assert.Throws<DomainException>(() => expired.Revoke(Now.AddDays(8)));
    }

    [Fact]
    public void Renew_ReplacesTheHash_AndRestartsTheValidity()
    {
        var invitation = New();
        const string newHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

        invitation.Renew(newHash, Now.AddDays(8));

        Assert.Equal(newHash, invitation.TokenHash);
        Assert.Equal(Now.AddDays(15), invitation.ExpiresAt);
        Assert.Equal(GroupInvitationStatus.Pending, invitation.GetEffectiveStatus(Now.AddDays(9)));
    }

    [Fact]
    public void Renew_ExpiredPersisted_ReturnsToPending_ButAcceptedOrRevokedDoNot()
    {
        var expired = New();
        expired.MarkExpiredIfDue(Now.AddDays(8));
        expired.Renew(Hash, Now.AddDays(9));
        Assert.Equal(GroupInvitationStatus.Pending, expired.Status);

        var accepted = New();
        accepted.Accept("ana@exemplo.com", Now);
        var revoked = New();
        revoked.Revoke(Now);
        Assert.Throws<DomainException>(() => accepted.Renew(Hash, Now));
        Assert.Throws<DomainException>(() => revoked.Renew(Hash, Now));
    }

    [Fact]
    public void RevokeIfPending_IsSilentForNonPending()
    {
        var accepted = New();
        accepted.Accept("ana@exemplo.com", Now);
        var pending = New();

        accepted.RevokeIfPending(Now);
        pending.RevokeIfPending(Now);

        Assert.Equal(GroupInvitationStatus.Accepted, accepted.Status);
        Assert.Equal(GroupInvitationStatus.Revoked, pending.Status);
    }
}
