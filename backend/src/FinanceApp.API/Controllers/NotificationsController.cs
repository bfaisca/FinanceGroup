using FinanceApp.Application.Common;
using FinanceApp.Application.Groups;
using FinanceApp.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>
/// Notificações dentro do app (contrato §17, D-041 a D-044). O usuário vem sempre do token: notificação de outro usuário
/// responde 404. Notificações de grupo só aparecem enquanto o destinatário é participante ativo. Nenhuma resposta contém
/// conta, cartão ou e-mail.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/notifications")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    /// <summary>Minhas notificações, mais recentes primeiro. <c>unreadOnly=true</c> lista só as não lidas.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List([FromQuery] NotificationListQuery query, CancellationToken cancellationToken) =>
        Ok(await notifications.ListAsync(query, cancellationToken));

    /// <summary>Contador de não lidas (alimenta o sino).</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken cancellationToken) =>
        Ok(await notifications.CountUnreadAsync(cancellationToken));

    /// <summary>Marca como lida (idempotente).</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        await notifications.MarkAsReadAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Marca todas as minhas notificações como lidas.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await notifications.MarkAllAsReadAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Aceita o convite pela notificação (sem token) e a marca como lida. Mesmas regras do convite por e-mail.</summary>
    [HttpPost("{id:guid}/accept-invitation")]
    [ProducesResponseType<GroupSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupSummaryDto>> AcceptInvitation(Guid id, CancellationToken cancellationToken) =>
        Ok(await notifications.AcceptInvitationAsync(id, cancellationToken));

    /// <summary>Recusa o convite pela notificação e a marca como lida.</summary>
    [HttpPost("{id:guid}/decline-invitation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> DeclineInvitation(Guid id, CancellationToken cancellationToken)
    {
        await notifications.DeclineInvitationAsync(id, cancellationToken);
        return NoContent();
    }
}
