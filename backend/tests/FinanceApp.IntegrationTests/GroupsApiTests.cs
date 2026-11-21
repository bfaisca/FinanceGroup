using System.Net;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Groups;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GroupsApiTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    private async Task<(ApiUser Owner, ApiUser Admin, ApiUser Member, GroupDetailDto Group)> NewGroupWithRolesAsync()
    {
        var owner = await h.NewUserAsync("Maria Dona");
        var admin = await h.NewUserAsync("Ana Admin");
        var member = await h.NewUserAsync("Bia Membro");
        var group = await h.CreateGroupAsync(owner, "Casa");
        await h.JoinAsync(owner, group.Id, admin);
        await h.JoinAsync(owner, group.Id, member);
        var adminRow = await h.MemberOfAsync(owner, group.Id, admin);
        var response = await owner.Put($"/api/v1/groups/{group.Id}/members/{adminRow.Id}/role", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (owner, admin, member, group);
    }

    [Fact]
    public async Task Create_MakesTheCreatorOwner_AndTheGroupAppearsInTheList()
    {
        var owner = await h.NewUserAsync("Maria Dona");

        var response = await owner.Post("/api/v1/groups", new { name = "  Casa  ", description = "Contas da casa" });
        var raw = await GroupSupport.RawAsync(response, HttpStatusCode.Created);
        GroupSupport.AssertNoPrivateData(raw, owner.Session.Email);
        var group = System.Text.Json.JsonSerializer.Deserialize<GroupDetailDto>(raw, ApiUser.Json)!;

        Assert.Equal("Casa", group.Name);
        Assert.Equal("Contas da casa", group.Description);
        Assert.Equal(GroupMemberRole.Owner, group.MyRole);
        Assert.Equal(1, group.MemberCount);
        Assert.True(group.IsActive);
        Assert.Equal("Maria Dona", group.CreatedByName);
        Assert.Equal(new GroupPermissionsDto(true, true, true, true, false, true), group.Permissions);
        var member = Assert.Single(group.Members);
        Assert.Equal((owner.Id, "Maria Dona", GroupMemberRole.Owner, GroupMemberStatus.Active, true), (member.UserId, member.Name, member.Role, member.Status, member.IsMe));
        Assert.EndsWith($"/api/v1/groups/{group.Id}", response.Headers.Location!.AbsolutePath, StringComparison.Ordinal);

        var list = await owner.GetOk<List<GroupSummaryDto>>("/api/v1/groups");
        var summary = Assert.Single(list);
        Assert.Equal((group.Id, GroupMemberRole.Owner, 1, true), (summary.Id, summary.MyRole, summary.MemberCount, summary.IsActive));
    }

    [Fact]
    public async Task Create_WithInvalidData_Returns400WithFieldErrors()
    {
        var owner = await h.NewUserAsync();

        var blank = await ReadProblemAsync(await owner.Post("/api/v1/groups", new { name = "  " }), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(blank.HasError("name"));
        var long1 = await ReadProblemAsync(await owner.Post("/api/v1/groups", new { name = new string('a', 101), description = new string('b', 501) }), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(long1.HasError("name"));
        Assert.True(long1.HasError("description"));
    }

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var anonymous = h.Factory.CreateApiClient();
        foreach (var url in new[] { "/api/v1/groups", $"/api/v1/groups/{Guid.NewGuid()}", $"/api/v1/groups/{Guid.NewGuid()}/transactions", "/api/v1/invitations/abc" })
        {
            await ReadProblemAsync(await anonymous.GetAsync(url), HttpStatusCode.Unauthorized, "unauthorized");
        }
    }

    [Fact]
    public async Task List_IsAlphabetical_AndHidesArchivedUnlessRequested()
    {
        var owner = await h.NewUserAsync();
        await h.CreateGroupAsync(owner, "viagem");
        await h.CreateGroupAsync(owner, "Casa");
        var old = await h.CreateGroupAsync(owner, "Antigo");
        Assert.Equal(HttpStatusCode.OK, (await owner.Post($"/api/v1/groups/{old.Id}/archive")).StatusCode);
        var other = await h.NewUserAsync();
        await h.CreateGroupAsync(other, "De outro usuário");

        var visible = await owner.GetOk<List<GroupSummaryDto>>("/api/v1/groups");
        var all = await owner.GetOk<List<GroupSummaryDto>>("/api/v1/groups?includeArchived=true");

        Assert.Equal(new[] { "Casa", "viagem" }, visible.Select(g => g.Name).ToArray());
        Assert.Equal(new[] { "Antigo", "Casa", "viagem" }, all.Select(g => g.Name).ToArray());
        Assert.False(all.First().IsActive);
    }

    [Fact]
    public async Task NonMember_GetsNotFound_OnEveryGroupRoute()
    {
        var (owner, _, _, group) = await NewGroupWithRolesAsync();
        var tx = await h.CreateGroupTxAsync(owner, group.Id, "Mercado", 50m);
        var stranger = await h.NewUserAsync("Estranho");
        var gid = group.Id;
        var someId = Guid.NewGuid();
        var category = await h.CategoryIdAsync(stranger, "Alimentação");

        var calls = new List<(string Name, HttpMethod Method, string Url, object? Body)>
        {
            ("get", HttpMethod.Get, $"/api/v1/groups/{gid}", null),
            ("update", HttpMethod.Put, $"/api/v1/groups/{gid}", new { name = "Invadido" }),
            ("archive", HttpMethod.Post, $"/api/v1/groups/{gid}/archive", null),
            ("unarchive", HttpMethod.Post, $"/api/v1/groups/{gid}/unarchive", null),
            ("leave", HttpMethod.Post, $"/api/v1/groups/{gid}/leave", null),
            ("members", HttpMethod.Get, $"/api/v1/groups/{gid}/members?includeFormer=true", null),
            ("role", HttpMethod.Put, $"/api/v1/groups/{gid}/members/{someId}/role", new { role = "Admin" }),
            ("remove", HttpMethod.Delete, $"/api/v1/groups/{gid}/members/{someId}", null),
            ("invitations", HttpMethod.Get, $"/api/v1/groups/{gid}/invitations", null),
            ("invite", HttpMethod.Post, $"/api/v1/groups/{gid}/invitations", new { email = "x@exemplo.com" }),
            ("resend", HttpMethod.Post, $"/api/v1/groups/{gid}/invitations/{someId}/resend", null),
            ("revoke", HttpMethod.Delete, $"/api/v1/groups/{gid}/invitations/{someId}", null),
            ("dashboard", HttpMethod.Get, $"/api/v1/groups/{gid}/dashboard?from=2026-01-01&to=2026-12-31", null),
            ("tx-list", HttpMethod.Get, $"/api/v1/groups/{gid}/transactions", null),
            ("tx-create", HttpMethod.Post, $"/api/v1/groups/{gid}/transactions", GroupSupport.GroupTxBody("Invasão", 10m, category, h.Today)),
            ("tx-get", HttpMethod.Get, $"/api/v1/groups/{gid}/transactions/{tx.Id}", null),
            ("tx-put", HttpMethod.Put, $"/api/v1/groups/{gid}/transactions/{tx.Id}", GroupSupport.GroupTxBody("Invasão", 10m, category, h.Today)),
            ("tx-delete", HttpMethod.Delete, $"/api/v1/groups/{gid}/transactions/{tx.Id}", null),
            ("tx-cancel", HttpMethod.Post, $"/api/v1/groups/{gid}/transactions/{tx.Id}/cancel", null),
            ("tx-reopen", HttpMethod.Post, $"/api/v1/groups/{gid}/transactions/{tx.Id}/reopen", null),
            ("tx-history", HttpMethod.Get, $"/api/v1/groups/{gid}/transactions/{tx.Id}/history", null),
            ("tx-pay", HttpMethod.Post, $"/api/v1/groups/{gid}/transactions/{tx.Id}/payments", new { amount = 1m, paymentDate = h.Today }),
            ("tx-unpay", HttpMethod.Delete, $"/api/v1/groups/{gid}/transactions/{tx.Id}/payments/{someId}", null),
        };

        foreach (var (name, method, url, body) in calls)
        {
            var response = await stranger.SendAsync(method, url, body);
            var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound, "not_found");
            Assert.True(problem.ValueKind == System.Text.Json.JsonValueKind.Object, name);
        }

        Assert.Empty(await stranger.GetOk<List<GroupSummaryDto>>("/api/v1/groups?includeArchived=true"));
        // Nada foi alterado.
        var seenByOwner = await owner.GetOk<GroupDetailDto>($"/api/v1/groups/{gid}");
        Assert.Equal("Casa", seenByOwner.Name);
        Assert.Equal(3, seenByOwner.MemberCount);
    }

    [Fact]
    public async Task Roles_FollowD030()
    {
        var (owner, admin, member, group) = await NewGroupWithRolesAsync();
        var gid = group.Id;
        var ownerRow = await h.MemberOfAsync(owner, gid, owner);
        var adminRow = await h.MemberOfAsync(owner, gid, admin);
        var memberRow = await h.MemberOfAsync(owner, gid, member);
        Assert.Equal(GroupMemberRole.Admin, adminRow.Role);

        // Member: 403 em tudo o que exige gestão.
        await ReadProblemAsync(await member.Put($"/api/v1/groups/{gid}", new { name = "Hack" }), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await member.Post($"/api/v1/groups/{gid}/archive"), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await member.Put($"/api/v1/groups/{gid}/members/{memberRow.Id}/role", new { role = "Admin" }), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await member.Delete($"/api/v1/groups/{gid}/members/{adminRow.Id}"), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await member.Post($"/api/v1/groups/{gid}/invitations", new { email = "x@exemplo.com" }), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await member.Get($"/api/v1/groups/{gid}/invitations"), HttpStatusCode.Forbidden, "forbidden");

        // Admin: edita e promove Member, mas não rebaixa nem remove outro Admin/Owner.
        var edited = await admin.Put($"/api/v1/groups/{gid}", new { name = "Casa nova", description = "d" });
        Assert.Equal("Casa nova", (await ApiUser.Read<GroupDetailDto>(edited)).Name);
        var promoted = await ApiUser.Read<GroupMemberDto>(await admin.Put($"/api/v1/groups/{gid}/members/{memberRow.Id}/role", new { role = "Admin" }));
        Assert.Equal(GroupMemberRole.Admin, promoted.Role);
        await ReadProblemAsync(await admin.Put($"/api/v1/groups/{gid}/members/{memberRow.Id}/role", new { role = "Member" }), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await admin.Delete($"/api/v1/groups/{gid}/members/{memberRow.Id}"), HttpStatusCode.Forbidden, "forbidden");
        var onOwnerRole = await ReadProblemAsync(await admin.Put($"/api/v1/groups/{gid}/members/{ownerRow.Id}/role", new { role = "Member" }), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Contains("proprietário", onOwnerRole.GetProperty("detail").GetString(), StringComparison.Ordinal);
        await ReadProblemAsync(await admin.Delete($"/api/v1/groups/{gid}/members/{ownerRow.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await ReadProblemAsync(await admin.Delete($"/api/v1/groups/{gid}/members/{adminRow.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await ReadProblemAsync(await admin.Put($"/api/v1/groups/{gid}/members/{adminRow.Id}/role", new { role = "Owner" }), HttpStatusCode.BadRequest, "validation_error");

        // Owner: rebaixa e remove Admin.
        var demoted = await ApiUser.Read<GroupMemberDto>(await owner.Put($"/api/v1/groups/{gid}/members/{memberRow.Id}/role", new { role = "Member" }));
        Assert.Equal(GroupMemberRole.Member, demoted.Role);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Delete($"/api/v1/groups/{gid}/members/{adminRow.Id}")).StatusCode);
        await ReadProblemAsync(await owner.Delete($"/api/v1/groups/{gid}/members/{ownerRow.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task RemovedMember_LosesAccess_ButFormerMembersAreListedOnRequest()
    {
        var (owner, _, member, group) = await NewGroupWithRolesAsync();
        var gid = group.Id;
        var memberRow = await h.MemberOfAsync(owner, gid, member);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Delete($"/api/v1/groups/{gid}/members/{memberRow.Id}")).StatusCode);

        await ReadProblemAsync(await member.Get($"/api/v1/groups/{gid}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await member.Get($"/api/v1/groups/{gid}/transactions"), HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await member.GetOk<List<GroupSummaryDto>>("/api/v1/groups"));
        var active = await owner.GetOk<List<GroupMemberDto>>($"/api/v1/groups/{gid}/members");
        Assert.DoesNotContain(active, m => m.UserId == member.Id);
        var former = (await owner.GetOk<List<GroupMemberDto>>($"/api/v1/groups/{gid}/members?includeFormer=true")).Single(m => m.UserId == member.Id);
        Assert.Equal(GroupMemberStatus.Removed, former.Status);
        Assert.NotNull(former.LeftAt);
        // Remover de novo (já não é ativo) → 404.
        await ReadProblemAsync(await owner.Delete($"/api/v1/groups/{gid}/members/{memberRow.Id}"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(2, (await owner.GetOk<GroupDetailDto>($"/api/v1/groups/{gid}")).MemberCount);
    }

    [Fact]
    public async Task Leave_MemberLeavesAndKeepsHistory_OwnerCannot()
    {
        var (owner, _, member, group) = await NewGroupWithRolesAsync();
        var gid = group.Id;
        var tx = await h.CreateGroupTxAsync(member, gid, "Do membro", 80m);
        await h.PayGroupTxAsync(member, gid, tx.Id, 30m);

        var ownerLeave = await ReadProblemAsync(await owner.Post($"/api/v1/groups/{gid}/leave"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Contains("proprietário", ownerLeave.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, (await member.Post($"/api/v1/groups/{gid}/leave")).StatusCode);

        await ReadProblemAsync(await member.Get($"/api/v1/groups/{gid}/transactions/{tx.Id}"), HttpStatusCode.NotFound, "not_found");
        // O histórico permanece para os demais, com o nome de quem saiu.
        var seen = await owner.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{gid}/transactions/{tx.Id}");
        Assert.Equal("Bia Membro", seen.CreatedBy.Name);
        Assert.Equal("Bia Membro", Assert.Single(seen.Payments).PaidBy.Name);
        var former = (await owner.GetOk<List<GroupMemberDto>>($"/api/v1/groups/{gid}/members?includeFormer=true")).Single(m => m.UserId == member.Id);
        Assert.Equal(GroupMemberStatus.Left, former.Status);
        // Sem a criadora ativa, ninguém edita/cancela; os pagamentos continuam possíveis (D-032).
        await ReadProblemAsync(await owner.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/cancel"), HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(HttpStatusCode.Created, (await owner.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/payments", new { amount = 20m, paymentDate = h.Today })).StatusCode);
    }

    [Fact]
    public async Task ArchivedGroup_IsReadOnly_AndHiddenFromTheDefaultList()
    {
        var (owner, _, member, group) = await NewGroupWithRolesAsync();
        var gid = group.Id;
        var tx = await h.CreateGroupTxAsync(owner, gid, "Aluguel", 100m);
        var category = await h.CategoryIdAsync(owner, "Alimentação");

        var archived = await ApiUser.Read<GroupDetailDto>(await owner.Post($"/api/v1/groups/{gid}/archive"));
        Assert.False(archived.IsActive);
        Assert.False(archived.Permissions.CanEditGroup);
        Assert.False(archived.Permissions.CanInvite);
        Assert.False(archived.Permissions.CanCreateTransactions);
        Assert.True(archived.Permissions.CanArchive);

        // Leitura continua.
        Assert.Equal(HttpStatusCode.OK, (await member.Get($"/api/v1/groups/{gid}/transactions/{tx.Id}")).StatusCode);
        Assert.Empty(await owner.GetOk<List<GroupSummaryDto>>("/api/v1/groups"));
        Assert.Single(await owner.GetOk<List<GroupSummaryDto>>("/api/v1/groups?includeArchived=true"));

        // Qualquer alteração → 422.
        async Task AssertUnprocessable(Task<HttpResponseMessage> call) =>
            await ReadProblemAsync(await call, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await AssertUnprocessable(owner.Put($"/api/v1/groups/{gid}", new { name = "Novo" }));
        await AssertUnprocessable(owner.Post($"/api/v1/groups/{gid}/transactions", GroupSupport.GroupTxBody("Nova", 10m, category, h.Today)));
        await AssertUnprocessable(owner.Put($"/api/v1/groups/{gid}/transactions/{tx.Id}", GroupSupport.GroupTxBody("Edit", 100m, category, h.Today)));
        await AssertUnprocessable(owner.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/cancel"));
        await AssertUnprocessable(owner.Delete($"/api/v1/groups/{gid}/transactions/{tx.Id}"));
        await AssertUnprocessable(member.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/payments", new { amount = 10m, paymentDate = h.Today }));
        await AssertUnprocessable(owner.Post($"/api/v1/groups/{gid}/invitations", new { email = "novo@exemplo.com" }));
        await AssertUnprocessable(owner.Archive(gid));
        var memberRow = await h.MemberOfAsync(owner, gid, member);
        await AssertUnprocessable(owner.Delete($"/api/v1/groups/{gid}/members/{memberRow.Id}"));
        await AssertUnprocessable(owner.Put($"/api/v1/groups/{gid}/members/{memberRow.Id}/role", new { role = "Admin" }));

        // Reativar volta ao normal; sair continua possível mesmo arquivado.
        Assert.True((await ApiUser.Read<GroupDetailDto>(await owner.Post($"/api/v1/groups/{gid}/unarchive"))).IsActive);
        Assert.Equal(HttpStatusCode.Created, (await member.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/payments", new { amount = 10m, paymentDate = h.Today })).StatusCode);
        await owner.Post($"/api/v1/groups/{gid}/archive");
        Assert.Equal(HttpStatusCode.NoContent, (await member.Post($"/api/v1/groups/{gid}/leave")).StatusCode);
    }

    [Fact]
    public async Task Update_ChangesNameAndDescription_AndValidates()
    {
        var owner = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner, "Casa", "antiga");

        var updated = await ApiUser.Read<GroupDetailDto>(await owner.Put($"/api/v1/groups/{group.Id}", new { name = "Casa 2", description = (string?)null }));

        Assert.Equal("Casa 2", updated.Name);
        Assert.Null(updated.Description);
        Assert.True(updated.UpdatedAt >= group.UpdatedAt);
        var invalid = await ReadProblemAsync(await owner.Put($"/api/v1/groups/{group.Id}", new { name = "" }), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(invalid.HasError("name"));
    }

    [Fact]
    public async Task MemberAndDetailResponses_NeverContainEmailsOrPrivateData()
    {
        var (owner, admin, member, group) = await NewGroupWithRolesAsync();
        var emails = new[] { owner.Session.Email, admin.Session.Email, member.Session.Email };

        foreach (var viewer in new[] { owner, admin, member })
        {
            GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await viewer.Get($"/api/v1/groups/{group.Id}")), emails);
            GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await viewer.Get($"/api/v1/groups/{group.Id}/members?includeFormer=true")), emails);
            GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await viewer.Get("/api/v1/groups?includeArchived=true")), emails);
        }
    }
}

internal static class GroupApiUserExtensions
{
    public static Task<HttpResponseMessage> Archive(this ApiUser user, Guid groupId) => user.Post($"/api/v1/groups/{groupId}/archive");
}
