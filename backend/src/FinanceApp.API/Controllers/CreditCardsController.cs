using FinanceApp.Application.CreditCards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Cartões de crédito individuais (contrato §11).</summary>
[ApiController]
[Authorize]
[Route("api/v1/credit-cards")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class CreditCardsController(CreditCardService cards) : ControllerBase
{
    /// <param name="includeInactive">Inclui cartões desativados (padrão: false).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CreditCardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CreditCardDto>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await cards.ListAsync(includeInactive, cancellationToken));

    [HttpPost]
    [ProducesResponseType<CreditCardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CreditCardDto>> Create([FromBody] CreateCreditCardRequest request, CancellationToken cancellationToken)
    {
        var card = await cards.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = card.Id }, card);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CreditCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CreditCardDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await cards.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CreditCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CreditCardDto>> Update(Guid id, [FromBody] UpdateCreditCardRequest request, CancellationToken cancellationToken) =>
        Ok(await cards.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<CreditCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CreditCardDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await cards.DeactivateAsync(id, cancellationToken));

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<CreditCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CreditCardDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await cards.ActivateAsync(id, cancellationToken));
}
