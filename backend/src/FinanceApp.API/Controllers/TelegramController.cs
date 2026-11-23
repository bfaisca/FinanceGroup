using System.Security.Cryptography;
using System.Text;
using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinanceApp.API.Controllers;

/// <summary>Vínculo da conta com o bot do Telegram (contrato §18, D-053). O usuário vem sempre do token.</summary>
[ApiController]
[Authorize]
[Route("api/v1/telegram")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class TelegramController(TelegramLinkService links) : ControllerBase
{
    /// <summary>Estado do vínculo (e se o bot está habilitado no ambiente).</summary>
    [HttpGet("link")]
    [ProducesResponseType<TelegramLinkStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TelegramLinkStatusDto>> Status(CancellationToken cancellationToken) =>
        Ok(await links.GetStatusAsync(cancellationToken));

    /// <summary>Gera o código de vínculo (uso único, 10 minutos). Um novo código invalida o anterior.</summary>
    [HttpPost("link-code")]
    [ProducesResponseType<TelegramLinkCodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<TelegramLinkCodeDto>> CreateCode(CancellationToken cancellationToken) =>
        Ok(await links.CreateCodeAsync(cancellationToken));

    /// <summary>Desfaz o vínculo e descarta códigos e lançamentos pendentes (idempotente).</summary>
    [HttpDelete("link")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unlink(CancellationToken cancellationToken)
    {
        await links.UnlinkAsync(cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Webhook do Telegram (D-053). Anônimo por natureza: a autenticidade é garantida pelo segredo do cabeçalho
/// <c>X-Telegram-Bot-Api-Secret-Token</c>, comparado em tempo constante. Com o bot desligado ou segredo inválido responde 404.
/// Falhas internas nunca viram erro HTTP (o Telegram reenviaria o mesmo update).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/telegram/webhook")]
public sealed class TelegramWebhookController(TelegramBotService bot, IOptions<TelegramOptions> options) : ControllerBase
{
    public const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Receive([FromBody] TelegramUpdate update, CancellationToken cancellationToken)
    {
        var opt = options.Value;
        var provided = Request.Headers[SecretHeader].ToString();
        if (!opt.Enabled
            || string.IsNullOrEmpty(opt.WebhookSecret)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(opt.WebhookSecret)))
        {
            return NotFound();
        }

        await bot.HandleAsync(update, cancellationToken);
        return Ok();
    }
}
