using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Emails;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceApp.Application.Groups;

/// <summary>
/// Convites por e-mail (D-031). O token (32 bytes, base64url) só existe no link enviado: apenas o hash SHA-256 é
/// persistido e nem o token nem o e-mail do convidado são registrados em log.
/// </summary>
public sealed partial class GroupInvitationService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    GroupAccess access,
    ISecureTokenService tokens,
    IEmailSender emailSender,
    GroupInvitationEmailFactory emailFactory,
    IValidator<CreateInvitationRequest> createValidator,
    ILogger<GroupInvitationService> logger)
{
    private const int TokenBytes = 32;
    private const int TokenMaxLength = 200;
    private const string PendingEmailIndex = "ux_group_invitations_group_id_email_pending";

    // Campo exigido pelo gerador de LoggerMessage (não reconhece parâmetros do construtor primário).
    private readonly ILogger<GroupInvitationService> logger = logger;

    public async Task<IReadOnlyList<GroupInvitationDto>> ListAsync(Guid groupId, GroupInvitationStatus? status, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(groupId, tracked: false, cancellationToken).ConfigureAwait(false);
        EnsureManager(me);

        var now = clock.UtcNow;
        var invitations = await db.GroupInvitations
            .AsNoTracking()
            .Where(i => i.GroupId == groupId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var filtered = invitations.Where(i => status is null || i.GetEffectiveStatus(now) == status).ToList();
        var names = await access.LoadUserNamesAsync(filtered.Select(i => i.InvitedByUserId), cancellationToken).ConfigureAwait(false);
        return filtered.Select(i => ToDto(i, names, now, emailSent: null)).ToList();
    }

    public async Task<GroupInvitationDto> CreateAsync(Guid groupId, CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var (group, me) = await access.RequireGroupAsync(groupId, tracked: true, cancellationToken).ConfigureAwait(false);
        group.EnsureCanInvite(me);

        var email = User.NormalizeEmail(request.Email);
        var now = clock.UtcNow;

        var invitedUser = await db.Users.AsNoTracking()
            .Where(u => u.Email == email && u.IsActive)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (invitedUser is not null && group.FindActiveMember(invitedUser.Id) is not null)
        {
            throw new BusinessRuleException("Este e-mail já pertence a um participante ativo do grupo.");
        }

        await EnsureNoPendingInvitationAsync(groupId, email, now, cancellationToken).ConfigureAwait(false);

        var token = tokens.GenerateToken(TokenBytes);
        var invitation = GroupInvitation.Create(groupId, me.UserId, email, tokens.Hash(token), now);
        db.GroupInvitations.Add(invitation);
        await SaveInvitationAsync(cancellationToken).ConfigureAwait(false);

        var emailSent = await TrySendAsync(invitation, group.Name, token, cancellationToken).ConfigureAwait(false);
        var names = await access.LoadUserNamesAsync([invitation.InvitedByUserId], cancellationToken).ConfigureAwait(false);
        return ToDto(invitation, names, now, emailSent);
    }

    /// <summary>Reenvia: novo token e nova validade; o token anterior deixa de valer.</summary>
    public async Task<GroupInvitationDto> ResendAsync(Guid groupId, Guid invitationId, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(groupId, tracked: false, cancellationToken).ConfigureAwait(false);
        group.EnsureCanInvite(me);
        var invitation = await LoadInvitationAsync(groupId, invitationId, cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        var token = tokens.GenerateToken(TokenBytes);
        invitation.Renew(tokens.Hash(token), now);
        await SaveInvitationAsync(cancellationToken).ConfigureAwait(false);

        var emailSent = await TrySendAsync(invitation, group.Name, token, cancellationToken).ConfigureAwait(false);
        var names = await access.LoadUserNamesAsync([invitation.InvitedByUserId], cancellationToken).ConfigureAwait(false);
        return ToDto(invitation, names, now, emailSent);
    }

    public async Task RevokeAsync(Guid groupId, Guid invitationId, CancellationToken cancellationToken)
    {
        var (group, me) = await access.RequireGroupAsync(groupId, tracked: false, cancellationToken).ConfigureAwait(false);
        group.EnsureCanInvite(me);
        var invitation = await LoadInvitationAsync(groupId, invitationId, cancellationToken).ConfigureAwait(false);

        invitation.Revoke(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pré-visualização para a tela de convite. O usuário precisa estar autenticado (compara o e-mail do convite).</summary>
    public async Task<InvitationPreviewDto> PreviewAsync(string token, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var invitation = await FindByTokenAsync(token, tracked: false, cancellationToken).ConfigureAwait(false);
        var group = await db.Groups.AsNoTracking().Include(g => g.Members)
            .FirstAsync(g => g.Id == invitation.GroupId, cancellationToken).ConfigureAwait(false);
        var user = await GetCurrentUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var names = await access.LoadUserNamesAsync([invitation.InvitedByUserId], cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        var status = invitation.GetEffectiveStatus(now);
        var alreadyMember = group.FindActiveMember(userId) is not null;
        var canAccept = status == GroupInvitationStatus.Pending && group.IsActive && invitation.EmailMatches(user.Email) && !alreadyMember;

        return new InvitationPreviewDto(
            group.Name,
            names.GetValueOrDefault(invitation.InvitedByUserId, User.AnonymizedName),
            invitation.Email,
            status,
            invitation.ExpiresAt,
            canAccept,
            alreadyMember);
    }

    public async Task<GroupSummaryDto> AcceptAsync(string token, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var invitation = await FindByTokenAsync(token, tracked: true, cancellationToken).ConfigureAwait(false);
        var user = await GetCurrentUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var group = await db.Groups.AsTracking().Include(g => g.Members)
            .FirstAsync(g => g.Id == invitation.GroupId, cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        // 1) e-mail (403) e estado do convite (422); 2) grupo ativo e ainda não participante (422); 3) só então muda o estado.
        invitation.EnsureCanRespond(user.Email, now);
        var isNewRow = group.FindMemberByUser(userId) is null;
        var member = group.AddOrReactivateMember(userId, now);
        if (isNewRow)
        {
            db.GroupMembers.Add(member);
        }

        invitation.Accept(user.Email, now);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogInvitationAccepted(invitation.Id, group.Id);

        return new GroupSummaryDto(
            group.Id, group.Name, group.Description, member.Role, group.ActiveMemberCount, group.IsActive, group.CreatedAt);
    }

    public async Task DeclineAsync(string token, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var invitation = await FindByTokenAsync(token, tracked: true, cancellationToken).ConfigureAwait(false);
        var user = await GetCurrentUserAsync(userId, cancellationToken).ConfigureAwait(false);

        invitation.Decline(user.Email, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ internos

    private static void EnsureManager(GroupMember me)
    {
        if (me.Role is not (GroupMemberRole.Owner or GroupMemberRole.Admin))
        {
            throw new ForbiddenException(detail: "Somente administradores podem gerenciar convites.");
        }
    }

    private async Task EnsureNoPendingInvitationAsync(Guid groupId, string email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await db.GroupInvitations.AsTracking()
            .Where(i => i.GroupId == groupId && i.Email == email && i.Status == GroupInvitationStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var invitation in pending)
        {
            // Vencido: constata a expiração (libera o índice de "um pendente por e-mail") e segue.
            if (!invitation.MarkExpiredIfDue(now))
            {
                throw new ConflictException(ErrorCodes.Conflict, "Já existe um convite pendente para este e-mail.");
            }
        }
    }

    private async Task<GroupInvitation> LoadInvitationAsync(Guid groupId, Guid invitationId, CancellationToken cancellationToken) =>
        await db.GroupInvitations.AsTracking()
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.GroupId == groupId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new NotFoundException("Convite não encontrado.");

    private async Task<GroupInvitation> FindByTokenAsync(string token, bool tracked, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > TokenMaxLength)
        {
            throw new NotFoundException("Convite não encontrado.");
        }

        var hash = tokens.Hash(token.Trim());
        IQueryable<GroupInvitation> query = db.GroupInvitations;
        query = tracked ? query.AsTracking() : query.AsNoTracking();
        return await query.FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Convite não encontrado.");
    }

    private async Task<User> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        return user is { IsActive: true } ? user : throw UnauthorizedException.NotAuthenticated();
    }

    /// <summary>Corrida de dois convites para o mesmo e-mail: o índice parcial único converte em 409.</summary>
    private async Task SaveInvitationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == PendingEmailIndex)
        {
            throw new ConflictException(ErrorCodes.Conflict, "Já existe um convite pendente para este e-mail.");
        }
    }

    /// <summary>Falha no envio nunca derruba a operação: o convite existe e o Admin pode reenviar (<c>emailSent=false</c>).</summary>
    private async Task<bool> TrySendAsync(GroupInvitation invitation, string groupName, string token, CancellationToken cancellationToken)
    {
        var inviterId = currentUser.RequireUserId();
        var inviterNames = await access.LoadUserNamesAsync([inviterId], cancellationToken).ConfigureAwait(false);
        var inviterName = inviterNames.GetValueOrDefault(inviterId, User.AnonymizedName);
        try
        {
            await emailSender.SendAsync(
                    emailFactory.Create(invitation.Email, inviterName, groupName, token, invitation.ExpiresAt), cancellationToken)
                .ConfigureAwait(false);
            LogInvitationEmailSent(invitation.Id, invitation.GroupId);
            return true;
        }
#pragma warning disable CA1031 // Falhas de SMTP não podem derrubar a criação do convite.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogInvitationEmailFailed(ex, invitation.Id, invitation.GroupId);
            return false;
        }
    }

    private static GroupInvitationDto ToDto(GroupInvitation i, IReadOnlyDictionary<Guid, string> names, DateTimeOffset now, bool? emailSent) =>
        new(
            i.Id,
            i.Email,
            i.GetEffectiveStatus(now),
            names.GetValueOrDefault(i.InvitedByUserId, User.AnonymizedName),
            i.ExpiresAt,
            i.CreatedAt,
            emailSent);

    [LoggerMessage(Level = LogLevel.Information, Message = "Convite {InvitationId} do grupo {GroupId} enviado por e-mail.")]
    private partial void LogInvitationEmailSent(Guid invitationId, Guid groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao enviar o e-mail do convite {InvitationId} do grupo {GroupId}.")]
    private partial void LogInvitationEmailFailed(Exception exception, Guid invitationId, Guid groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Convite {InvitationId} aceito; novo participante no grupo {GroupId}.")]
    private partial void LogInvitationAccepted(Guid invitationId, Guid groupId);
}
