using FinanceApp.Application.Common;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Notifications;

/// <summary>
/// Notificações dentro do app (contrato §17, D-041 a D-044). O usuário vem SEMPRE do contexto autenticado; notificação de
/// outro usuário é indistinguível de inexistente (404). Notificações de grupo só aparecem/contam enquanto o destinatário é
/// participante ATIVO do grupo; convites não dependem disso. Aceitar/recusar reaproveita as regras do
/// <see cref="GroupInvitationService"/> (sem duplicá-las).
/// </summary>
public sealed class NotificationService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    GroupAccess access,
    GroupInvitationService invitations,
    IValidator<NotificationListQuery> listValidator)
{
    public async Task<PagedResult<NotificationDto>> ListAsync(NotificationListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var me = currentUser.RequireUserId();

        var visible = VisibleTo(me);
        if (query.UnreadOnly)
        {
            visible = visible.Where(n => n.ReadAt == null);
        }

        var total = await visible.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await visible
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await ToDtosAsync(me, rows, cancellationToken).ConfigureAwait(false);
        return PagedResult.Create(items, query.Page, query.PageSize, total);
    }

    public async Task<UnreadCountDto> CountUnreadAsync(CancellationToken cancellationToken)
    {
        var me = currentUser.RequireUserId();
        var count = await VisibleTo(me).CountAsync(n => n.ReadAt == null, cancellationToken).ConfigureAwait(false);
        return new UnreadCountDto(count);
    }

    /// <summary>Idempotente: já lida continua lida (a primeira leitura é preservada).</summary>
    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var me = currentUser.RequireUserId();
        var notification = await VisibleTo(me, tracked: true)
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw NotificationNotFound();

        if (notification.IsRead)
        {
            return;
        }

        notification.MarkAsRead(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken)
    {
        var me = currentUser.RequireUserId();
        var unread = await VisibleTo(me, tracked: true).Where(n => n.ReadAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (unread.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        foreach (var notification in unread)
        {
            notification.MarkAsRead(now);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Aceita o convite pela notificação (sem token): mesmas regras do convite por e-mail (D-031/D-043).</summary>
    public async Task<GroupSummaryDto> AcceptInvitationAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var invitationId = await RequireInvitationIdAsync(notificationId, cancellationToken).ConfigureAwait(false);
        return await invitations.AcceptByInvitationIdAsync(invitationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeclineInvitationAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var invitationId = await RequireInvitationIdAsync(notificationId, cancellationToken).ConfigureAwait(false);
        await invitations.DeclineByInvitationIdAsync(invitationId, cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ internos

    private static NotFoundException NotificationNotFound() => new("Notificação não encontrada.");

    /// <summary>Notificação do usuário atual que é de convite; qualquer outra situação (inexistente, de outro usuário, de outro tipo) → 404.</summary>
    private async Task<Guid> RequireInvitationIdAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var me = currentUser.RequireUserId();
        var invitationId = await db.Notifications.AsNoTracking()
            .Where(n => n.Id == notificationId && n.RecipientUserId == me && n.Type == NotificationType.GroupInvitationReceived)
            .Select(n => n.InvitationId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return invitationId ?? throw NotificationNotFound();
    }

    /// <summary>
    /// Base de toda consulta (D-044): notificações do usuário; as de grupo só enquanto ele for participante ativo do grupo.
    /// </summary>
    private IQueryable<Notification> VisibleTo(Guid userId, bool tracked = false)
    {
        IQueryable<Notification> source = db.Notifications;
        source = tracked ? source.AsTracking() : source.AsNoTracking();
        return source.Where(n =>
            n.RecipientUserId == userId
            && (n.Type == NotificationType.GroupInvitationReceived
                || db.GroupMembers.Any(m => m.GroupId == n.GroupId && m.UserId == userId && m.Status == GroupMemberStatus.Active)));
    }

    private async Task<List<NotificationDto>> ToDtosAsync(Guid me, List<Notification> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var now = clock.UtcNow;
        var groupIds = rows.Select(n => n.GroupId).Distinct().ToList();
        var groupNames = await db.Groups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken)
            .ConfigureAwait(false);
        var actorNames = await access.LoadUserNamesAsync(rows.Where(n => n.ActorUserId is not null).Select(n => n.ActorUserId!.Value), cancellationToken)
            .ConfigureAwait(false);

        var invitationIds = rows.Where(n => n.InvitationId is not null).Select(n => n.InvitationId!.Value).Distinct().ToList();
        var invitationStates = new Dictionary<Guid, NotificationInvitationDto>();
        if (invitationIds.Count > 0)
        {
            var myEmail = await db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => u.Email).FirstAsync(cancellationToken).ConfigureAwait(false);
            var found = await db.GroupInvitations.AsNoTracking().Where(i => invitationIds.Contains(i.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
            var memberOf = (await db.GroupMembers.AsNoTracking()
                    .Where(m => m.UserId == me && m.Status == GroupMemberStatus.Active && groupIds.Contains(m.GroupId))
                    .Select(m => m.GroupId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .ToHashSet();
            foreach (var invitation in found)
            {
                invitationStates[invitation.Id] = ToInvitationDto(invitation, myEmail, memberOf.Contains(invitation.GroupId), now);
            }
        }

        return rows.Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.CreatedAt,
                n.ReadAt,
                n.IsRead,
                new NotificationGroupDto(n.GroupId, groupNames.GetValueOrDefault(n.GroupId, string.Empty)),
                n.ActorUserId is { } actorId ? new NotificationActorDto(actorId, actorNames.GetValueOrDefault(actorId, User.AnonymizedName)) : null,
                n.TransactionId is { } txId
                    ? new NotificationTransactionDto(txId, n.TransactionDescription ?? string.Empty, TransactionType.Expense, n.Amount ?? 0m, n.SeriesOccurrences)
                    : null,
                n.InvitationId is { } invId && invitationStates.TryGetValue(invId, out var state) ? state : null))
            .ToList();
    }

    /// <summary>
    /// Estado derivado agora (D-043): <c>CanRespond</c> = Pending não expirado + e-mail da conta = e-mail do convite + não é
    /// participante ativo do grupo.
    /// </summary>
    internal static NotificationInvitationDto ToInvitationDto(GroupInvitation invitation, string accountEmail, bool isActiveMember, DateTimeOffset now)
    {
        var status = invitation.GetEffectiveStatus(now);
        var canRespond = status == GroupInvitationStatus.Pending && invitation.EmailMatches(accountEmail) && !isActiveMember;
        return new NotificationInvitationDto(invitation.Id, status, invitation.ExpiresAt, canRespond);
    }
}
