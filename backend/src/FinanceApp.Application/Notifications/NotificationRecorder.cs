using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Notifications;

/// <summary>
/// Cria notificações (D-042/D-043) SEMPRE na unidade de trabalho do evento de origem: apenas adiciona ao contexto e quem
/// chama persiste tudo no mesmo <c>SaveChanges</c> (mesma transação). Se o evento falhar, nada é notificado; se a notificação
/// falhar, o evento também não é gravado — a atomicidade é a garantia pedida, sem tarefas fora da transação.
/// </summary>
internal static class NotificationRecorder
{
    /// <summary>Nova despesa de grupo (ou série inteira, D-049) → membros ativos, exceto o autor (D-042a).</summary>
    public static async Task AddTransactionCreatedAsync(
        IAppDbContext db, Transaction transaction, Guid groupId, Guid actorUserId, DateTimeOffset now, int? seriesOccurrences, CancellationToken cancellationToken)
    {
        var recipients = await ActiveRecipientsAsync(db, groupId, actorUserId, cancellationToken).ConfigureAwait(false);
        foreach (var recipientId in recipients)
        {
            db.Notifications.Add(Notification.ForTransactionCreated(
                recipientId, groupId, actorUserId, transaction.Id, transaction.Description, transaction.Amount, now, seriesOccurrences));
        }
    }

    /// <summary>Pagamento/recebimento registrado → membros ativos, exceto o pagador; o valor é o TOTAL do pagamento: principal + juros (D-042b/D-048).</summary>
    public static async Task AddPaymentRegisteredAsync(
        IAppDbContext db, Transaction transaction, Guid groupId, Guid payerUserId, decimal paymentAmount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var recipients = await ActiveRecipientsAsync(db, groupId, payerUserId, cancellationToken).ConfigureAwait(false);
        foreach (var recipientId in recipients)
        {
            db.Notifications.Add(Notification.ForPaymentRegistered(
                recipientId, groupId, payerUserId, transaction.Id, transaction.Description, paymentAmount, now));
        }
    }

    /// <summary>
    /// Convite criado/reenviado (D-043c): se existe conta ATIVA com o e-mail convidado (e-mails são gravados em minúsculas,
    /// logo a comparação ignora maiúsculas), cria a notificação ou reaproveita a existente (volta a ser não lida).
    /// </summary>
    public static async Task UpsertInvitationAsync(
        IAppDbContext db, GroupInvitation invitation, Guid invitedByUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(invitation.Email);
        var recipientId = await db.Users.AsNoTracking()
            .Where(u => u.Email == email && u.IsActive)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (recipientId is not { } id)
        {
            return;
        }

        var alreadyMember = await db.GroupMembers.AsNoTracking()
            .AnyAsync(m => m.GroupId == invitation.GroupId && m.UserId == id && m.Status == GroupMemberStatus.Active, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyMember)
        {
            return;
        }

        var existing = await db.Notifications.AsTracking()
            .FirstOrDefaultAsync(n => n.RecipientUserId == id && n.InvitationId == invitation.Id, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            db.Notifications.Add(Notification.ForInvitationReceived(id, invitation.GroupId, invitedByUserId, invitation.Id, now));
        }
        else
        {
            existing.Reissue(invitedByUserId, now);
        }
    }

    /// <summary>Cadastro (D-043d): convites <c>Pending</c> não expirados para o e-mail do novo usuário viram notificações.</summary>
    public static async Task AddForNewUserAsync(IAppDbContext db, User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await db.GroupInvitations.AsNoTracking()
            .Where(i => i.Email == user.Email && i.Status == GroupInvitationStatus.Pending && i.ExpiresAt > now)
            .Select(i => new { i.Id, i.GroupId, i.InvitedByUserId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var invitation in pending)
        {
            db.Notifications.Add(Notification.ForInvitationReceived(user.Id, invitation.GroupId, invitation.InvitedByUserId, invitation.Id, now));
        }
    }

    /// <summary>Aceitar/recusar o convite (por token ou pela notificação) marca a notificação do destinatário como lida (D-044).</summary>
    public static async Task MarkInvitationReadAsync(
        IAppDbContext db, Guid recipientUserId, Guid invitationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.AsTracking()
            .FirstOrDefaultAsync(n => n.RecipientUserId == recipientUserId && n.InvitationId == invitationId, cancellationToken)
            .ConfigureAwait(false);
        notification?.MarkAsRead(now);
    }

    private static async Task<List<Guid>> ActiveRecipientsAsync(IAppDbContext db, Guid groupId, Guid excludedUserId, CancellationToken cancellationToken) =>
        await db.GroupMembers.AsNoTracking()
            .Where(m => m.GroupId == groupId && m.Status == GroupMemberStatus.Active && m.UserId != excludedUserId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
