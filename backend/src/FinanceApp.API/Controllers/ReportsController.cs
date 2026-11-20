using FinanceApp.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Relatórios individuais (contrato §14, D-028).</summary>
[ApiController]
[Authorize]
[Route("api/v1/reports")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>Totais e série do período. <c>from</c>/<c>to</c> obrigatórios (até 366 dias); <c>groupBy</c> = day | month (padrão month).</summary>
    [HttpGet("summary")]
    [ProducesResponseType<SummaryReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SummaryReportDto>> Summary([FromQuery] SummaryQuery query, CancellationToken cancellationToken) =>
        Ok(await reports.GetSummaryAsync(query, cancellationToken));

    /// <summary>Por categoria raiz, com subcategorias. <c>type</c> padrão Expense.</summary>
    [HttpGet("by-category")]
    [ProducesResponseType<CategoryReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryReportDto>> ByCategory([FromQuery] TypedPeriodQuery query, CancellationToken cancellationToken) =>
        Ok(await reports.GetByCategoryAsync(query, cancellationToken));

    /// <summary>Por conta efetiva (a da movimentação ou a do cartão); sem conta → "Sem conta".</summary>
    [HttpGet("by-account")]
    [ProducesResponseType<AccountReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountReportDto>> ByAccount([FromQuery] TypedPeriodQuery query, CancellationToken cancellationToken) =>
        Ok(await reports.GetByAccountAsync(query, cancellationToken));

    [HttpGet("by-payment-method")]
    [ProducesResponseType<PaymentMethodReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentMethodReportDto>> ByPaymentMethod([FromQuery] TypedPeriodQuery query, CancellationToken cancellationToken) =>
        Ok(await reports.GetByPaymentMethodAsync(query, cancellationToken));

    /// <summary><c>months</c> 1–36 (padrão 12) e <c>endMonth</c> YYYY-MM (padrão mês atual).</summary>
    [HttpGet("monthly-evolution")]
    [ProducesResponseType<MonthlyEvolutionReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyEvolutionReportDto>> MonthlyEvolution([FromQuery] MonthlyEvolutionQuery query, CancellationToken cancellationToken) =>
        Ok(await reports.GetMonthlyEvolutionAsync(query, cancellationToken));

    /// <summary>Comprometimento futuro das parcelas em aberto a partir do mês atual.</summary>
    [HttpGet("installments")]
    [ProducesResponseType<InstallmentsReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InstallmentsReportDto>> Installments([FromQuery] InstallmentsReportQuery query, CancellationToken cancellationToken) =>
        Ok(await reports.GetInstallmentsAsync(query, cancellationToken));

    /// <summary>Recorrências ativas e valores das ocorrências já geradas para os próximos 12 meses.</summary>
    [HttpGet("recurrences")]
    [ProducesResponseType<RecurrencesReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecurrencesReportDto>> Recurrences(CancellationToken cancellationToken) =>
        Ok(await reports.GetRecurrencesAsync(cancellationToken));
}
