using System.Text.Json.Serialization;

namespace FinanceApp.Application.Telegram;

/// <summary>Resposta de <c>POST /telegram/link-code</c>: o código aparece uma única vez e vale por poucos minutos.</summary>
public sealed record TelegramLinkCodeDto(string Code, DateTimeOffset ExpiresAt, string BotUsername, string DeepLink);

/// <summary>Estado do vínculo do usuário autenticado. Nunca expõe o identificador do chat.</summary>
public sealed record TelegramLinkStatusDto(bool Configured, bool Linked, DateTimeOffset? LinkedAt, string? BotUsername);

/// <summary>Subconjunto do <c>Update</c> do Telegram usado pelo bot.</summary>
public sealed class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; init; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQuery? CallbackQuery { get; init; }
}

public sealed class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }

    [JsonPropertyName("chat")]
    public TelegramChat? Chat { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

public sealed class TelegramCallbackQuery
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public string? Data { get; init; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }
}
