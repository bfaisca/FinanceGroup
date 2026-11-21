using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Notifications;

/// <summary>Grupo da notificação: id e nome (nada além do que o grupo já exibe aos participantes).</summary>
public sealed record NotificationGroupDto(Guid Id, string Name);

/// <summary>Quem executou a ação; <c>Name</c> é lido da conta na consulta ("Usuário removido" se anonimizada). Nunca e-mail.</summary>
public sealed record NotificationActorDto(Guid UserId, string Name);

/// <summary>
/// Instantâneo da movimentação. <c>Amount</c>: valor da movimentação em <c>GroupTransactionCreated</c> e valor do
/// pagamento em <c>GroupPaymentRegistered</c>.
/// </summary>
public sealed record NotificationTransactionDto(Guid Id, string Description, TransactionType Type, decimal Amount);

/// <summary>Estado do convite DERIVADO na consulta (D-043); <c>CanRespond</c> habilita aceitar/recusar.</summary>
public sealed record NotificationInvitationDto(Guid Id, GroupInvitationStatus Status, DateTimeOffset ExpiresAt, bool CanRespond);

/// <summary>Contrato §17. Nenhum campo de conta, cartão ou e-mail.</summary>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    bool IsRead,
    NotificationGroupDto Group,
    NotificationActorDto? Actor,
    NotificationTransactionDto? Transaction,
    NotificationInvitationDto? Invitation);

public sealed record UnreadCountDto(int Count);

/// <summary>Parâmetros de <c>GET /notifications</c> (contrato §17).</summary>
public sealed class NotificationListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public bool UnreadOnly { get; init; }
}
