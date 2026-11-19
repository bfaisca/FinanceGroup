using FinanceApp.Application.Common;
using FinanceApp.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Movimentações individuais, pagamentos e histórico (contrato §8).</summary>
[ApiController]
[Authorize]
[Route("api/v1/transactions")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class TransactionsController(TransactionService transactions) : ControllerBase
{
    /// <summary>Lista paginada. <c>status</c> é o estado apresentado (aceita <c>Overdue</c>).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TransactionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<TransactionDto>>> List([FromQuery] TransactionListQuery query, CancellationToken cancellationToken) =>
        Ok(await transactions.ListAsync(query, cancellationToken));

    [HttpPost]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<TransactionDetailDto>> Create([FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var created = await transactions.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TransactionDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await transactions.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<TransactionDetailDto>> Update(Guid id, [FromBody] UpdateTransactionRequest request, CancellationToken cancellationToken) =>
        Ok(await transactions.UpdateAsync(id, request, cancellationToken));

    /// <summary>Exclui somente movimentações sem pagamentos (D-020); com pagamentos: 422.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await transactions.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<TransactionDetailDto>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await transactions.CancelAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reopen")]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<TransactionDetailDto>> Reopen(Guid id, CancellationToken cancellationToken) =>
        Ok(await transactions.ReopenAsync(id, cancellationToken));

    /// <summary>Histórico de alterações (mais recente primeiro).</summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<IReadOnlyList<TransactionHistoryEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<TransactionHistoryEntryDto>>> History(Guid id, CancellationToken cancellationToken) =>
        Ok(await transactions.GetHistoryAsync(id, cancellationToken));

    /// <summary>Registra pagamento/recebimento (parcial ou integral). Excedente, data futura ou cancelada: 400/422.</summary>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<TransactionDetailDto>> AddPayment(Guid id, [FromBody] AddPaymentRequest request, CancellationToken cancellationToken)
    {
        var updated = await transactions.AddPaymentAsync(id, request, cancellationToken);
        return Created($"/api/v1/transactions/{id}", updated);
    }

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    [ProducesResponseType<TransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<TransactionDetailDto>> RemovePayment(Guid id, Guid paymentId, CancellationToken cancellationToken) =>
        Ok(await transactions.RemovePaymentAsync(id, paymentId, cancellationToken));
}
