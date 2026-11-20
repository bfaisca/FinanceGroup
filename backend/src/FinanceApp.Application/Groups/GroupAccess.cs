using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Groups;

/// <summary>Participação do usuário autenticado num grupo (sem carregar o agregado).</summary>
public sealed record GroupMembership(Guid GroupId, Guid UserId, GroupMemberRole Role, bool GroupIsActive)
{
    /// <summary>Grupo arquivado é somente leitura (D-030).</summary>
    public void EnsureWritable()
    {
        if (!GroupIsActive)
        {
            throw new DomainException("Este grupo está arquivado e é somente leitura. Reative o grupo para alterá-lo.");
        }
    }
}

/// <summary>
/// Resolve o grupo SEMPRE a partir do usuário autenticado: quem não é participante ATIVO (nunca foi, saiu ou foi removido)
/// recebe 404, sem revelar a existência do grupo (D-030).
/// </summary>
public sealed class GroupAccess(IAppDbContext db, ICurrentUser currentUser)
{
    public static NotFoundException GroupNotFound() => new("Grupo não encontrado.");

    /// <summary>Participação ativa do usuário atual (consulta leve, para movimentações e dashboard).</summary>
    public async Task<GroupMembership> RequireMembershipAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var membership = await db.GroupMembers
            .AsNoTracking()
            .Where(m => m.GroupId == groupId && m.UserId == userId && m.Status == GroupMemberStatus.Active)
            .Join(db.Groups, m => m.GroupId, g => g.Id, (m, g) => new GroupMembership(g.Id, m.UserId, m.Role, g.IsActive))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return membership ?? throw GroupNotFound();
    }

    /// <summary>Agregado completo (com todos os participantes) e o participante ativo do usuário atual.</summary>
    public async Task<(Group Group, GroupMember Me)> RequireGroupAsync(Guid groupId, bool tracked, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        IQueryable<Group> query = db.Groups.Include(g => g.Members);
        query = tracked ? query.AsTracking() : query.AsNoTracking();
        var group = await query
            .FirstOrDefaultAsync(g => g.Id == groupId && g.Members.Any(m => m.UserId == userId && m.Status == GroupMemberStatus.Active), cancellationToken)
            .ConfigureAwait(false)
            ?? throw GroupNotFound();
        var me = group.FindActiveMember(userId) ?? throw GroupNotFound();
        return (group, me);
    }

    /// <summary>Nomes de exibição (contas excluídas já aparecem como "Usuário removido"). Nunca devolve e-mail.</summary>
    public async Task<Dictionary<Guid, string>> LoadUserNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken)
            .ConfigureAwait(false);
    }
}
