using FinanceApp.Application.Common;
using FinanceApp.Application.Installments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Parcelamentos (contrato §12, D-026).</summary>
[ApiController]
[Authorize]
[Route("api/v1/installment-plans")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class InstallmentPlansController(InstallmentPlanService plans) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<InstallmentPlanDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<InstallmentPlanDto>>> List([FromQuery] InstallmentPlanListQuery query, CancellationToken cancellationToken) =>
        Ok(await plans.ListAsync(query, cancellationToken));

    /// <summary>Cria o plano e todas as parcelas na mesma transação (centavos: a primeira parcela absorve a diferença).</summary>
    [HttpPost]
    [ProducesResponseType<InstallmentPlanDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<InstallmentPlanDetailDto>> Create([FromBody] CreateInstallmentPlanRequest request, CancellationToken cancellationToken)
    {
        var created = await plans.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InstallmentPlanDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<InstallmentPlanDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await plans.GetAsync(id, cancellationToken));

    /// <summary>Cancela as parcelas sem pagamento; parcelas com pagamento permanecem.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<InstallmentPlanDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<InstallmentPlanDetailDto>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await plans.CancelAsync(id, cancellationToken));

    /// <summary>Exclui o plano e as parcelas somente se nenhuma parcela tiver pagamento; caso contrário, 422.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await plans.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
