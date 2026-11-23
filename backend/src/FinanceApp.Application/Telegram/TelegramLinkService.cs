using System.Security.Cryptography;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Domain.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceApp.Application.Telegram;

/// <summary>Vínculo entre a conta do usuário autenticado e o chat do Telegram (D-053). O usuário vem sempre do token.</summary>
public sealed class TelegramLinkService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ISecureTokenService tokens,
    IOptions<TelegramOptions> options)
{
    public const int CodeLength = 8;
    public const int CodeMinutes = 10;

    // Sem 0/O/1/I/L para evitar confusão ao digitar.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public async Task<TelegramLinkCodeDto> CreateCodeAsync(CancellationToken cancellationToken)
    {
        var opt = options.Value;
        if (!opt.Enabled)
        {
            throw new BusinessRuleException("O bot do Telegram não está habilitado neste ambiente.");
        }

        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        // Um código ativo por usuário: gerar outro invalida o anterior.
        var previous = await db.TelegramLinkCodes.Where(c => c.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.TelegramLinkCodes.RemoveRange(previous);

        var code = RandomNumberGenerator.GetString(Alphabet, CodeLength);
        var entity = TelegramLinkCode.Create(userId, tokens.Hash(code), now.AddMinutes(CodeMinutes), now);
        db.TelegramLinkCodes.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var bot = opt.BotUsername!.TrimStart('@');
        return new TelegramLinkCodeDto(code, entity.ExpiresAt, bot, $"https://t.me/{bot}?start={code}");
    }

    public async Task<TelegramLinkStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var opt = options.Value;
        var userId = currentUser.RequireUserId();
        var link = await db.TelegramLinks.Where(l => l.UserId == userId).Select(l => (DateTimeOffset?)l.CreatedAt).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return new TelegramLinkStatusDto(opt.Enabled, link is not null, link, opt.Enabled ? opt.BotUsername?.TrimStart('@') : null);
    }

    public async Task UnlinkAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        await RemoveAllForUserAsync(db, userId, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Remove vínculo, códigos e despesas pendentes do usuário (desvincular e exclusão de conta). Não salva.</summary>
    public static async Task RemoveAllForUserAsync(IAppDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        db.TelegramLinks.RemoveRange(await db.TelegramLinks.Where(l => l.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        db.TelegramLinkCodes.RemoveRange(await db.TelegramLinkCodes.Where(c => c.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        db.TelegramPendingExpenses.RemoveRange(await db.TelegramPendingExpenses.Where(p => p.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
    }
}
