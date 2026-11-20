using System.Text.Json.Serialization;
using FinanceApp.Domain.Groups;

namespace FinanceApp.Application.Groups;

/// <summary>Nenhum DTO de grupo contém conta, cartão, saldo ou e-mail de participante (D-035).</summary>
public sealed record GroupSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    GroupMemberRole MyRole,
    int MemberCount,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record GroupMemberDto(
    Guid Id,
    Guid UserId,
    string Name,
    GroupMemberRole Role,
    GroupMemberStatus Status,
    DateTimeOffset JoinedAt,
    DateTimeOffset? LeftAt,
    bool IsMe);

public sealed record GroupPermissionsDto(
    bool CanManageMembers,
    bool CanInvite,
    bool CanEditGroup,
    bool CanArchive,
    bool CanLeave,
    bool CanCreateTransactions)
{
    public static GroupPermissionsDto From(GroupPermissions p) =>
        new(p.CanManageMembers, p.CanInvite, p.CanEditGroup, p.CanArchive, p.CanLeave, p.CanCreateTransactions);
}

/// <summary>Detalhe do grupo (estende o resumo). <c>Members</c> contém apenas participantes ativos.</summary>
public sealed record GroupDetailDto(
    Guid Id,
    string Name,
    string? Description,
    GroupMemberRole MyRole,
    int MemberCount,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedByName,
    DateTimeOffset UpdatedAt,
    GroupPermissionsDto Permissions,
    IReadOnlyList<GroupMemberDto> Members);

public sealed class CreateGroupRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed class UpdateGroupRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed class ChangeMemberRoleRequest
{
    /// <summary><c>Admin</c> ou <c>Member</c>.</summary>
    public GroupMemberRole? Role { get; init; }
}

public sealed class CreateInvitationRequest
{
    public string Email { get; init; } = string.Empty;
}

public sealed record GroupInvitationDto(
    Guid Id,
    string Email,
    GroupInvitationStatus Status,
    string InvitedByName,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? EmailSent = null);

public sealed record InvitationPreviewDto(
    string GroupName,
    string InvitedByName,
    string Email,
    GroupInvitationStatus Status,
    DateTimeOffset ExpiresAt,
    bool CanAccept,
    bool AlreadyMember);
