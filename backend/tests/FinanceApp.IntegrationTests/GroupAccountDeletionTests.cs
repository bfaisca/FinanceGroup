using System.Net;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Categories;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>D-036/D-014: a exclusão da conta sai dos grupos, transfere a propriedade e preserva o histórico dos grupos.</summary>
[Collection(ApiCollection.Name)]
public class GroupAccountDeletionTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    private static async Task DeleteAsync(ApiUser user) =>
        Assert.Equal(HttpStatusCode.NoContent, (await user.SendAsync(HttpMethod.Delete, "/api/v1/users/me", new { password = DefaultPassword })).StatusCode);

    [Fact]
    public async Task DeletingTheOwner_TransfersToTheOldestAdmin_KeepsGroupHistory_AndRemovesIndividualData()
    {
        var owner = await h.NewUserAsync("Dona");
        var member = await h.NewUserAsync("Membro Antigo");
        var admin = await h.NewUserAsync("Admin");
        var late = await h.NewUserAsync("Admin Tardio");
        var group = await h.CreateGroupAsync(owner, "Casa");
        var gid = group.Id;
        await h.JoinAsync(owner, gid, member);
        await h.JoinAsync(owner, gid, admin);
        await h.JoinAsync(owner, gid, late);
        foreach (var promoted in new[] { late, admin })
        {
            var row = await h.MemberOfAsync(owner, gid, promoted);
            await owner.Put($"/api/v1/groups/{gid}/members/{row.Id}/role", new { role = "Admin" });
        }

        var account = await h.CreateAccountAsync(owner, "Corrente", balance: 100m, referenceDate: h.Today.AddDays(-3));
        var custom = await h.CreateCategoryAsync(owner, "Só da Dona");
        var food = await h.CategoryIdAsync(owner, "Alimentação");
        await h.CreateTxAsync(owner, "Individual", "Expense", 10m, food, h.Today);
        var groupTx = await h.CreateGroupTxAsync(owner, gid, "Do grupo", 300m, categoryId: custom.Id);
        await h.PayGroupTxAsync(owner, gid, groupTx.Id, 100m, accountId: account.Id);
        var pending = await h.InviteAsync(owner, gid, "pendente@exemplo.com");
        var other = await h.CreateGroupAsync(member, "Outro grupo");
        await h.JoinAsync(member, other.Id, owner);

        await DeleteAsync(owner);

        // Propriedade transferida ao Admin mais antigo no grupo (JoinedAt), nao ao promovido primeiro.
        var members = await member.GetOk<List<GroupMemberDto>>($"/api/v1/groups/{gid}/members?includeFormer=true");
        Assert.Equal(GroupMemberRole.Owner, members.Single(m => m.UserId == admin.Id).Role);
        Assert.Equal(GroupMemberRole.Admin, members.Single(m => m.UserId == late.Id).Role);
        var former = members.Single(m => m.UserId == owner.Id);
        Assert.Equal((GroupMemberStatus.Left, "Usuário removido"), (former.Status, former.Name));
        Assert.Equal(3, (await member.GetOk<GroupDetailDto>($"/api/v1/groups/{gid}")).MemberCount);
        var otherGroup = await member.GetOk<GroupDetailDto>($"/api/v1/groups/{other.Id}");
        Assert.True(otherGroup.IsActive);
        Assert.Equal(GroupMemberRole.Owner, otherGroup.MyRole);

        // Histórico: a movimentação e o pagamento permanecem, com o autor anonimizado e sem conta.
        var tx = await member.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{gid}/transactions/{groupTx.Id}");
        Assert.Equal("Usuário removido", tx.CreatedBy.Name);
        Assert.Equal(100m, tx.PaidAmount);
        Assert.Equal("Usuário removido", Assert.Single(tx.Payments).PaidBy.Name);
        Assert.Equal("Só da Dona", tx.CategoryName);
        var stored = await h.WithDbAsync(db => Task.FromResult(db.TransactionPayments.AsNoTracking().Single(p => p.TransactionId == groupTx.Id)));
        Assert.Null(stored.AccountId);
        Assert.Equal(owner.Id, stored.CreatedByUserId);
        var dashboard = await member.GetOk<GroupDashboardDto>($"/api/v1/groups/{gid}/dashboard?from={h.Today.AddDays(-5):yyyy-MM-dd}&to={h.Today.AddDays(5):yyyy-MM-dd}");
        Assert.Equal(300m, dashboard.Totals.Expenses);
        Assert.Contains(dashboard.CreatedBy, p => p.Name == "Usuário removido" && p.Status == GroupMemberStatus.Left);

        // Convite pendente enviado por ela foi revogado; dados individuais foram removidos.
        var invitation = await h.WithDbAsync(db => Task.FromResult(db.GroupInvitations.AsNoTracking().Single(i => i.Id == pending.Id)));
        Assert.Equal(GroupInvitationStatus.Revoked, invitation.Status);
        Assert.Equal((0, 0), await h.WithDbAsync(async db => (
            await db.Accounts.CountAsync(a => a.UserId == owner.Id),
            await db.Transactions.CountAsync(t => t.GroupId == null && t.CreatedByUserId == owner.Id))));
        // O nome anonimizado e o histórico não expõem o e-mail do usuário removido.
        GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await member.Get($"/api/v1/groups/{gid}/transactions/{groupTx.Id}")), owner.Session.Email);
    }

    [Fact]
    public async Task DeletingTheOwner_WithoutAdmins_TransfersToTheOldestMember()
    {
        var owner = await h.NewUserAsync();
        var first = await h.NewUserAsync("Primeira");
        var second = await h.NewUserAsync("Segunda");
        var group = await h.CreateGroupAsync(owner);
        await h.JoinAsync(owner, group.Id, first);
        await h.JoinAsync(owner, group.Id, second);

        await DeleteAsync(owner);

        Assert.Equal(GroupMemberRole.Owner, (await first.GetOk<GroupDetailDto>($"/api/v1/groups/{group.Id}")).MyRole);
        Assert.Equal(GroupMemberRole.Member, (await second.GetOk<GroupDetailDto>($"/api/v1/groups/{group.Id}")).MyRole);
    }

    [Fact]
    public async Task DeletingTheOnlyMember_ArchivesTheGroup_AndKeepsItsRecords()
    {
        var owner = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        var tx = await h.CreateGroupTxAsync(owner, group.Id, "Sozinha", 50m);

        await DeleteAsync(owner);

        var state = await h.WithDbAsync(async db => (
            (await db.Groups.AsNoTracking().SingleAsync(g => g.Id == group.Id)).IsActive,
            await db.Transactions.CountAsync(t => t.Id == tx.Id),
            (await db.GroupMembers.AsNoTracking().SingleAsync(m => m.GroupId == group.Id)).Status));
        Assert.Equal((false, 1, GroupMemberStatus.Left), state);
    }

    [Fact]
    public async Task DeletingAMember_LeavesTheGroupIntact_AndOtherUsersUnaffected()
    {
        var owner = await h.NewUserAsync("Dona");
        var member = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner);
        await h.JoinAsync(owner, group.Id, member);
        var tx = await h.CreateGroupTxAsync(member, group.Id, "Da Bia", 80m);
        await h.PayGroupTxAsync(member, group.Id, tx.Id, 80m);
        var ownerTx = await h.CreateGroupTxAsync(owner, group.Id, "Da Dona", 20m);

        await DeleteAsync(member);

        var detail = await owner.GetOk<GroupDetailDto>($"/api/v1/groups/{group.Id}");
        Assert.Equal((GroupMemberRole.Owner, 1, true), (detail.MyRole, detail.MemberCount, detail.IsActive));
        var seen = await owner.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{group.Id}/transactions/{tx.Id}");
        Assert.Equal(("Usuário removido", TransactionStatusPaid()), (seen.CreatedBy.Name, seen.Status));
        // A Dona segue editando a própria movimentação.
        Assert.True((await owner.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{group.Id}/transactions/{ownerTx.Id}")).Permissions.CanEdit);
        // Sem a criadora ativa, a movimentação do removido fica sem edição para todos (autor-apenas).
        Assert.False(seen.Permissions.CanEdit);
    }

    private static FinanceApp.Domain.Transactions.TransactionStatus TransactionStatusPaid() => FinanceApp.Domain.Transactions.TransactionStatus.Paid;
}
