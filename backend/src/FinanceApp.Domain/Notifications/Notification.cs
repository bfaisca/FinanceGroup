using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Domain.Notifications;

/// <summary>Tipos de notificação dentro do app (D-042). Edição/cancelamento/reabertura/entrada/saída não notificam.</summary>
public enum NotificationType
{
    GroupTransactionCreated,
    GroupPaymentRegistered,
    GroupInvitationReceived,
}

/// <summary>
/// Notificação dentro do app (D-041 a D-044). Guarda um instantâneo do que já é visível ao grupo (descrição, valor do evento,
/// tipo, ator): nunca conta, cartão, e-mail ou dados individuais. O nome do ator NÃO é copiado — é lido da conta na consulta.
/// </summary>
public sealed class Notification
{
    private Notification()
    {
    }

    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public NotificationType Type { get; private set; }

    public Guid GroupId { get; private set; }

    /// <summary>Quem executou a ação (autor da movimentação, pagador ou quem convidou).</summary>
    public Guid? ActorUserId { get; private set; }

    public Guid? TransactionId { get; private set; }

    public string? TransactionDescription { get; private set; }

    /// <summary>
    /// Valor da movimentação (<c>GroupTransactionCreated</c>) ou o valor TOTAL do pagamento — principal + juros
    /// (<c>GroupPaymentRegistered</c>, D-048).
    /// </summary>
    public decimal? Amount { get; private set; }

    /// <summary>Total de ocorrências criadas quando a notificação de criação é de uma série recorrente (D-049); senão nulo.</summary>
    public int? SeriesOccurrences { get; private set; }

    public Guid? InvitationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    /// <summary>Nova movimentação de grupo (D-042a). O autor nunca é o destinatário.</summary>
    public static Notification ForTransactionCreated(
        Guid recipientUserId,
        Guid groupId,
        Guid actorUserId,
        Guid transactionId,
        string description,
        decimal transactionAmount,
        DateTimeOffset now,
        int? seriesOccurrences = null)
    {
        if (seriesOccurrences is { } n && n is < GroupTransactionSeries.MinOccurrences or > GroupTransactionSeries.MaxOccurrences)
        {
            throw new DomainException("O total de ocorrências da série é inválido.");
        }

        var notification = CreateForTransactionEvent(
            NotificationType.GroupTransactionCreated, recipientUserId, groupId, actorUserId, transactionId, description, transactionAmount, now);
        notification.SeriesOccurrences = seriesOccurrences;
        return notification;
    }

    /// <summary>Pagamento registrado (D-042b): <paramref name="paymentAmount"/> é o valor TOTAL do PAGAMENTO (principal + juros).</summary>
    public static Notification ForPaymentRegistered(
        Guid recipientUserId,
        Guid groupId,
        Guid actorUserId,
        Guid transactionId,
        string description,
        decimal paymentAmount,
        DateTimeOffset now) =>
        CreateForTransactionEvent(
            NotificationType.GroupPaymentRegistered, recipientUserId, groupId, actorUserId, transactionId, description, paymentAmount, now);

    /// <summary>Convite recebido por quem já tem conta com o e-mail convidado (D-043).</summary>
    public static Notification ForInvitationReceived(
        Guid recipientUserId,
        Guid groupId,
        Guid invitedByUserId,
        Guid invitationId,
        DateTimeOffset now)
    {
        Guard.NotEmpty(recipientUserId, "O destinatário da notificação é obrigatório.");
        Guard.NotEmpty(groupId, "O grupo da notificação é obrigatório.");
        Guard.NotEmpty(invitedByUserId, "Quem convidou é obrigatório.");
        Guard.NotEmpty(invitationId, "O convite da notificação é obrigatório.");
        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Type = NotificationType.GroupInvitationReceived,
            GroupId = groupId,
            ActorUserId = invitedByUserId,
            InvitationId = invitationId,
            CreatedAt = Guard.Utc(now),
        };
    }

    /// <summary>Marca como lida; idempotente (a primeira leitura é preservada).</summary>
    public void MarkAsRead(DateTimeOffset now) => ReadAt ??= Guard.Utc(now);

    /// <summary>
    /// Reenvio do convite (D-043): reaproveita a notificação existente, volta a ser não lida e sobe ao topo da lista.
    /// Só vale para notificações de convite.
    /// </summary>
    public void Reissue(Guid invitedByUserId, DateTimeOffset now)
    {
        if (Type != NotificationType.GroupInvitationReceived)
        {
            throw new DomainException("Somente notificações de convite podem ser reaproveitadas.");
        }

        ActorUserId = Guard.NotEmpty(invitedByUserId, "Quem convidou é obrigatório.");
        ReadAt = null;
        CreatedAt = Guard.Utc(now);
    }

    private static Notification CreateForTransactionEvent(
        NotificationType type,
        Guid recipientUserId,
        Guid groupId,
        Guid actorUserId,
        Guid transactionId,
        string description,
        decimal amount,
        DateTimeOffset now)
    {
        Guard.NotEmpty(recipientUserId, "O destinatário da notificação é obrigatório.");
        Guard.NotEmpty(groupId, "O grupo da notificação é obrigatório.");
        Guard.NotEmpty(actorUserId, "Quem executou a ação é obrigatório.");
        Guard.NotEmpty(transactionId, "A movimentação da notificação é obrigatória.");
        if (recipientUserId == actorUserId)
        {
            throw new DomainException("Quem executou a ação não recebe a própria notificação.");
        }

        var trimmed = Guard.NotBlank(description, "A descrição da movimentação é obrigatória.").Trim();
        if (trimmed.Length > Transaction.DescriptionMaxLength)
        {
            throw new DomainException($"A descrição deve ter no máximo {Transaction.DescriptionMaxLength} caracteres.");
        }

        if (amount <= 0 || !MoneyRules.HasValidScale(amount) || !MoneyRules.IsWithinRange(amount))
        {
            throw new DomainException("O valor da notificação é inválido.");
        }

        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Type = type,
            GroupId = groupId,
            ActorUserId = actorUserId,
            TransactionId = transactionId,
            TransactionDescription = trimmed,
            Amount = amount,
            CreatedAt = Guard.Utc(now),
        };
    }
}
