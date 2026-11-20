using System.Net;
using System.Net.Http.Json;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Groups;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GroupInvitationsApiTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    [Fact]
    public async Task Create_NeverReturnsTokenOrLink_AndSendsTheEmail()
    {
        var owner = await h.NewUserAsync("Maria Dona");
        var invitee = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner, "Casa");

        var response = await owner.Post($"/api/v1/groups/{group.Id}/invitations", new { email = $"  {invitee.Session.Email.ToUpperInvariant()} " });
        var raw = await GroupSupport.RawAsync(response, HttpStatusCode.Created);
        var token = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.DoesNotContain(token!, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("token", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("link", raw, StringComparison.OrdinalIgnoreCase);
        var dto = System.Text.Json.JsonSerializer.Deserialize<GroupInvitationDto>(raw, ApiUser.Json)!;
        Assert.Equal(GroupInvitationStatus.Pending, dto.Status);
        Assert.Equal(invitee.Session.Email, dto.Email);
        Assert.Equal("Maria Dona", dto.InvitedByName);
        Assert.True(dto.EmailSent);
        Assert.True(dto.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));

        // Persistiu apenas o hash.
        var stored = await h.WithDbAsync(db => Task.FromResult(db.GroupInvitations.Where(i => i.Id == dto.Id).Select(i => i.TokenHash).Single()));
        Assert.NotEqual(token, stored);
        Assert.Equal(64, stored.Length);

        // A listagem (gestores) não traz o token e omite emailSent.
        var listRaw = await GroupSupport.RawAsync(await owner.Get($"/api/v1/groups/{group.Id}/invitations"));
        Assert.DoesNotContain(token!, listRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("emailSent", listRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WhenEmailSendingFails_StillCreatesWithEmailSentFalse()
    {
        var owner = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        h.Factory.Emails.Fail = true;
        try
        {
            var dto = await ApiUser.Read<GroupInvitationDto>(
                await owner.Post($"/api/v1/groups/{group.Id}/invitations", new { email = "falha@exemplo.com" }), HttpStatusCode.Created);

            Assert.False(dto.EmailSent);
            Assert.Equal(GroupInvitationStatus.Pending, dto.Status);
        }
        finally
        {
            h.Factory.Emails.Fail = false;
        }
    }

    [Fact]
    public async Task Create_ValidatesEmail_Duplicates_AndExistingMembers()
    {
        var owner = await h.NewUserAsync();
        var member = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        await h.JoinAsync(owner, group.Id, member);

        var invalid = await ReadProblemAsync(await owner.Post($"/api/v1/groups/{group.Id}/invitations", new { email = "nao-e-email" }), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(invalid.HasError("email"));
        await ReadProblemAsync(await owner.Post($"/api/v1/groups/{group.Id}/invitations", new { email = member.Session.Email }), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        await h.InviteAsync(owner, group.Id, "novo@exemplo.com");
        await ReadProblemAsync(await owner.Post($"/api/v1/groups/{group.Id}/invitations", new { email = "NOVO@exemplo.com" }), HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task Preview_RequiresAuth_AndNeverRevealsTheToken_ForOtherUsers()
    {
        var owner = await h.NewUserAsync("Maria Dona");
        var invitee = await h.NewUserAsync("Bia");
        var other = await h.NewUserAsync("Outra");
        var group = await h.CreateGroupAsync(owner, "Casa");
        await h.InviteAsync(owner, group.Id, invitee.Session.Email);
        var token = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email)!;

        await ReadProblemAsync(await h.Factory.CreateApiClient().GetAsync($"/api/v1/invitations/{token}"), HttpStatusCode.Unauthorized, "unauthorized");
        var preview = await invitee.GetOk<InvitationPreviewDto>($"/api/v1/invitations/{token}");
        Assert.Equal(("Casa", "Maria Dona", true, false, GroupInvitationStatus.Pending), (preview.GroupName, preview.InvitedByName, preview.CanAccept, preview.AlreadyMember, preview.Status));

        // Outro usuário autenticado vê que não pode aceitar (e-mail diferente).
        var foreign = await other.GetOk<InvitationPreviewDto>($"/api/v1/invitations/{token}");
        Assert.False(foreign.CanAccept);
        await ReadProblemAsync(await invitee.Get("/api/v1/invitations/token-inexistente"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Accept_JoinsAsMember_AndTokenCannotBeReused()
    {
        var owner = await h.NewUserAsync();
        var invitee = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner, "Casa");
        await h.InviteAsync(owner, group.Id, invitee.Session.Email);
        var token = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email)!;

        var summary = await ApiUser.Read<GroupSummaryDto>(await invitee.Post($"/api/v1/invitations/{token}/accept"));

        Assert.Equal((group.Id, GroupMemberRole.Member, 2), (summary.Id, summary.MyRole, summary.MemberCount));
        await ReadProblemAsync(await invitee.Post($"/api/v1/invitations/{token}/accept"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        var invitations = await owner.GetOk<List<GroupInvitationDto>>($"/api/v1/groups/{group.Id}/invitations");
        Assert.Equal(GroupInvitationStatus.Accepted, Assert.Single(invitations).Status);
    }

    [Fact]
    public async Task Accept_WithDifferentAccount_Returns403_AndKeepsInvitationPending()
    {
        var owner = await h.NewUserAsync();
        var invitee = await h.NewUserAsync();
        var intruder = await h.NewUserAsync("Intruso");
        var group = await h.CreateGroupAsync(owner);
        await h.InviteAsync(owner, group.Id, invitee.Session.Email);
        var token = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email)!;

        await ReadProblemAsync(await intruder.Post($"/api/v1/invitations/{token}/accept"), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await intruder.Post($"/api/v1/invitations/{token}/decline"), HttpStatusCode.Forbidden, "forbidden");

        Assert.Equal(GroupInvitationStatus.Pending, (await owner.GetOk<List<GroupInvitationDto>>($"/api/v1/groups/{group.Id}/invitations")).Single().Status);
        await ReadProblemAsync(await intruder.Get($"/api/v1/groups/{group.Id}"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Accept_ForAnEmailRegisteredLater_Works()
    {
        var owner = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        var email = $"futuro-{Guid.NewGuid():N}@exemplo.com";
        await h.InviteAsync(owner, group.Id, email);
        var token = h.Factory.Emails.LastInvitationTokenFor(email)!;

        var session = await h.Client.RegisterAsync(name: "Futuro", email: email);
        var newcomer = new ApiUser(h.Client, session);
        var summary = await ApiUser.Read<GroupSummaryDto>(await newcomer.Post($"/api/v1/invitations/{token}/accept"));

        Assert.Equal(group.Id, summary.Id);
    }

    [Fact]
    public async Task Accept_ExpiredOrRevoked_Returns422()
    {
        var owner = await h.NewUserAsync();
        var invitee = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);

        var expired = await h.InviteAsync(owner, group.Id, invitee.Session.Email);
        var expiredToken = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email)!;
        await h.ExpireInvitationAsync(expired.Id);
        var preview = await invitee.GetOk<InvitationPreviewDto>($"/api/v1/invitations/{expiredToken}");
        Assert.Equal(GroupInvitationStatus.Expired, preview.Status);
        Assert.False(preview.CanAccept);
        await ReadProblemAsync(await invitee.Post($"/api/v1/invitations/{expiredToken}/accept"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        // Convite expirado não bloqueia um novo convite para o mesmo e-mail.
        var fresh = await h.InviteAsync(owner, group.Id, invitee.Session.Email);
        var freshToken = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email)!;
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Delete($"/api/v1/groups/{group.Id}/invitations/{fresh.Id}")).StatusCode);
        await ReadProblemAsync(await invitee.Post($"/api/v1/invitations/{freshToken}/accept"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Equal(GroupInvitationStatus.Revoked, (await invitee.GetOk<InvitationPreviewDto>($"/api/v1/invitations/{freshToken}")).Status);
    }

    [Fact]
    public async Task Resend_InvalidatesTheOldToken_AndRenewsExpiry()
    {
        var owner = await h.NewUserAsync();
        var invitee = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        var invitation = await h.InviteAsync(owner, group.Id, invitee.Session.Email);
        var oldToken = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email)!;
        await h.ExpireInvitationAsync(invitation.Id);

        var resent = await ApiUser.Read<GroupInvitationDto>(await owner.Post($"/api/v1/groups/{group.Id}/invitations/{invitation.Id}/resend"));
        var newToken = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email)!;

        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(GroupInvitationStatus.Pending, resent.Status);
        Assert.True(resent.EmailSent);
        Assert.True(resent.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
        await ReadProblemAsync(await invitee.Get($"/api/v1/invitations/{oldToken}"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(HttpStatusCode.OK, (await invitee.Post($"/api/v1/invitations/{newToken}/accept")).StatusCode);
        // Reenviar um convite já aceito → 422.
        await ReadProblemAsync(await owner.Post($"/api/v1/groups/{group.Id}/invitations/{invitation.Id}/resend"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task Decline_MarksDeclined_AndAllowsANewInvitation()
    {
        var owner = await h.NewUserAsync();
        var invitee = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        await h.InviteAsync(owner, group.Id, invitee.Session.Email);
        var token = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email)!;

        Assert.Equal(HttpStatusCode.NoContent, (await invitee.Post($"/api/v1/invitations/{token}/decline")).StatusCode);

        Assert.Equal(GroupInvitationStatus.Declined, (await owner.GetOk<List<GroupInvitationDto>>($"/api/v1/groups/{group.Id}/invitations")).Single().Status);
        await ReadProblemAsync(await invitee.Post($"/api/v1/invitations/{token}/accept"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await h.InviteAsync(owner, group.Id, invitee.Session.Email);
    }

    [Fact]
    public async Task FormerMember_ReturnsThroughANewInvitation_ReusingTheSameMemberRow()
    {
        var owner = await h.NewUserAsync();
        var member = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner);
        await h.JoinAsync(owner, group.Id, member);
        var tx = await h.CreateGroupTxAsync(member, group.Id, "Antiga", 40m);
        var firstRow = await h.MemberOfAsync(owner, group.Id, member);
        await member.Post($"/api/v1/groups/{group.Id}/leave");

        var summary = await h.JoinAsync(owner, group.Id, member);

        Assert.Equal(GroupMemberRole.Member, summary.MyRole);
        var again = await h.MemberOfAsync(owner, group.Id, member, includeFormer: true);
        Assert.Equal((firstRow.Id, GroupMemberStatus.Active, null), (again.Id, again.Status, again.LeftAt));
        // O histórico anterior continua com a mesma autoria e a edição volta a ser dela.
        var seen = await member.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{group.Id}/transactions/{tx.Id}");
        Assert.True(seen.Permissions.CanEdit);
        var membersCount = await h.WithDbAsync(db => Task.FromResult(db.GroupMembers.Count(m => m.GroupId == group.Id && m.UserId == member.Id)));
        Assert.Equal(1, membersCount);
    }

    [Fact]
    public async Task Members_CannotManageInvitations_AndInvitationsOfOtherGroupsAreNotFound()
    {
        var owner = await h.NewUserAsync();
        var member = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        var otherGroup = await h.CreateGroupAsync(owner, "Outro");
        await h.JoinAsync(owner, group.Id, member);
        var invitation = await h.InviteAsync(owner, otherGroup.Id, "x@exemplo.com");

        await ReadProblemAsync(await member.Delete($"/api/v1/groups/{group.Id}/invitations/{invitation.Id}"), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await owner.Delete($"/api/v1/groups/{group.Id}/invitations/{invitation.Id}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await owner.Post($"/api/v1/groups/{group.Id}/invitations/{invitation.Id}/resend"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Preview_AndAccept_AreRateLimited()
    {
        var session = await h.Client.RegisterAsync();

        await using var strict = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:Auth:PermitLimit"] = "3",
            ["RateLimiting:Auth:WindowSeconds"] = "60",
        })));
        using var client = strict.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.Auth.AccessToken);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/invitations/inexistente-{i}")).StatusCode);
        }

        await ReadProblemAsync(await client.GetAsync("/api/v1/invitations/inexistente-4"), HttpStatusCode.TooManyRequests, "too_many_requests");
        await ReadProblemAsync(await client.PostAsJsonAsync("/api/v1/invitations/inexistente-5/accept", new { }), HttpStatusCode.TooManyRequests, "too_many_requests");
    }
}
