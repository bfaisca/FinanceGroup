namespace FinanceApp.Domain.Groups;

public enum GroupMemberRole
{
    Owner,
    Admin,
    Member,
}

public enum GroupMemberStatus
{
    Active,
    Left,
    Removed,
}

/// <summary><c>Expired</c> é derivado na leitura (<c>Pending</c> com validade vencida); só é persistido quando a expiração é constatada.</summary>
public enum GroupInvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Revoked,
    Expired,
}
