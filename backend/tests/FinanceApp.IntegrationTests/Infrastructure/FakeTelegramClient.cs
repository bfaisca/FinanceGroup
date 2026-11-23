using System.Collections.Concurrent;
using FinanceApp.Application.Telegram;

namespace FinanceApp.IntegrationTests.Infrastructure;

public sealed record SentTelegramMessage(long ChatId, string Text, IReadOnlyList<IReadOnlyList<TelegramButton>>? Keyboard, long? EditedMessageId);

/// <summary>Registra o que o bot enviaria ao Telegram (sem rede).</summary>
public sealed class FakeTelegramClient : ITelegramClient
{
    private readonly ConcurrentQueue<SentTelegramMessage> sent = new();

    public IReadOnlyCollection<SentTelegramMessage> Sent => sent;

    public SentTelegramMessage Last => sent.Last();

    public Task SendMessageAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<TelegramButton>>? keyboard, CancellationToken cancellationToken)
    {
        sent.Enqueue(new SentTelegramMessage(chatId, text, keyboard, null));
        return Task.CompletedTask;
    }

    public Task EditMessageAsync(long chatId, long messageId, string text, CancellationToken cancellationToken)
    {
        sent.Enqueue(new SentTelegramMessage(chatId, text, null, messageId));
        return Task.CompletedTask;
    }

    public Task AnswerCallbackAsync(string callbackQueryId, CancellationToken cancellationToken) => Task.CompletedTask;
}
