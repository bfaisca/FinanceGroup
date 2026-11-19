using FinanceApp.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Dashboard financeiro individual (contrato §9).</summary>
[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary><c>from</c> e <c>to</c> obrigatórios (YYYY-MM-DD), <c>from ≤ to</c>, intervalo de até 366 dias.</summary>
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery] DashboardQuery query, CancellationToken cancellationToken) =>
        Ok(await dashboard.GetAsync(query, cancellationToken));
}
