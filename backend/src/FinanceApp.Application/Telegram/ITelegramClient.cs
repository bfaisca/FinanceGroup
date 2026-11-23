namespace FinanceApp.Application.Telegram;

/// <summary>Botão em linha do Telegram.</summary>
public sealed record TelegramButton(string Text, string CallbackData);

/// <summary>Cliente mínimo da Bot API do Telegram (implementado na infraestrutura).</summary>
public interface ITelegramClient
{
    Task SendMessageAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<TelegramButton>>? keyboard, CancellationToken cancellationToken);

    /// <summary>Substitui o texto de uma mensagem e remove os botões.</summary>
    Task EditMessageAsync(long chatId, long messageId, string text, CancellationToken cancellationToken);

    Task AnswerCallbackAsync(string callbackQueryId, CancellationToken cancellationToken);
}
