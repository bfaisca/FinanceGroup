using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.UnitTests.Etapa4;

public sealed class GroupInvitationServiceTests : IDisposable
{
    private readonly GroupTestKit kit = new();

    public void Dispose() => kit.Dispose();

    private async Task<(FinanceApp.Domain.Users.User Owner, GroupDetailDto Group)> OwnerWithGroupAsync()
    {
        var owner = await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        return (owner, group);
    }

    private Task<GroupInvitationDto> InviteAsync(Guid groupId, string email) =>
        kit.Invitations.CreateAsync(groupId, new CreateInvitationRequest { Email = email }, CancellationToken.None);

    [Fact]
    public async Task Create_StoresOnlyTheHash_SendsTheLink_AndNeverReturnsTheToken()
    {
        var (_, group) = await OwnerWithGroupAsync();

        var dto = await InviteAsync(group.Id, "  Ana@Exemplo.com ");

        Assert.Equal("ana@exemplo.com", dto.Email);
        Assert.Equal(GroupInvitationStatus.Pending, dto.Status);
        Assert.True(dto.EmailSent);
        Assert.Equal("Maria", dto.InvitedByName);
        Assert.Equal(kit.Clock.UtcNow.AddDays(7), dto.ExpiresAt);

        var token = kit.LastToken("ana@exemplo.com");
        Assert.True(token.Length >= 43);
        var stored = await kit.Db.GroupInvitations.AsNoTracking().SingleAsync();
        Assert.Equal(kit.Tokens.Hash(token), stored.TokenHash);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.DoesNotContain(token, System.Text.Json.JsonSerializer.Serialize(dto), StringComparison.Ordinal);

        var message = Assert.Single(kit.Emails.Sent);
        Assert.Contains("convidou você", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("Casa", message.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WhenEmailFails_StillCreatesTheInvitation_WithEmailSentFalse()
    {
        var (_, group) = await OwnerWithGroupAsync();
        kit.Emails.Fail = true;

        var dto = await InviteAsync(group.Id, "ana@exemplo.com");

        Assert.False(dto.EmailSent);
        Assert.Equal(1, await kit.Db.GroupInvitations.CountAsync());

        // O Admin pode reenviar depois.
        kit.Emails.Fail = false;
        var resent = await kit.Invitations.ResendAsync(group.Id, dto.Id, CancellationToken.None);
        Assert.True(resent.EmailSent);
        Assert.Single(kit.Emails.Sent);
    }

    [Fact]
    public async Task Create_SecondPendingInvitationForTheSameEmail_IsAConflict()
    {
        var (_, group) = await OwnerWithGroupAsync();
        await InviteAsync(group.Id, "ana@exemplo.com");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => InviteAsync(group.Id, "ANA@exemplo.com"));

        Assert.Equal("conflict", ex.Code);
    }

    [Fact]
    public async Task Create_ForAnActiveMemberEmail_IsABusinessRuleViolation()
    {
        var (owner, group) = await OwnerWithGroupAsync();
        var ana = await kit.NewUserAsync("Ana");
        await kit.JoinAsync(group.Id, owner, ana);
        kit.As(owner);

        await Assert.ThrowsAsync<BusinessRuleException>(() => InviteAsync(group.Id, "ana@exemplo.com"));
    }

    [Fact]
    public async Task Create_AfterTheFirstExpired_IsAllowed_AndTheOldOneBecomesExpired()
    {
        var (_, group) = await OwnerWithGroupAsync();
        var first = await InviteAsync(group.Id, "ana@exemplo.com");
        kit.Clock.Advance(TimeSpan.FromDays(8));

        var second = await InviteAsync(group.Id, "ana@exemplo.com");

        Assert.NotEqual(first.Id, second.Id);
        var list = await kit.Invitations.ListAsync(group.Id, null, CancellationToken.None);
        Assert.Equal(GroupInvitationStatus.Pending, list.Single(i => i.Id == second.Id).Status);
        Assert.Equal(GroupInvitationStatus.Expired, list.Single(i => i.Id == first.Id).Status);
    }

    [Fact]
    public async Task Create_ByMemberIsForbidden_ByNonMemberIsNotFound_InArchivedGroupIsRejected()
    {
        var (owner, group) = await OwnerWithGroupAsync();
        var member = await kit.NewUserAsync("Bia");
        await kit.JoinAsync(group.Id, owner, member);
        var stranger = await kit.NewUserAsync("Caio");

        kit.As(member);
        await Assert.ThrowsAsync<PermissionDeniedException>(() => InviteAsync(group.Id, "x@exemplo.com"));
        await Assert.ThrowsAsync<ForbiddenException>(() => kit.Invitations.ListAsync(group.Id, null, CancellationToken.None));

        kit.As(stranger);
        await Assert.ThrowsAsync<NotFoundException>(() => InviteAsync(group.Id, "x@exemplo.com"));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Invitations.ListAsync(group.Id, null, CancellationToken.None));

        kit.As(owner);
        await kit.Groups.ArchiveAsync(group.Id, CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() => InviteAsync(group.Id, "x@exemplo.com"));
    }

    [Fact]
    public async Task Preview_ShowsWhetherTheLoggedUserCanAccept()
    {
        var (_, group) = await OwnerWithGroupAsync();
        await InviteAsync(group.Id, "ana@exemplo.com");
        var token = kit.LastToken("ana@exemplo.com");
        var ana = await kit.NewUserAsync("Ana");
        var other = await kit.NewUserAsync("Caio");

        kit.As(ana);
        var forAna = await kit.Invitations.PreviewAsync(token, CancellationToken.None);
        Assert.Equal("Casa", forAna.GroupName);
        Assert.Equal("Maria", forAna.InvitedByName);
        Assert.True(forAna.CanAccept);
        Assert.False(forAna.AlreadyMember);

        kit.As(other);
        var forOther = await kit.Invitations.PreviewAsync(token, CancellationToken.None);
        Assert.False(forOther.CanAccept);
    }

    [Fact]
    public async Task Preview_UnknownToken_IsNotFound()
    {
        await OwnerWithGroupAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => kit.Invitations.PreviewAsync("nao-existe", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Invitations.PreviewAsync(new string('x', 500), CancellationToken.None));
    }

    [Fact]
    public async Task Accept_AddsTheUserAsMember_AndMarksTheInvitationAccepted()
    {
        var (_, group) = await OwnerWithGroupAsync();
        await InviteAsync(group.Id, "ana@exemplo.com");
        var token = kit.LastToken("ana@exemplo.com");
        var ana = await kit.NewUserAsync("Ana");

        var summary = await kit.Invitations.AcceptAsync(token, CancellationToken.None);

        Assert.Equal(group.Id, summary.Id);
        Assert.Equal(GroupMemberRole.Member, summary.MyRole);
        Assert.Equal(2, summary.MemberCount);
        var member = await kit.Db.GroupMembers.AsNoTracking().SingleAsync(m => m.UserId == ana.Id);
        Assert.Equal(GroupMemberStatus.Active, member.Status);
        Assert.Equal(GroupInvitationStatus.Accepted, (await kit.Db.GroupInvitations.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Accept_WithAnotherAccountsEmail_IsForbidden_AndChangesNothing()
    {
        var (_, group) = await OwnerWithGroupAsync();
        await InviteAsync(group.Id, "ana@exemplo.com");
        var token = kit.LastToken("ana@exemplo.com");
        var intruder = await kit.NewUserAsync("Caio");

        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Invitations.AcceptAsync(token, CancellationToken.None));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Invitations.DeclineAsync(token, CancellationToken.None));

        Assert.False(await kit.Db.GroupMembers.AnyAsync(m => m.UserId == intruder.Id));
        Assert.Equal(GroupInvitationStatus.Pending, (await kit.Db.GroupInvitations.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Accept_Expired_IsRejected_AndListedAsExpired()
    {
        var (owner, group) = await OwnerWithGroupAsync();
        await InviteAsync(group.Id, "ana@exemplo.com");
        var token = kit.LastToken("ana@exemplo.com");
        await kit.NewUserAsync("Ana");
        kit.Clock.Advance(TimeSpan.FromDays(7));

        var ex = await Assert.ThrowsAsync<DomainException>(() => kit.Invitations.AcceptAsync(token, CancellationToken.None));
        Assert.Contains("expirou", ex.Message, StringComparison.Ordinal);
        var preview = await kit.Invitations.PreviewAsync(token, CancellationToken.None);
        Assert.Equal(GroupInvitationStatus.Expired, preview.Status);
        Assert.False(preview.CanAccept);

        kit.As(owner);
        var expired = await kit.Invitations.ListAsync(group.Id, GroupInvitationStatus.Expired, CancellationToken.None);
        Assert.Single(expired);
        Assert.Empty(await kit.Invitations.ListAsync(group.Id, GroupInvitationStatus.Pending, CancellationToken.None));
    }

    [Fact]
    public async Task Accept_Twice_IsRejected_AsAlreadyUsed()
    {
        var (_, group) = await OwnerWithGroupAsync();
        await InviteAsync(group.Id, "ana@exemplo.com");
        var token = kit.LastToken("ana@exemplo.com");
        await kit.NewUserAsync("Ana");
        await kit.Invitations.AcceptAsync(token, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() => kit.Invitations.AcceptAsync(token, CancellationToken.None));

        Assert.Contains("já foi utilizado", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, await kit.Db.GroupMembers.CountAsync());
    }

    [Fact]
    public async Task Accept_WhenAlreadyAnActiveMember_IsRejected_AndInvitationStaysPending()
    {
        var (owner, group) = await OwnerWithGroupAsync();
        var ana = await kit.NewUserAsync("Ana");
        // Convite emitido antes de Ana entrar por outro convite.
        kit.As(owner);
        await InviteAsync(group.Id, "ana@exemplo.com");
        var firstToken = kit.LastToken("ana@exemplo.com");
        await kit.Invitations.ResendAsync(group.Id, (await kit.Invitations.ListAsync(group.Id, null, CancellationToken.None)).Single().Id, CancellationToken.None);
        var secondToken = kit.LastToken("ana@exemplo.com");
        kit.As(ana);
        await kit.Invitations.AcceptAsync(secondToken, CancellationToken.None);

        // O token antigo já não vale (reenvio) e o convite foi usado.
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Invitations.AcceptAsync(firstToken, CancellationToken.None));
        var preview = await kit.Invitations.PreviewAsync(secondToken, CancellationToken.None);
        Assert.True(preview.AlreadyMember);
        Assert.False(preview.CanAccept);
    }

    [Fact]
    public async Task Resend_InvalidatesThePreviousToken_AndRenewsTheValidity()
    {
        var (_, group) = await OwnerWithGroupAsync();
        var created = await InviteAsync(group.Id, "ana@exemplo.com");
        var oldToken = kit.LastToken("ana@exemplo.com");
        kit.Clock.Advance(TimeSpan.FromDays(3));

        var resent = await kit.Invitations.ResendAsync(group.Id, created.Id, CancellationToken.None);

        var newToken = kit.LastToken("ana@exemplo.com");
        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(kit.Clock.UtcNow.AddDays(7), resent.ExpiresAt);
        Assert.True(resent.EmailSent);
        await kit.NewUserAsync("Ana");
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Invitations.PreviewAsync(oldToken, CancellationToken.None));
        Assert.Equal("Casa", (await kit.Invitations.PreviewAsync(newToken, CancellationToken.None)).GroupName);
    }

    [Fact]
    public async Task Resend_ExpiredInvitation_Works_ButAcceptedOnesDoNot()
    {
        var (owner, group) = await OwnerWithGroupAsync();
        var created = await InviteAsync(group.Id, "ana@exemplo.com");
        kit.Clock.Advance(TimeSpan.FromDays(10));
        var resent = await kit.Invitations.ResendAsync(group.Id, created.Id, CancellationToken.None);
        Assert.Equal(GroupInvitationStatus.Pending, resent.Status);

        var ana = await kit.NewUserAsync("Ana");
        await kit.Invitations.AcceptAsync(kit.LastToken("ana@exemplo.com"), CancellationToken.None);
        kit.As(owner);
        await Assert.ThrowsAsync<DomainException>(() => kit.Invitations.ResendAsync(group.Id, created.Id, CancellationToken.None));
        Assert.NotNull(ana);
    }

    [Fact]
    public async Task Revoke_BlocksAcceptance_AndOnlyPendingCanBeRevoked()
    {
        var (_, group) = await OwnerWithGroupAsync();
        var created = await InviteAsync(group.Id, "ana@exemplo.com");
        var token = kit.LastToken("ana@exemplo.com");
        await kit.Invitations.RevokeAsync(group.Id, created.Id, CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() => kit.Invitations.RevokeAsync(group.Id, created.Id, CancellationToken.None));
        await kit.NewUserAsync("Ana");
        var ex = await Assert.ThrowsAsync<DomainException>(() => kit.Invitations.AcceptAsync(token, CancellationToken.None));
        Assert.Contains("revogado", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoke_InvitationOfAnotherGroup_IsNotFound()
    {
        var (_, group) = await OwnerWithGroupAsync();
        var other = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Outro" }, CancellationToken.None);
        var invitation = await InviteAsync(other.Id, "ana@exemplo.com");

        await Assert.ThrowsAsync<NotFoundException>(() => kit.Invitations.RevokeAsync(group.Id, invitation.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Decline_MarksDeclined_AndTheInvitationCannotBeAcceptedAfterwards()
    {
        var (_, group) = await OwnerWithGroupAsync();
        await InviteAsync(group.Id, "ana@exemplo.com");
        var token = kit.LastToken("ana@exemplo.com");
        await kit.NewUserAsync("Ana");

        await kit.Invitations.DeclineAsync(token, CancellationToken.None);

        Assert.Equal(GroupInvitationStatus.Declined, (await kit.Db.GroupInvitations.AsNoTracking().SingleAsync()).Status);
        await Assert.ThrowsAsync<DomainException>(() => kit.Invitations.AcceptAsync(token, CancellationToken.None));
    }

    [Fact]
    public async Task FormerMember_InvitedAgain_ReusesTheSameRow()
    {
        var (owner, group) = await OwnerWithGroupAsync();
        var ana = await kit.NewUserAsync("Ana");
        await kit.JoinAsync(group.Id, owner, ana);
        var originalRow = await kit.Db.GroupMembers.AsNoTracking().SingleAsync(m => m.UserId == ana.Id);
        kit.As(ana);
        await kit.Groups.LeaveAsync(group.Id, CancellationToken.None);
        kit.Clock.Advance(TimeSpan.FromDays(2));

        await kit.JoinAsync(group.Id, owner, ana);

        var rows = await kit.Db.GroupMembers.AsNoTracking().Where(m => m.UserId == ana.Id).ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(originalRow.Id, row.Id);
        Assert.Equal(GroupMemberStatus.Active, row.Status);
        Assert.Null(row.LeftAt);
        Assert.Equal(kit.Clock.UtcNow, row.JoinedAt);
        Assert.Equal(2, await kit.Db.GroupInvitations.CountAsync(i => i.Status == GroupInvitationStatus.Accepted));
    }

    [Fact]
    public async Task Accept_IntoAnArchivedGroup_IsRejected()
    {
        var (_, group) = await OwnerWithGroupAsync();
        await InviteAsync(group.Id, "ana@exemplo.com");
        var token = kit.LastToken("ana@exemplo.com");
        await kit.Groups.ArchiveAsync(group.Id, CancellationToken.None);
        await kit.NewUserAsync("Ana");

        await Assert.ThrowsAsync<DomainException>(() => kit.Invitations.AcceptAsync(token, CancellationToken.None));
        Assert.Equal(GroupInvitationStatus.Pending, (await kit.Db.GroupInvitations.AsNoTracking().SingleAsync()).Status);
    }
}
