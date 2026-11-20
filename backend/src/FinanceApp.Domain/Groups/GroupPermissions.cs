namespace FinanceApp.Domain.Groups;

/// <summary>Permissões do usuário num grupo (D-030). Grupo arquivado é somente leitura (exceto reativar e sair).</summary>
public sealed record GroupPermissions(
    bool CanManageMembers,
    bool CanInvite,
    bool CanEditGroup,
    bool CanArchive,
    bool CanLeave,
    bool CanCreateTransactions)
{
    public static GroupPermissions For(GroupMemberRole role, bool groupIsActive)
    {
        var isManager = role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        return new GroupPermissions(
            CanManageMembers: isManager && groupIsActive,
            CanInvite: isManager && groupIsActive,
            CanEditGroup: isManager && groupIsActive,
            CanArchive: isManager,
            CanLeave: role != GroupMemberRole.Owner,
            CanCreateTransactions: groupIsActive);
    }
}
