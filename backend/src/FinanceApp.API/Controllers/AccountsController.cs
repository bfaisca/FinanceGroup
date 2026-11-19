using FinanceApp.Application.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Contas financeiras individuais e saldos informados (contrato §6).</summary>
[ApiController]
[Authorize]
[Route("api/v1/accounts")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class AccountsController(AccountService accounts) : ControllerBase
{
    /// <param name="includeInactive">Inclui contas desativadas (padrão: false).</param>
    /// <param name="asOf">Data-alvo do saldo projetado (padrão: hoje no fuso da aplicação).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccountDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> List(
        [FromQuery] bool includeInactive,
        [FromQuery] DateOnly? asOf,
        CancellationToken cancellationToken) =>
        Ok(await accounts.ListAsync(includeInactive, asOf, cancellationToken));

    [HttpPost]
    [ProducesResponseType<AccountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AccountDto>> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await accounts.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = account.Id }, account);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AccountDto>> Get(Guid id, [FromQuery] DateOnly? asOf, CancellationToken cancellationToken) =>
        Ok(await accounts.GetAsync(id, asOf, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AccountDto>> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken) =>
        Ok(await accounts.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AccountDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await accounts.DeactivateAsync(id, cancellationToken));

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AccountDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await accounts.ActivateAsync(id, cancellationToken));

    /// <summary>Saldos informados da conta, do mais recente para o mais antigo.</summary>
    [HttpGet("{id:guid}/balance-snapshots")]
    [ProducesResponseType<IReadOnlyList<BalanceSnapshotDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<BalanceSnapshotDto>>> ListSnapshots(Guid id, CancellationToken cancellationToken) =>
        Ok(await accounts.ListSnapshotsAsync(id, cancellationToken));

    /// <summary>Informa um novo saldo; passa a ser a referência do saldo projetado (D-019). Conta inativa: 422.</summary>
    [HttpPost("{id:guid}/balance-snapshots")]
    [ProducesResponseType<BalanceSnapshotDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<BalanceSnapshotDto>> CreateSnapshot(
        Guid id,
        [FromBody] CreateBalanceSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var snapshot = await accounts.CreateSnapshotAsync(id, request, cancellationToken);
        return Created($"/api/v1/accounts/{id}/balance-snapshots", snapshot);
    }
}
