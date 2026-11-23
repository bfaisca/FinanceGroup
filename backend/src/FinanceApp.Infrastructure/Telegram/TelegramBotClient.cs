using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Telegram;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceApp.Infrastructure.Telegram;

/// <summary>
/// Cliente da Bot API (D-053). O token faz parte da URL exigida pelo Telegram: este cliente não registra URLs
/// (os logs padrão do HttpClient são removidos no registro) e nunca inclui o token em mensagens de erro.
/// </summary>
internal sealed partial class TelegramBotClient(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramBotClient> logger) : ITelegramClient
{
    private readonly ILogger<TelegramBotClient> logger = logger;

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public Task SendMessageAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<TelegramButton>>? keyboard, CancellationToken cancellationToken) =>
        PostAsync(
            "sendMessage",
            new
            {
                chat_id = chatId,
                text,
                reply_markup = keyboard is null
                    ? null
                    : new { inline_keyboard = keyboard.Select(row => row.Select(b => new { text = b.Text, callback_data = b.CallbackData })) },
            },
            cancellationToken);

    public Task EditMessageAsync(long chatId, long messageId, string text, CancellationToken cancellationToken) =>
        PostAsync("editMessageText", new { chat_id = chatId, message_id = messageId, text }, cancellationToken);

    public Task AnswerCallbackAsync(string callbackQueryId, CancellationToken cancellationToken) =>
        PostAsync("answerCallbackQuery", new { callback_query_id = callbackQueryId }, cancellationToken);

    private async Task PostAsync(string method, object payload, CancellationToken cancellationToken)
    {
        var token = options.Value.BotToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        try
        {
            using var response = await http.PostAsJsonAsync($"https://api.telegram.org/bot{token}/{method}", payload, Json, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogRejected(method, (int)response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            // Mensagem da exceção pode conter a URL (com o token): registrar apenas o tipo.
            LogFailed(method, ex.GetType().Name);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailed(method, "Timeout");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram recusou {Method} (HTTP {StatusCode}).")]
    private partial void LogRejected(string method, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha de comunicação com o Telegram em {Method} ({Reason}).")]
    private partial void LogFailed(string method, string reason);
}
