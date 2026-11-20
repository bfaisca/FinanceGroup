using FinanceApp.Application.Recurrences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Recorrências (contrato §13, D-027). Regras não são excluídas, apenas desativadas.</summary>
[ApiController]
[Authorize]
[Route("api/v1/recurrences")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class RecurrencesController(RecurrenceService recurrences) : ControllerBase
{
    /// <param name="includeInactive">Inclui regras desativadas (padrão: false).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecurrenceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecurrenceDto>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await recurrences.ListAsync(includeInactive, cancellationToken));

    /// <summary>Cria a regra e gera as ocorrências até o horizonte (na mesma transação).</summary>
    [HttpPost]
    [ProducesResponseType<RecurrenceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<RecurrenceDto>> Create([FromBody] CreateRecurrenceRequest request, CancellationToken cancellationToken)
    {
        var rule = await recurrences.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RecurrenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RecurrenceDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await recurrences.GetAsync(id, cancellationToken));

    /// <summary>Atualiza a regra e regenera as ocorrências futuras sem pagamento/edição manual (D-027).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RecurrenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<RecurrenceDto>> Update(Guid id, [FromBody] UpdateRecurrenceRequest request, CancellationToken cancellationToken) =>
        Ok(await recurrences.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<RecurrenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RecurrenceDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await recurrences.DeactivateAsync(id, cancellationToken));

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<RecurrenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<RecurrenceDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await recurrences.ActivateAsync(id, cancellationToken));
}
