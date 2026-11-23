using System.Globalization;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Telegram;
using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceApp.Application.Telegram;

/// <summary>
/// Bot do Telegram (D-053/D-054): lança despesas individuais a partir de texto simples. A identidade vem do vínculo chat → conta;
/// a despesa é criada pelo mesmo <see cref="TransactionService"/> da API (mesmas regras e validações), sem conta, cartão ou grupo.
/// </summary>
public sealed partial class TelegramBotService(
    IAppDbContext db,
    TransactionService transactions,
    CurrentUserOverride userOverride,
    ITelegramClient client,
    IDateTimeProvider clock,
    ISecureTokenService tokens,
    IOptions<TelegramOptions> options,
    ILogger<TelegramBotService> logger)
{
    private readonly ILogger<TelegramBotService> logger = logger;

    public static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(15);
    private const int CategoriesPerRow = 2;
    private const string CreatePrefix = "c:";
    private const string CancelPrefix = "x:";

    private const string HelpText =
        "Eu lanço despesas no GroupFinance.\n\n" +
        "Envie o valor e a descrição, por exemplo:\n" +
        "• 45,90 mercado\n" +
        "• mercado 45,90\n" +
        "• /despesa 1.234,56 aluguel\n\n" +
        "Depois escolha a categoria nos botões. A despesa é lançada na data de hoje, como pendente e sem conta vinculada.\n\n" +
        "Comandos: /ajuda, /vincular CÓDIGO, /desvincular.";

    public async Task HandleAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        try
        {
            if (update.CallbackQuery is { } callback)
            {
                await HandleCallbackAsync(callback, cancellationToken).ConfigureAwait(false);
            }
            else if (update.Message is { Chat: { } chat } message && string.Equals(chat.Type, "private", StringComparison.Ordinal))
            {
                await HandleMessageAsync(chat.Id, message.Text, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // O Telegram reenviaria o update em caso de erro HTTP; registramos sem conteúdo da mensagem e respondemos 200.
            LogUpdateFailed(ex.GetType().Name, update.UpdateId);
        }
    }

    private async Task HandleMessageAsync(long chatId, string? text, CancellationToken cancellationToken)
    {
        var value = (text ?? string.Empty).Trim();
        var command = FirstWord(value).ToLowerInvariant();
        var argument = value.Length > command.Length ? value[command.Length..].Trim() : string.Empty;
        var at = command.IndexOf('@', StringComparison.Ordinal);
        if (at > 0)
        {
            command = command[..at];
        }

        switch (command)
        {
            case "/start" when argument.Length > 0:
            case "/vincular":
                await LinkAsync(chatId, argument, cancellationToken).ConfigureAwait(false);
                return;
            case "/start":
            case "/ajuda":
            case "/help":
                await client.SendMessageAsync(chatId, HelpText, null, cancellationToken).ConfigureAwait(false);
                return;
            case "/desvincular":
                await UnlinkAsync(chatId, cancellationToken).ConfigureAwait(false);
                return;
        }

        var link = await FindLinkAsync(chatId, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            await client.SendMessageAsync(chatId, "Este chat ainda não está vinculado. No site, abra Configurações → Telegram, gere um código e envie aqui: /vincular CÓDIGO.", null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var parsed = TelegramExpenseParser.Parse(value);
        if (parsed is null)
        {
            await client.SendMessageAsync(chatId, "Não entendi. Envie o valor e a descrição com um único valor, por exemplo: 45,90 mercado.", null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var now = clock.UtcNow;
        TelegramPendingExpense pending;
        try
        {
            pending = TelegramPendingExpense.Create(link.UserId, parsed.Description, parsed.Amount, now);
        }
        catch (Domain.Common.DomainException ex)
        {
            await client.SendMessageAsync(chatId, ex.Message, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var categories = await ExpenseCategoriesAsync(link.UserId, cancellationToken).ConfigureAwait(false);
        if (categories.Count == 0)
        {
            await client.SendMessageAsync(chatId, "Você não tem categorias de despesa ativas. Cadastre uma no site.", null, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Limpeza oportunista das despesas pendentes vencidas (de qualquer usuário).
        var cutoff = now - PendingLifetime;
        db.TelegramPendingExpenses.RemoveRange(await db.TelegramPendingExpenses.Where(p => p.CreatedAt < cutoff).ToListAsync(cancellationToken).ConfigureAwait(false));
        db.TelegramPendingExpenses.Add(pending);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var buttons = categories
            .Select(c => new TelegramButton(c.Name, $"{CreatePrefix}{pending.Id}:{c.Id:N}"))
            .Chunk(CategoriesPerRow)
            .Select(row => (IReadOnlyList<TelegramButton>)row)
            .ToList();
        buttons.Add([new TelegramButton("Cancelar", $"{CancelPrefix}{pending.Id}")]);

        await client.SendMessageAsync(
            chatId,
            $"Despesa de {Money(pending.Amount)}: {pending.Description}\nEscolha a categoria:",
            buttons,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleCallbackAsync(TelegramCallbackQuery callback, CancellationToken cancellationToken)
    {
        await client.AnswerCallbackAsync(callback.Id, cancellationToken).ConfigureAwait(false);
        var chatId = callback.Message?.Chat?.Id;
        var messageId = callback.Message?.MessageId;
        var data = callback.Data ?? string.Empty;
        if (chatId is null || messageId is null)
        {
            return;
        }

        var link = await FindLinkAsync(chatId.Value, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            await client.EditMessageAsync(chatId.Value, messageId.Value, "Este chat não está vinculado.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (data.StartsWith(CancelPrefix, StringComparison.Ordinal) && long.TryParse(data[CancelPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var cancelId))
        {
            db.TelegramPendingExpenses.RemoveRange(await db.TelegramPendingExpenses.Where(p => p.Id == cancelId && p.UserId == link.UserId).ToListAsync(cancellationToken).ConfigureAwait(false));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await client.EditMessageAsync(chatId.Value, messageId.Value, "Lançamento cancelado.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var parts = data.Split(':');
        if (parts.Length != 3 || parts[0] != "c" || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pendingId) || !Guid.TryParseExact(parts[2], "N", out var categoryId))
        {
            return;
        }

        var pending = await db.TelegramPendingExpenses.FirstOrDefaultAsync(p => p.Id == pendingId && p.UserId == link.UserId, cancellationToken).ConfigureAwait(false);
        if (pending is null || clock.UtcNow - pending.CreatedAt > PendingLifetime)
        {
            await client.EditMessageAsync(chatId.Value, messageId.Value, "Este lançamento expirou. Envie a despesa novamente.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var category = await db.Categories
            .Where(c => c.Id == categoryId && c.IsActive && c.Type == CategoryType.Expense && (c.UserId == link.UserId || c.UserId == null))
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
        {
            await client.EditMessageAsync(chatId.Value, messageId.Value, "Categoria indisponível. Envie a despesa novamente.", cancellationToken).ConfigureAwait(false);
            return;
        }

        // O único ponto em que a identidade do escopo é definida pelo servidor, a partir do vínculo verificado.
        userOverride.UserId = link.UserId;
        try
        {
            await transactions.CreateAsync(
                new CreateTransactionRequest
                {
                    Description = pending.Description,
                    Type = TransactionType.Expense,
                    Amount = pending.Amount,
                    CategoryId = categoryId,
                    TransactionDate = clock.Today,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (AppException ex)
        {
            await client.EditMessageAsync(chatId.Value, messageId.Value, $"Não foi possível lançar: {ex.Title}", cancellationToken).ConfigureAwait(false);
            return;
        }
        finally
        {
            userOverride.UserId = null;
        }

        db.TelegramPendingExpenses.Remove(pending);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await client.EditMessageAsync(
            chatId.Value,
            messageId.Value,
            $"✅ Despesa lançada: {pending.Description} — {Money(pending.Amount)} ({category}), hoje, pendente.",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task LinkAsync(long chatId, string rawCode, CancellationToken cancellationToken)
    {
        var code = new string(rawCode.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (code.Length != TelegramLinkService.CodeLength)
        {
            await client.SendMessageAsync(chatId, "Envie o código gerado no site: /vincular CÓDIGO.", null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var now = clock.UtcNow;
        var hash = tokens.Hash(code);
        var entry = await db.TelegramLinkCodes.AsTracking().FirstOrDefaultAsync(c => c.CodeHash == hash, cancellationToken).ConfigureAwait(false);
        if (entry is null || entry.IsExpired(now))
        {
            await client.SendMessageAsync(chatId, "Código inválido ou expirado. Gere um novo em Configurações → Telegram.", null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var otherOwner = await db.TelegramLinks.AnyAsync(l => l.ChatId == chatId && l.UserId != entry.UserId, cancellationToken).ConfigureAwait(false);
        if (otherOwner)
        {
            await client.SendMessageAsync(chatId, "Este chat já está vinculado a outra conta. Envie /desvincular antes.", null, cancellationToken).ConfigureAwait(false);
            return;
        }

        var existing = await db.TelegramLinks.AsTracking().FirstOrDefaultAsync(l => l.UserId == entry.UserId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            db.TelegramLinks.Add(TelegramLink.Create(entry.UserId, chatId, now));
        }
        else
        {
            existing.MoveToChat(chatId, now);
        }

        db.TelegramLinkCodes.Remove(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await client.SendMessageAsync(chatId, "✅ Conta vinculada. Agora é só enviar o valor e a descrição, por exemplo: 45,90 mercado.", null, cancellationToken).ConfigureAwait(false);
    }

    private async Task UnlinkAsync(long chatId, CancellationToken cancellationToken)
    {
        var link = await FindLinkAsync(chatId, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            await client.SendMessageAsync(chatId, "Este chat não está vinculado.", null, cancellationToken).ConfigureAwait(false);
            return;
        }

        await TelegramLinkService.RemoveAllForUserAsync(db, link.UserId, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await client.SendMessageAsync(chatId, "Vínculo removido. Para voltar a usar, gere um novo código no site.", null, cancellationToken).ConfigureAwait(false);
    }

    private Task<TelegramLink?> FindLinkAsync(long chatId, CancellationToken cancellationToken) =>
        db.TelegramLinks.FirstOrDefaultAsync(l => l.ChatId == chatId, cancellationToken);

    private async Task<List<(Guid Id, string Name)>> ExpenseCategoriesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.Categories
            .Where(c => c.IsActive && c.Type == CategoryType.Expense && c.ParentCategoryId == null && (c.UserId == userId || c.UserId == null))
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(c => (c.Id, c.Name)).ToList();
    }

    private static string FirstWord(string value)
    {
        var space = value.IndexOfAny([' ', '\n', '\t']);
        return space < 0 ? value : value[..space];
    }

    private static string Money(decimal amount) => "R$ " + amount.ToString("N2", new CultureInfo("pt-BR"));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao processar update do Telegram ({ExceptionType}), update {UpdateId}.")]
    private partial void LogUpdateFailed(string exceptionType, long updateId);
}
