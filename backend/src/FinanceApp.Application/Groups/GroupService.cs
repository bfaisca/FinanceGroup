using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Groups;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Groups;

/// <summary>
/// Casos de uso de grupos e participantes (contrato §16). A identidade vem sempre do usuário autenticado; as regras de
/// papéis ficam no agregado <see cref="Group"/>. Não participante ativo → 404; papel insuficiente → 403; regra violada → 422.
/// </summary>
public sealed class GroupService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    GroupAccess access,
    IValidator<CreateGroupRequest> createValidator,
    IValidator<UpdateGroupRequest> updateValidator,
    IValidator<ChangeMemberRoleRequest> roleValidator)
{
    public async Task<IReadOnlyList<GroupSummaryDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var rows = await db.Groups
            .AsNoTracking()
            .Where(g => g.Members.Any(m => m.UserId == userId && m.Status == GroupMemberStatus.Active) && (includeArchived || g.IsActive))
            .Select(g => new GroupSummaryDto(
                g.Id,
                g.Name,
                g.Description,
                g.Members.Where(m => m.UserId == userId).Select(m => m.Role).First(),
                g.Members.Count(m => m.Status == GroupMemberStatus.Active),
                g.IsActive,
                g.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(g => g.Id).ToList();
    }

    public async Task<GroupDetailDto> CreateAsync(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();

        var group = Group.Create(userId, request.Name, request.Description, clock.UtcNow);
        db.Groups.Add(group);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await BuildDetailAsync(group, group.FindActiveMember(userId)!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(id, tracked: false, cancellationToken).ConfigureAwait(false);
        return await BuildDetailAsync(group, me, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupDetailDto> UpdateAsync(Guid id, UpdateGroupRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var (group, me) = await access.RequireGroupAsync(id, tracked: true, cancellationToken).ConfigureAwait(false);
        group.Update(me, request.Name, request.Description, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await BuildDetailAsync(group, me, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupDetailDto> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(id, tracked: true, cancellationToken).ConfigureAwait(false);
        group.Archive(me, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await BuildDetailAsync(group, me, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupDetailDto> UnarchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(id, tracked: true, cancellationToken).ConfigureAwait(false);
        group.Unarchive(me, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await BuildDetailAsync(group, me, cancellationToken).ConfigureAwait(false);
    }

    public async Task LeaveAsync(Guid id, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(id, tracked: true, cancellationToken).ConfigureAwait(false);
        group.Leave(me, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GroupMemberDto>> ListMembersAsync(Guid id, bool includeFormer, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(id, tracked: false, cancellationToken).ConfigureAwait(false);
        var members = group.Members.Where(m => includeFormer || m.IsActive).ToList();
        return await ToMemberDtosAsync(members, me.UserId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupMemberDto> ChangeRoleAsync(Guid id, Guid memberId, ChangeMemberRoleRequest request, CancellationToken cancellationToken)
    {
        await roleValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var (group, me) = await access.RequireGroupAsync(id, tracked: true, cancellationToken).ConfigureAwait(false);
        var target = FindActiveTarget(group, memberId);

        group.ChangeRole(me, target, request.Role!.Value, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await ToMemberDtosAsync([target], me.UserId, cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task RemoveMemberAsync(Guid id, Guid memberId, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(id, tracked: true, cancellationToken).ConfigureAwait(false);
        var target = FindActiveTarget(group, memberId);

        group.RemoveMember(me, target, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ montagem de DTOs

    private static GroupMember FindActiveTarget(Group group, Guid memberId) =>
        group.FindMember(memberId) is { IsActive: true } target ? target : throw new NotFoundException("Participante não encontrado.");

    private async Task<GroupDetailDto> BuildDetailAsync(Group group, GroupMember me, CancellationToken cancellationToken)
    {
        var active = group.Members.Where(m => m.IsActive).ToList();
        var members = await ToMemberDtosAsync(active, me.UserId, cancellationToken).ConfigureAwait(false);
        var names = await access.LoadUserNamesAsync([group.CreatedByUserId], cancellationToken).ConfigureAwait(false);

        return new GroupDetailDto(
            group.Id,
            group.Name,
            group.Description,
            me.Role,
            active.Count,
            group.IsActive,
            group.CreatedAt,
            names.GetValueOrDefault(group.CreatedByUserId, FinanceApp.Domain.Users.User.AnonymizedName),
            group.UpdatedAt,
            GroupPermissionsDto.From(group.PermissionsOf(me)),
            members);
    }

    private async Task<IReadOnlyList<GroupMemberDto>> ToMemberDtosAsync(
        IReadOnlyCollection<GroupMember> members,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var names = await access.LoadUserNamesAsync(members.Select(m => m.UserId), cancellationToken).ConfigureAwait(false);
        return members
            .OrderBy(m => m.IsActive ? 0 : 1)
            .ThenBy(m => m.JoinedAt)
            .ThenBy(m => m.Id)
            .Select(m => new GroupMemberDto(
                m.Id,
                m.UserId,
                names.GetValueOrDefault(m.UserId, FinanceApp.Domain.Users.User.AnonymizedName),
                m.Role,
                m.Status,
                m.JoinedAt,
                m.LeftAt,
                m.UserId == currentUserId))
            .ToList();
    }
}
