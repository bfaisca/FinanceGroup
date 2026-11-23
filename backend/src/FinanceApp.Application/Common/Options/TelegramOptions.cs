namespace FinanceApp.Application.Common.Options;

/// <summary>Seção <c>Telegram</c> (variáveis <c>Telegram__*</c>, D-053). Sem <see cref="BotToken"/> o recurso fica desligado.</summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string? BotToken { get; set; }

    /// <summary>Segredo enviado pelo Telegram no cabeçalho <c>X-Telegram-Bot-Api-Secret-Token</c> (mesmo valor do <c>setWebhook</c>).</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Nome público do bot, sem "@" (usado no link <c>t.me/&lt;bot&gt;?start=&lt;codigo&gt;</c>).</summary>
    public string? BotUsername { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(BotToken);

    public IEnumerable<string> Validate()
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(WebhookSecret) || WebhookSecret.Length < 16)
        {
            yield return "Telegram:WebhookSecret é obrigatório (mínimo 16 caracteres) quando o bot está habilitado.";
        }
        else if (!WebhookSecret.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            yield return "Telegram:WebhookSecret deve conter apenas letras, números, '_' e '-'.";
        }

        if (string.IsNullOrWhiteSpace(BotUsername))
        {
            yield return "Telegram:BotUsername é obrigatório quando o bot está habilitado.";
        }
    }
}
