using FinanceApp.API.RateLimiting;
using FinanceApp.Application.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceApp.API.Controllers;

/// <summary>
/// Convites vistos pelo convidado (contrato §16.1). O token vem do link enviado por e-mail e só existe nele (a API guarda o hash).
/// Exige usuário autenticado (compara o e-mail do convite com o do usuário); tem limite de requisições por IP.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/invitations/{token}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
public sealed class InvitationsController(GroupInvitationService invitations) : ControllerBase
{
    /// <summary>Dados do convite para a tela de aceite (<c>canAccept</c> considera o e-mail do usuário logado).</summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<InvitationPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InvitationPreviewDto>> Preview(string token, CancellationToken cancellationToken) =>
        Ok(await invitations.PreviewAsync(token, cancellationToken));

    /// <summary>Aceita o convite: 403 se o e-mail do usuário for diferente; 422 se expirado, revogado, já usado ou já participante.</summary>
    [HttpPost("accept")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<GroupSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupSummaryDto>> Accept(string token, CancellationToken cancellationToken) =>
        Ok(await invitations.AcceptAsync(token, cancellationToken));

    [HttpPost("decline")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Decline(string token, CancellationToken cancellationToken)
    {
        await invitations.DeclineAsync(token, cancellationToken);
        return NoContent();
    }
}
