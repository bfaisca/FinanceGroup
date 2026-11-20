using FinanceApp.Application.Common;
using FinanceApp.Application.Groups;
using FinanceApp.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>
/// Movimentações de um grupo e seus pagamentos por participante (contrato §16.2). Um único registro por movimentação;
/// somente o criador altera/cancela/exclui (403 aos demais); qualquer participante ativo registra o próprio pagamento.
/// As respostas nunca contêm conta ou cartão.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/groups/{groupId:guid}/transactions")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public sealed class GroupTransactionsController(GroupTransactionService transactions) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<GroupTransactionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<GroupTransactionDto>>> List(Guid groupId, [FromQuery] GroupTransactionListQuery query, CancellationToken cancellationToken) =>
        Ok(await transactions.ListAsync(groupId, query, cancellationToken));

    [HttpPost]
    [ProducesResponseType<GroupTransactionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupTransactionDetailDto>> Create(Guid groupId, [FromBody] CreateGroupTransactionRequest request, CancellationToken cancellationToken)
    {
        var created = await transactions.CreateAsync(groupId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { groupId, txId = created.Id }, created);
    }

    [HttpGet("{txId:guid}")]
    [ProducesResponseType<GroupTransactionDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GroupTransactionDetailDto>> Get(Guid groupId, Guid txId, CancellationToken cancellationToken) =>
        Ok(await transactions.GetAsync(groupId, txId, cancellationToken));

    [HttpPut("{txId:guid}")]
    [ProducesResponseType<GroupTransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupTransactionDetailDto>> Update(Guid groupId, Guid txId, [FromBody] UpdateGroupTransactionRequest request, CancellationToken cancellationToken) =>
        Ok(await transactions.UpdateAsync(groupId, txId, request, cancellationToken));

    /// <summary>Somente o criador e somente sem pagamentos; com pagamentos: 422 (cancele).</summary>
    [HttpDelete("{txId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid groupId, Guid txId, CancellationToken cancellationToken)
    {
        await transactions.DeleteAsync(groupId, txId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{txId:guid}/cancel")]
    [ProducesResponseType<GroupTransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupTransactionDetailDto>> Cancel(Guid groupId, Guid txId, CancellationToken cancellationToken) =>
        Ok(await transactions.CancelAsync(groupId, txId, cancellationToken));

    [HttpPost("{txId:guid}/reopen")]
    [ProducesResponseType<GroupTransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupTransactionDetailDto>> Reopen(Guid groupId, Guid txId, CancellationToken cancellationToken) =>
        Ok(await transactions.ReopenAsync(groupId, txId, cancellationToken));

    /// <summary>Histórico com o nome de quem agiu (mais recente primeiro).</summary>
    [HttpGet("{txId:guid}/history")]
    [ProducesResponseType<IReadOnlyList<GroupTransactionHistoryEntryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GroupTransactionHistoryEntryDto>>> History(Guid groupId, Guid txId, CancellationToken cancellationToken) =>
        Ok(await transactions.GetHistoryAsync(groupId, txId, cancellationToken));

    /// <summary>
    /// Registra o PRÓPRIO pagamento/recebimento. <c>accountId</c> (opcional) deve ser conta do próprio pagador e nunca é devolvido;
    /// serve apenas ao saldo projetado individual.
    /// </summary>
    [HttpPost("{txId:guid}/payments")]
    [ProducesResponseType<GroupTransactionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupTransactionDetailDto>> AddPayment(Guid groupId, Guid txId, [FromBody] AddPaymentRequest request, CancellationToken cancellationToken)
    {
        var updated = await transactions.AddPaymentAsync(groupId, txId, request, cancellationToken);
        return Created($"/api/v1/groups/{groupId}/transactions/{txId}", updated);
    }

    /// <summary>Somente o próprio pagador remove o seu pagamento (403 aos demais).</summary>
    [HttpDelete("{txId:guid}/payments/{paymentId:guid}")]
    [ProducesResponseType<GroupTransactionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupTransactionDetailDto>> RemovePayment(Guid groupId, Guid txId, Guid paymentId, CancellationToken cancellationToken) =>
        Ok(await transactions.RemovePaymentAsync(groupId, txId, paymentId, cancellationToken));
}
