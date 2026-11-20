namespace FinanceApp.Domain.Groups;

/// <summary>
/// Participação de um usuário num grupo. Uma única linha por (grupo, usuário): sair/ser removido muda o status
/// e reentrar reativa a mesma linha, preservando o histórico de movimentações e pagamentos (D-031).
/// </summary>
public sealed class GroupMember
{
    private GroupMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public Guid UserId { get; private set; }

    public GroupMemberRole Role { get; private set; }

    public GroupMemberStatus Status { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset? LeftAt { get; private set; }

    public bool IsActive => Status == GroupMemberStatus.Active;

    internal static GroupMember Create(Guid groupId, Guid userId, GroupMemberRole role, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            Role = role,
            Status = GroupMemberStatus.Active,
            JoinedAt = now.ToUniversalTime(),
        };

    internal void SetRole(GroupMemberRole role) => Role = role;

    internal void Deactivate(GroupMemberStatus status, DateTimeOffset now)
    {
        Status = status;
        LeftAt = now.ToUniversalTime();
    }

    /// <summary>Reentrada: mesma linha, papel <c>Member</c>, nova data de entrada.</summary>
    internal void Reactivate(DateTimeOffset now)
    {
        Role = GroupMemberRole.Member;
        Status = GroupMemberStatus.Active;
        JoinedAt = now.ToUniversalTime();
        LeftAt = null;
    }
}
