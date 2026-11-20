using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.UnitTests.Etapa4;

/// <summary>Autorização SEMPRE pelo usuário autenticado: não participante → 404, papel insuficiente → 403, regra → 422.</summary>
public sealed class GroupServiceTests : IDisposable
{
    private readonly GroupTestKit kit = new();

    public void Dispose() => kit.Dispose();

    private async Task<(FinanceApp.Domain.Users.User Owner, FinanceApp.Domain.Users.User Admin, FinanceApp.Domain.Users.User Member, GroupDetailDto Group)> SetupAsync()
    {
        var owner = await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa", Description = "Contas da casa" }, CancellationToken.None);
        var admin = await kit.NewUserAsync("Ana");
        await kit.JoinAsync(group.Id, owner, admin);
        var member = await kit.NewUserAsync("Bia");
        await kit.JoinAsync(group.Id, owner, member);

        kit.As(owner);
        var adminRow = (await kit.Groups.ListMembersAsync(group.Id, false, CancellationToken.None)).Single(m => m.UserId == admin.Id);
        await kit.Groups.ChangeRoleAsync(group.Id, adminRow.Id, new ChangeMemberRoleRequest { Role = GroupMemberRole.Admin }, CancellationToken.None);
        return (owner, admin, member, group);
    }

    [Fact]
    public async Task Create_MakesTheCallerOwner_WithPermissionsAndNoEmailInTheMembers()
    {
        var owner = await kit.NewUserAsync("Maria", "maria@exemplo.com");

        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = " Casa ", Description = "x" }, CancellationToken.None);

        Assert.Equal("Casa", group.Name);
        Assert.Equal(GroupMemberRole.Owner, group.MyRole);
        Assert.Equal(1, group.MemberCount);
        Assert.Equal("Maria", group.CreatedByName);
        Assert.True(group.IsActive);
        Assert.Equal(new GroupPermissionsDto(true, true, true, true, false, true), group.Permissions);
        var member = Assert.Single(group.Members);
        Assert.Equal(owner.Id, member.UserId);
        Assert.True(member.IsMe);
        Assert.DoesNotContain("maria@exemplo.com", System.Text.Json.JsonSerializer.Serialize(group), StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_ShowsOnlyMyGroups_Alphabetically_AndHidesArchivedByDefault()
    {
        var owner = await kit.NewUserAsync("Maria");
        await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "viagem" }, CancellationToken.None);
        var casa = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        var antigo = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Antigo" }, CancellationToken.None);
        await kit.Groups.ArchiveAsync(antigo.Id, CancellationToken.None);
        await kit.NewUserAsync("Ana");
        await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Da Ana" }, CancellationToken.None);

        kit.As(owner);
        var visible = await kit.Groups.ListAsync(false, CancellationToken.None);
        var all = await kit.Groups.ListAsync(true, CancellationToken.None);

        Assert.Equal(new[] { "Casa", "viagem" }, visible.Select(g => g.Name).ToArray());
        Assert.Equal(new[] { "Antigo", "Casa", "viagem" }, all.Select(g => g.Name).ToArray());
        Assert.Equal(casa.Id, visible[0].Id);
        Assert.All(all, g => Assert.Equal(GroupMemberRole.Owner, g.MyRole));
        Assert.False(all[0].IsActive);
    }

    [Fact]
    public async Task EveryRoute_ForANonMember_IsNotFound()
    {
        var (_, _, _, group) = await SetupAsync();
        var stranger = await kit.NewUserAsync("Caio");
        var someMember = await kit.Db.GroupMembers.AsNoTracking().FirstAsync();
        var ct = CancellationToken.None;

        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.GetAsync(group.Id, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.UpdateAsync(group.Id, new UpdateGroupRequest { Name = "x" }, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.ArchiveAsync(group.Id, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.UnarchiveAsync(group.Id, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.LeaveAsync(group.Id, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.ListMembersAsync(group.Id, true, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.ChangeRoleAsync(group.Id, someMember.Id, new ChangeMemberRoleRequest { Role = GroupMemberRole.Admin }, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.RemoveMemberAsync(group.Id, someMember.Id, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Transactions.ListAsync(group.Id, new GroupTransactionListQuery(), ct));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.CreateTxAsync(group.Id, "x", 10m));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Dashboard.GetAsync(group.Id, new GroupDashboardQuery { From = kit.Today, To = kit.Today }, ct));
        Assert.Empty(await kit.Groups.ListAsync(true, ct));
    }

    [Fact]
    public async Task Update_AndArchive_MemberIsForbidden_AdminAllowed()
    {
        var (owner, admin, member, group) = await SetupAsync();

        kit.As(member);
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Groups.UpdateAsync(group.Id, new UpdateGroupRequest { Name = "Hack" }, CancellationToken.None));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Groups.ArchiveAsync(group.Id, CancellationToken.None));

        kit.As(admin);
        var updated = await kit.Groups.UpdateAsync(group.Id, new UpdateGroupRequest { Name = "Casa nova" }, CancellationToken.None);
        Assert.Equal("Casa nova", updated.Name);
        var archived = await kit.Groups.ArchiveAsync(group.Id, CancellationToken.None);
        Assert.False(archived.IsActive);
        Assert.False(archived.Permissions.CanEditGroup);
        Assert.True(archived.Permissions.CanArchive);

        kit.As(owner);
        Assert.True((await kit.Groups.UnarchiveAsync(group.Id, CancellationToken.None)).IsActive);
    }

    [Fact]
    public async Task ChangeRole_And_Remove_FollowD030()
    {
        var (owner, admin, member, group) = await SetupAsync();
        kit.As(owner);
        var members = await kit.Groups.ListMembersAsync(group.Id, false, CancellationToken.None);
        var ownerRow = members.Single(m => m.UserId == owner.Id);
        var adminRow = members.Single(m => m.UserId == admin.Id);
        var memberRow = members.Single(m => m.UserId == member.Id);
        Assert.Equal(GroupMemberRole.Admin, adminRow.Role);

        // Admin: promove Member, mas não rebaixa Admin, nem remove Admin/Owner.
        kit.As(admin);
        var promoted = await kit.Groups.ChangeRoleAsync(group.Id, memberRow.Id, new ChangeMemberRoleRequest { Role = GroupMemberRole.Admin }, CancellationToken.None);
        Assert.Equal(GroupMemberRole.Admin, promoted.Role);
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Groups.ChangeRoleAsync(group.Id, memberRow.Id, new ChangeMemberRoleRequest { Role = GroupMemberRole.Member }, CancellationToken.None));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Groups.RemoveMemberAsync(group.Id, memberRow.Id, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => kit.Groups.ChangeRoleAsync(group.Id, ownerRow.Id, new ChangeMemberRoleRequest { Role = GroupMemberRole.Member }, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => kit.Groups.RemoveMemberAsync(group.Id, ownerRow.Id, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => kit.Groups.RemoveMemberAsync(group.Id, adminRow.Id, CancellationToken.None));

        // Owner: rebaixa e remove.
        kit.As(owner);
        var demoted = await kit.Groups.ChangeRoleAsync(group.Id, memberRow.Id, new ChangeMemberRoleRequest { Role = GroupMemberRole.Member }, CancellationToken.None);
        Assert.Equal(GroupMemberRole.Member, demoted.Role);
        await kit.Groups.RemoveMemberAsync(group.Id, memberRow.Id, CancellationToken.None);
        var former = (await kit.Groups.ListMembersAsync(group.Id, true, CancellationToken.None)).Single(m => m.Id == memberRow.Id);
        Assert.Equal(GroupMemberStatus.Removed, former.Status);
        Assert.NotNull(former.LeftAt);
        Assert.DoesNotContain(await kit.Groups.ListMembersAsync(group.Id, false, CancellationToken.None), m => m.Id == memberRow.Id);

        // O removido perde o acesso (404).
        kit.As(member);
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.GetAsync(group.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Role_OfAFormerMember_OrFromAnotherGroup_IsNotFound()
    {
        var (owner, _, member, group) = await SetupAsync();
        var otherGroup = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Outro" }, CancellationToken.None);
        var otherOwnerRow = otherGroup.Members.Single();
        kit.As(owner);
        var memberRow = (await kit.Groups.ListMembersAsync(group.Id, false, CancellationToken.None)).Single(m => m.UserId == member.Id);
        await kit.Groups.RemoveMemberAsync(group.Id, memberRow.Id, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.ChangeRoleAsync(group.Id, memberRow.Id, new ChangeMemberRoleRequest { Role = GroupMemberRole.Admin }, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.RemoveMemberAsync(group.Id, otherOwnerRow.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.RemoveMemberAsync(group.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Leave_MemberLeaves_OwnerCannot_AndLeaverLosesAccessButHistoryStays()
    {
        var (owner, _, member, group) = await SetupAsync();
        kit.As(member);
        var tx = await kit.CreateTxAsync(group.Id, "Mercado", 100m);
        await kit.PayAsync(group.Id, tx.Id, 40m);

        await kit.Groups.LeaveAsync(group.Id, CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Groups.GetAsync(group.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Transactions.GetAsync(group.Id, tx.Id, CancellationToken.None));

        kit.As(owner);
        await Assert.ThrowsAsync<DomainException>(() => kit.Groups.LeaveAsync(group.Id, CancellationToken.None));
        var seenByOwner = await kit.Transactions.GetAsync(group.Id, tx.Id, CancellationToken.None);
        Assert.Equal("Bia", seenByOwner.CreatedBy.Name);
        Assert.Equal("Bia", Assert.Single(seenByOwner.Payments).PaidBy.Name);
        var former = (await kit.Groups.ListMembersAsync(group.Id, true, CancellationToken.None)).Single(m => m.UserId == member.Id);
        Assert.Equal(GroupMemberStatus.Left, former.Status);
    }

    [Fact]
    public async Task Detail_PermissionsDependOnTheRole_AndMembersListsOnlyActive()
    {
        var (_, admin, member, group) = await SetupAsync();

        kit.As(member);
        var asMember = await kit.Groups.GetAsync(group.Id, CancellationToken.None);
        Assert.Equal(GroupMemberRole.Member, asMember.MyRole);
        Assert.Equal(new GroupPermissionsDto(false, false, false, false, true, true), asMember.Permissions);
        Assert.Equal(3, asMember.MemberCount);
        Assert.Equal(3, asMember.Members.Count);

        kit.As(admin);
        var asAdmin = await kit.Groups.GetAsync(group.Id, CancellationToken.None);
        Assert.Equal(new GroupPermissionsDto(true, true, true, true, true, true), asAdmin.Permissions);
    }

    [Fact]
    public async Task Create_InvalidName_IsAValidationError()
    {
        await kit.NewUserAsync("Maria");

        var ex = await Assert.ThrowsAsync<FinanceApp.Application.Common.Exceptions.ValidationException>(
            () => kit.Groups.CreateAsync(new CreateGroupRequest { Name = " " }, CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("name"));
    }
}
