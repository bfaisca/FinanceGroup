using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa4;

/// <summary>Papéis, participação e arquivamento (D-030, D-031, D-036).</summary>
public class GroupDomainTests
{
    private static readonly Guid AdminUser = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MemberUser = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherMember = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>Owner = UserId; Admin = AdminUser; Members = MemberUser e OtherMember (entrada em instantes crescentes).</summary>
    private static (Group Group, GroupMember Owner, GroupMember Admin, GroupMember Member, GroupMember Member2) NewGroup()
    {
        var group = Group.Create(UserId, "  Casa  ", "  Despesas da casa ", Now);
        var owner = group.FindActiveMember(UserId)!;
        var admin = group.AddOrReactivateMember(AdminUser, Now.AddMinutes(1));
        group.ChangeRole(owner, admin, GroupMemberRole.Admin, Now.AddMinutes(1));
        var member = group.AddOrReactivateMember(MemberUser, Now.AddMinutes(2));
        var member2 = group.AddOrReactivateMember(OtherMember, Now.AddMinutes(3));
        return (group, owner, admin, member, member2);
    }

    [Fact]
    public void Create_MakesCreatorTheOwnerAndNormalizesText()
    {
        var group = Group.Create(UserId, "  Casa  ", "  Despesas ", Now);

        Assert.Equal("Casa", group.Name);
        Assert.Equal("Despesas", group.Description);
        Assert.True(group.IsActive);
        Assert.Equal(UserId, group.CreatedByUserId);
        var owner = Assert.Single(group.Members);
        Assert.Equal(UserId, owner.UserId);
        Assert.Equal(GroupMemberRole.Owner, owner.Role);
        Assert.Equal(GroupMemberStatus.Active, owner.Status);
        Assert.Null(owner.LeftAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankName(string name) =>
        Assert.Throws<DomainException>(() => Group.Create(UserId, name, null, Now));

    [Fact]
    public void Create_RejectsTooLongNameAndDescription()
    {
        Assert.Throws<DomainException>(() => Group.Create(UserId, new string('a', 101), null, Now));
        Assert.Throws<DomainException>(() => Group.Create(UserId, "Casa", new string('a', 501), Now));
    }

    // ------------------------------------------------------------ edição e arquivamento

    [Fact]
    public void Update_OnlyOwnerOrAdmin_MemberIsDenied()
    {
        var (group, owner, admin, member, _) = NewGroup();

        group.Update(admin, "Casa 2", null, Now.AddHours(1));
        Assert.Equal("Casa 2", group.Name);
        group.Update(owner, "Casa 3", "x", Now.AddHours(2));
        Assert.Equal("Casa 3", group.Name);

        Assert.Throws<PermissionDeniedException>(() => group.Update(member, "Hack", null, Now));
        Assert.Equal("Casa 3", group.Name);
    }

    [Fact]
    public void Archive_AndUnarchive_RequireManager_AndAreStrict()
    {
        var (group, owner, admin, member, _) = NewGroup();

        Assert.Throws<PermissionDeniedException>(() => group.Archive(member, Now));
        group.Archive(admin, Now.AddHours(1));
        Assert.False(group.IsActive);
        Assert.Throws<DomainException>(() => group.Archive(owner, Now));
        Assert.Throws<PermissionDeniedException>(() => group.Unarchive(member, Now));
        group.Unarchive(owner, Now.AddHours(2));
        Assert.True(group.IsActive);
        Assert.Throws<DomainException>(() => group.Unarchive(owner, Now));
    }

    [Fact]
    public void ArchivedGroup_IsReadOnly_ForMembersRolesAndEdits()
    {
        var (group, owner, _, member, _) = NewGroup();
        group.Archive(owner, Now);

        Assert.Throws<DomainException>(() => group.Update(owner, "Novo", null, Now));
        Assert.Throws<DomainException>(() => group.AddOrReactivateMember(Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => group.ChangeRole(owner, member, GroupMemberRole.Admin, Now));
        Assert.Throws<DomainException>(() => group.RemoveMember(owner, member, Now));
        Assert.Throws<DomainException>(() => group.EnsureCanInvite(owner));
        Assert.Throws<DomainException>(() => group.EnsureWritable());
    }

    [Fact]
    public void Leave_IsStillAllowedInAnArchivedGroup()
    {
        var (group, owner, _, member, _) = NewGroup();
        group.Archive(owner, Now);

        group.Leave(member, Now.AddHours(1));

        Assert.Equal(GroupMemberStatus.Left, member.Status);
    }

    // ------------------------------------------------------------ papéis

    [Fact]
    public void ChangeRole_AdminPromotesMember_ButOnlyOwnerDemotesAdmin()
    {
        var (group, owner, admin, member, _) = NewGroup();

        group.ChangeRole(admin, member, GroupMemberRole.Admin, Now.AddHours(1));
        Assert.Equal(GroupMemberRole.Admin, member.Role);

        // Admin não rebaixa Admin (⚠ D-030).
        Assert.Throws<PermissionDeniedException>(() => group.ChangeRole(admin, member, GroupMemberRole.Member, Now));
        Assert.Equal(GroupMemberRole.Admin, member.Role);

        group.ChangeRole(owner, member, GroupMemberRole.Member, Now.AddHours(2));
        Assert.Equal(GroupMemberRole.Member, member.Role);
    }

    [Fact]
    public void ChangeRole_MemberIsDenied_OwnerCannotBeChanged_OwnerRoleCannotBeAssigned()
    {
        var (group, owner, admin, member, member2) = NewGroup();

        Assert.Throws<PermissionDeniedException>(() => group.ChangeRole(member, member2, GroupMemberRole.Admin, Now));
        Assert.Throws<DomainException>(() => group.ChangeRole(admin, owner, GroupMemberRole.Member, Now));
        Assert.Throws<DomainException>(() => group.ChangeRole(owner, owner, GroupMemberRole.Admin, Now));
        Assert.Throws<DomainException>(() => group.ChangeRole(owner, member, GroupMemberRole.Owner, Now));
        Assert.Equal(GroupMemberRole.Owner, owner.Role);
        Assert.Equal(GroupMemberRole.Member, member.Role);
    }

    [Fact]
    public void ChangeRole_AdminCannotDemoteThemselves()
    {
        var (group, _, admin, _, _) = NewGroup();

        Assert.Throws<PermissionDeniedException>(() => group.ChangeRole(admin, admin, GroupMemberRole.Member, Now));
    }

    // ------------------------------------------------------------ remoção e saída

    [Fact]
    public void RemoveMember_AdminRemovesMember_SetsRemovedAndKeepsTheRow()
    {
        var (group, _, admin, member, _) = NewGroup();

        group.RemoveMember(admin, member, Now.AddDays(1));

        Assert.Equal(GroupMemberStatus.Removed, member.Status);
        Assert.Equal(Now.AddDays(1), member.LeftAt);
        Assert.Contains(member, group.Members);
        Assert.Null(group.FindActiveMember(MemberUser));
    }

    [Fact]
    public void RemoveMember_AdminCannotRemoveAdminOrOwner_OwnerRemovesAdmin()
    {
        var (group, owner, admin, member, _) = NewGroup();
        group.ChangeRole(owner, member, GroupMemberRole.Admin, Now);

        Assert.Throws<PermissionDeniedException>(() => group.RemoveMember(admin, member, Now));
        Assert.Throws<DomainException>(() => group.RemoveMember(admin, owner, Now));

        group.RemoveMember(owner, member, Now.AddDays(1));
        Assert.Equal(GroupMemberStatus.Removed, member.Status);
    }

    [Fact]
    public void RemoveMember_MemberIsDenied_AndNobodyRemovesThemselves()
    {
        var (group, owner, admin, member, member2) = NewGroup();

        Assert.Throws<PermissionDeniedException>(() => group.RemoveMember(member, member2, Now));
        Assert.Throws<DomainException>(() => group.RemoveMember(owner, owner, Now));
        Assert.Throws<DomainException>(() => group.RemoveMember(admin, admin, Now));
    }

    [Fact]
    public void RemoveMember_AlreadyRemovedTarget_IsRejected()
    {
        var (group, _, admin, member, _) = NewGroup();
        group.RemoveMember(admin, member, Now);

        Assert.Throws<DomainException>(() => group.RemoveMember(admin, member, Now));
    }

    [Fact]
    public void Leave_MemberAndAdminLeave_OwnerCannot()
    {
        var (group, owner, admin, member, _) = NewGroup();

        group.Leave(member, Now.AddDays(1));
        group.Leave(admin, Now.AddDays(1));

        Assert.Equal(GroupMemberStatus.Left, member.Status);
        Assert.Equal(GroupMemberStatus.Left, admin.Status);
        Assert.Throws<DomainException>(() => group.Leave(owner, Now));
        Assert.Equal(GroupMemberStatus.Active, owner.Status);
    }

    [Fact]
    public void Leave_TwiceIsRejected()
    {
        var (group, _, _, member, _) = NewGroup();
        group.Leave(member, Now);

        Assert.Throws<DomainException>(() => group.Leave(member, Now));
    }

    // ------------------------------------------------------------ reativação (D-031)

    [Fact]
    public void AddOrReactivate_FormerMember_ReusesTheSameRow()
    {
        var (group, owner, _, member, _) = NewGroup();
        group.ChangeRole(owner, member, GroupMemberRole.Admin, Now);
        var originalId = member.Id;
        group.Leave(member, Now.AddDays(1));
        Assert.Equal(GroupMemberStatus.Left, member.Status);
        var rowsBefore = group.Members.Count;

        var back = group.AddOrReactivateMember(MemberUser, Now.AddDays(10));

        Assert.Same(member, back);
        Assert.Equal(originalId, back.Id);
        Assert.Equal(rowsBefore, group.Members.Count);
        Assert.Equal(GroupMemberStatus.Active, back.Status);
        Assert.Equal(GroupMemberRole.Member, back.Role);
        Assert.Equal(Now.AddDays(10), back.JoinedAt);
        Assert.Null(back.LeftAt);
    }

    [Fact]
    public void AddOrReactivate_RemovedMemberCanComeBack_ActiveMemberCannotBeAddedTwice()
    {
        var (group, _, admin, member, _) = NewGroup();
        group.RemoveMember(admin, member, Now);

        group.AddOrReactivateMember(MemberUser, Now.AddDays(1));
        Assert.Equal(GroupMemberStatus.Active, member.Status);
        Assert.Throws<DomainException>(() => group.AddOrReactivateMember(MemberUser, Now.AddDays(2)));
    }

    // ------------------------------------------------------------ exclusão de conta (D-036)

    [Fact]
    public void Depart_NonOwner_JustLeaves()
    {
        var (group, owner, _, member, _) = NewGroup();

        group.DepartBecauseAccountDeleted(MemberUser, Now.AddDays(1));

        Assert.Equal(GroupMemberStatus.Left, member.Status);
        Assert.Equal(GroupMemberRole.Owner, owner.Role);
        Assert.True(group.IsActive);
    }

    [Fact]
    public void Depart_Owner_TransfersToOldestActiveAdmin()
    {
        var (group, owner, admin, member, _) = NewGroup();
        // Um Admin mais novo e um Member mais antigo que ele: o Admin tem prioridade, e entre Admins vale o mais antigo.
        var newerAdmin = group.AddOrReactivateMember(Guid.NewGuid(), Now.AddMinutes(10));
        group.ChangeRole(owner, newerAdmin, GroupMemberRole.Admin, Now.AddMinutes(10));

        group.DepartBecauseAccountDeleted(UserId, Now.AddDays(1));

        Assert.Equal(GroupMemberStatus.Left, owner.Status);
        Assert.Equal(GroupMemberRole.Owner, admin.Role);
        Assert.Equal(GroupMemberRole.Admin, newerAdmin.Role);
        Assert.Equal(GroupMemberRole.Member, member.Role);
        Assert.True(group.IsActive);
    }

    [Fact]
    public void Depart_Owner_WithoutAdmins_TransfersToOldestActiveMember()
    {
        var group = Group.Create(UserId, "Casa", null, Now);
        var first = group.AddOrReactivateMember(MemberUser, Now.AddMinutes(1));
        var second = group.AddOrReactivateMember(OtherMember, Now.AddMinutes(2));

        group.DepartBecauseAccountDeleted(UserId, Now.AddDays(1));

        Assert.Equal(GroupMemberRole.Owner, first.Role);
        Assert.Equal(GroupMemberRole.Member, second.Role);
    }

    [Fact]
    public void Depart_Owner_IgnoresFormerMembersWhenChoosingTheSuccessor()
    {
        var group = Group.Create(UserId, "Casa", null, Now);
        var gone = group.AddOrReactivateMember(MemberUser, Now.AddMinutes(1));
        var stays = group.AddOrReactivateMember(OtherMember, Now.AddMinutes(2));
        group.Leave(gone, Now.AddMinutes(3));

        group.DepartBecauseAccountDeleted(UserId, Now.AddDays(1));

        Assert.Equal(GroupMemberRole.Owner, stays.Role);
        Assert.Equal(GroupMemberStatus.Left, gone.Status);
    }

    [Fact]
    public void Depart_OwnerAlone_ArchivesTheGroup()
    {
        var group = Group.Create(UserId, "Casa", null, Now);

        group.DepartBecauseAccountDeleted(UserId, Now.AddDays(1));

        Assert.False(group.IsActive);
        Assert.Equal(GroupMemberStatus.Left, group.Members.Single().Status);
    }

    [Fact]
    public void Depart_UserWhoIsNotAnActiveMember_DoesNothing()
    {
        var (group, _, _, _, _) = NewGroup();
        var before = group.UpdatedAt;

        group.DepartBecauseAccountDeleted(Guid.NewGuid(), Now.AddDays(1));

        Assert.Equal(before, group.UpdatedAt);
        Assert.Equal(4, group.ActiveMemberCount);
    }

    // ------------------------------------------------------------ permissões

    [Theory]
    [InlineData(GroupMemberRole.Owner, true, true, true, true, true, false, true)]
    [InlineData(GroupMemberRole.Admin, true, true, true, true, true, true, true)]
    [InlineData(GroupMemberRole.Member, true, false, false, false, false, true, true)]
    public void Permissions_ByRole_ActiveGroup(
        GroupMemberRole role, bool active, bool manage, bool invite, bool edit, bool archive, bool leave, bool transactions)
    {
        var p = GroupPermissions.For(role, active);

        Assert.Equal(new GroupPermissions(manage, invite, edit, archive, leave, transactions), p);
    }

    [Fact]
    public void Permissions_ArchivedGroup_OnlyUnarchiveAndLeaveRemain()
    {
        var owner = GroupPermissions.For(GroupMemberRole.Owner, groupIsActive: false);
        var member = GroupPermissions.For(GroupMemberRole.Member, groupIsActive: false);

        Assert.Equal(new GroupPermissions(false, false, false, true, false, false), owner);
        Assert.Equal(new GroupPermissions(false, false, false, false, true, false), member);
    }
}
