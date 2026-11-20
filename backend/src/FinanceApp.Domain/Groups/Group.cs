using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Groups;

/// <summary>
/// Grupo financeiro (agregado com seus participantes). Concentra as regras de papéis e participação (D-030/D-031/D-036).
/// Grupos nunca são excluídos, apenas arquivados (somente leitura), para preservar o histórico.
/// </summary>
public sealed class Group
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private readonly List<GroupMember> members = [];

    private Group()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary><c>false</c> = arquivado (somente leitura).</summary>
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<GroupMember> Members => members;

    /// <summary>Cria o grupo; o criador é o primeiro participante, com o papel <c>Owner</c>.</summary>
    public static Group Create(Guid creatorUserId, string name, string? description, DateTimeOffset now)
    {
        if (creatorUserId == Guid.Empty)
        {
            throw new DomainException("O usuário é obrigatório.");
        }

        var utcNow = now.ToUniversalTime();
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = NormalizeName(name),
            Description = NormalizeDescription(description),
            CreatedByUserId = creatorUserId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            IsActive = true,
        };
        group.members.Add(GroupMember.Create(group.Id, creatorUserId, GroupMemberRole.Owner, utcNow));
        return group;
    }

    public GroupMember? FindMember(Guid memberId) => members.FirstOrDefault(m => m.Id == memberId);

    public GroupMember? FindMemberByUser(Guid userId) => members.FirstOrDefault(m => m.UserId == userId);

    public GroupMember? FindActiveMember(Guid userId) => members.FirstOrDefault(m => m.UserId == userId && m.IsActive);

    public int ActiveMemberCount => members.Count(m => m.IsActive);

    public GroupPermissions PermissionsOf(GroupMember member) => GroupPermissions.For(member.Role, IsActive);

    // ------------------------------------------------------------------ dados do grupo

    public void Update(GroupMember actor, string name, string? description, DateTimeOffset now)
    {
        EnsureWritable();
        EnsureManager(actor, "Somente administradores podem editar o grupo.");
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        UpdatedAt = now.ToUniversalTime();
    }

    public void Archive(GroupMember actor, DateTimeOffset now)
    {
        EnsureManager(actor, "Somente administradores podem arquivar o grupo.");
        if (!IsActive)
        {
            throw new DomainException("O grupo já está arquivado.");
        }

        IsActive = false;
        UpdatedAt = now.ToUniversalTime();
    }

    public void Unarchive(GroupMember actor, DateTimeOffset now)
    {
        EnsureManager(actor, "Somente administradores podem reativar o grupo.");
        if (IsActive)
        {
            throw new DomainException("O grupo não está arquivado.");
        }

        IsActive = true;
        UpdatedAt = now.ToUniversalTime();
    }

    /// <summary>Grupo arquivado é somente leitura: nenhuma alteração financeira ou de participantes.</summary>
    public void EnsureWritable()
    {
        if (!IsActive)
        {
            throw new DomainException("Este grupo está arquivado e é somente leitura. Reative o grupo para alterá-lo.");
        }
    }

    /// <summary>Convidar/reenviar/revogar convites: Owner/Admin, com o grupo ativo.</summary>
    public void EnsureCanInvite(GroupMember actor)
    {
        EnsureManager(actor, "Somente administradores podem gerenciar convites.");
        EnsureWritable();
    }

    // ------------------------------------------------------------------ participantes

    /// <summary>
    /// Inclui o usuário como <c>Member</c>: cria o participante ou reativa a linha existente de quem saiu/foi removido (D-031).
    /// </summary>
    public GroupMember AddOrReactivateMember(Guid userId, DateTimeOffset now)
    {
        EnsureWritable();
        var existing = FindMemberByUser(userId);
        if (existing is { IsActive: true })
        {
            throw new DomainException("O usuário já é participante ativo deste grupo.");
        }

        if (existing is not null)
        {
            existing.Reactivate(now);
            UpdatedAt = now.ToUniversalTime();
            return existing;
        }

        var member = GroupMember.Create(Id, userId, GroupMemberRole.Member, now);
        members.Add(member);
        UpdatedAt = now.ToUniversalTime();
        return member;
    }

    /// <summary>
    /// Promove Member→Admin (Owner/Admin) ou rebaixa Admin→Member (somente Owner). O Owner não pode ser alterado.
    /// </summary>
    public void ChangeRole(GroupMember actor, GroupMember target, GroupMemberRole newRole, DateTimeOffset now)
    {
        EnsureWritable();
        EnsureManager(actor, "Somente administradores podem alterar papéis.");
        EnsureActiveMemberOfGroup(target);
        if (newRole is not (GroupMemberRole.Admin or GroupMemberRole.Member))
        {
            throw new DomainException("O papel deve ser Admin ou Member.");
        }

        if (target.Role == GroupMemberRole.Owner)
        {
            throw new DomainException("O papel do proprietário do grupo não pode ser alterado.");
        }

        if (target.Role == newRole)
        {
            return;
        }

        if (target.Role == GroupMemberRole.Admin && actor.Role != GroupMemberRole.Owner)
        {
            throw new PermissionDeniedException("Somente o proprietário do grupo pode rebaixar um administrador.");
        }

        target.SetRole(newRole);
        UpdatedAt = now.ToUniversalTime();
    }

    /// <summary>Remove um participante (status <c>Removed</c>). Owner remove Admin/Member; Admin remove apenas Member.</summary>
    public void RemoveMember(GroupMember actor, GroupMember target, DateTimeOffset now)
    {
        EnsureWritable();
        EnsureManager(actor, "Somente administradores podem remover participantes.");
        EnsureActiveMemberOfGroup(target);
        if (target.Id == actor.Id)
        {
            throw new DomainException("Você não pode remover a si mesmo. Use a opção de sair do grupo.");
        }

        if (target.Role == GroupMemberRole.Owner)
        {
            throw new DomainException("O proprietário do grupo não pode ser removido.");
        }

        if (target.Role == GroupMemberRole.Admin && actor.Role != GroupMemberRole.Owner)
        {
            throw new PermissionDeniedException("Somente o proprietário do grupo pode remover um administrador.");
        }

        target.Deactivate(GroupMemberStatus.Removed, now);
        UpdatedAt = now.ToUniversalTime();
    }

    /// <summary>O próprio participante sai (status <c>Left</c>). O proprietário não pode sair. Permitido em grupo arquivado.</summary>
    public void Leave(GroupMember actor, DateTimeOffset now)
    {
        EnsureActiveMemberOfGroup(actor);
        if (actor.Role == GroupMemberRole.Owner)
        {
            throw new DomainException("O proprietário não pode sair do grupo.");
        }

        actor.Deactivate(GroupMemberStatus.Left, now);
        UpdatedAt = now.ToUniversalTime();
    }

    /// <summary>
    /// Saída por exclusão de conta (D-036): o participante passa a <c>Left</c>. Se era o proprietário, a propriedade vai ao
    /// Admin ativo mais antigo (senão ao Member ativo mais antigo); sem participantes ativos, o grupo é arquivado.
    /// </summary>
    public void DepartBecauseAccountDeleted(Guid userId, DateTimeOffset now)
    {
        var member = FindActiveMember(userId);
        if (member is null)
        {
            return;
        }

        var wasOwner = member.Role == GroupMemberRole.Owner;
        member.Deactivate(GroupMemberStatus.Left, now);
        if (wasOwner)
        {
            var successor = members
                .Where(m => m.IsActive)
                .OrderBy(m => m.Role == GroupMemberRole.Admin ? 0 : 1)
                .ThenBy(m => m.JoinedAt)
                .ThenBy(m => m.Id)
                .FirstOrDefault();
            if (successor is not null)
            {
                successor.SetRole(GroupMemberRole.Owner);
            }
            else
            {
                IsActive = false;
            }
        }

        UpdatedAt = now.ToUniversalTime();
    }

    // ------------------------------------------------------------------ internos

    private void EnsureActiveMemberOfGroup(GroupMember member)
    {
        if (member.GroupId != Id || !member.IsActive)
        {
            throw new DomainException("O participante não pertence a este grupo.");
        }
    }

    private static void EnsureManager(GroupMember actor, string message)
    {
        if (!actor.IsActive || actor.Role is not (GroupMemberRole.Owner or GroupMemberRole.Admin))
        {
            throw new PermissionDeniedException(message);
        }
    }

    private static string NormalizeName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > NameMaxLength)
        {
            throw new DomainException($"O nome do grupo deve ter entre 1 e {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmed is { Length: > DescriptionMaxLength })
        {
            throw new DomainException($"A descrição deve ter no máximo {DescriptionMaxLength} caracteres.");
        }

        return trimmed;
    }
}
