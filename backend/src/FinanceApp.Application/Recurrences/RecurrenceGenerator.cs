using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceApp.Application.Recurrences;

/// <summary>Implementação de <see cref="IRecurrenceGenerator"/>. Os logs trazem apenas identificadores (sem dados financeiros).</summary>
public sealed partial class RecurrenceGenerator(IAppDbContext db, IDateTimeProvider clock, ILogger<RecurrenceGenerator> logger)
    : IRecurrenceGenerator
{
    private const int MaxRaceAttempts = 3;

    private readonly ILogger<RecurrenceGenerator> logger = logger;

    public async Task<int> GenerateAsync(Guid ruleId, DateOnly today, int horizonMonths, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxRaceAttempts; attempt++)
        {
            var rule = await db.RecurrenceRules
                .AsNoTracking()
                .Include(r => r.Category)
                .Include(r => r.Account)
                .Include(r => r.CreditCard)
                .FirstOrDefaultAsync(r => r.Id == ruleId && r.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (rule is null)
            {
                return 0;
            }

            var created = await PrepareAsync(rule, today, horizonMonths, null, cancellationToken).ConfigureAwait(false);
            if (created.Count == 0)
            {
                return 0;
            }

            db.Transactions.AddRange(created);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                LogGenerated(ruleId, created.Count);
                return created.Count;
            }
            catch (UniqueConstraintViolationException)
            {
                // Outra execução gravou as mesmas ocorrências ao mesmo tempo ("já existe"): descarta e recalcula o que falta.
                db.ChangeTracker.Clear();
                LogRaceDetected(ruleId, attempt);
            }
        }

        return 0;
    }

    public async Task<int> GenerateAllAsync(DateOnly today, int horizonMonths, CancellationToken cancellationToken)
    {
        var ruleIds = await db.RecurrenceRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = 0;
        foreach (var ruleId in ruleIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                total += await GenerateAsync(ruleId, today, horizonMonths, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Uma regra com problema não pode impedir as demais.
                db.ChangeTracker.Clear();
                LogRuleFailed(ex, ruleId);
            }
        }

        return total;
    }

    public async Task<IReadOnlyList<Transaction>> PrepareAsync(
        RecurrenceRule rule,
        DateOnly today,
        int horizonMonths,
        IReadOnlySet<DateOnly>? ignoredExistingDates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!rule.IsActive)
        {
            return [];
        }

        var horizon = today.AddMonths(horizonMonths);
        var existing = (await db.Transactions
                .AsNoTracking()
                .Where(t => t.RecurrenceRuleId == rule.Id)
                .Select(t => t.OccurrenceDate!.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();
        if (ignoredExistingDates is not null)
        {
            existing.ExceptWith(ignoredExistingDates);
        }

        var missing = rule.GetOccurrenceDates(horizon).Where(d => !existing.Contains(d)).ToList();
        if (missing.Count == 0)
        {
            return [];
        }

        var category = rule.Category
            ?? await db.Categories.AsNoTracking().FirstAsync(c => c.Id == rule.CategoryId, cancellationToken).ConfigureAwait(false);
        var account = rule.Account;
        if (account is null && rule.AccountId is { } accountId)
        {
            account = await db.Accounts.AsNoTracking().FirstAsync(a => a.Id == accountId, cancellationToken).ConfigureAwait(false);
        }

        var card = rule.CreditCard;
        if (card is null && rule.CreditCardId is { } cardId)
        {
            card = await db.CreditCards.AsNoTracking().FirstAsync(c => c.Id == cardId, cancellationToken).ConfigureAwait(false);
        }

        var now = clock.UtcNow;
        var created = new List<Transaction>(missing.Count);
        try
        {
            foreach (var date in missing)
            {
                created.Add(rule.CreateOccurrence(date, today, category, account, card, now));
            }
        }
        catch (DomainException ex)
        {
            // Categoria/conta/cartão inativos: a geração é ignorada e registrada (D-027).
            LogSkipped(rule.Id, ex.Message);
            return [];
        }

        return created;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recorrência {RuleId}: {Count} ocorrências geradas.")]
    private partial void LogGenerated(Guid ruleId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recorrência {RuleId}: ocorrências já criadas por outra execução (tentativa {Attempt}); recalculando.")]
    private partial void LogRaceDetected(Guid ruleId, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recorrência {RuleId}: geração ignorada ({Reason}).")]
    private partial void LogSkipped(Guid ruleId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Recorrência {RuleId}: falha ao gerar ocorrências.")]
    private partial void LogRuleFailed(Exception exception, Guid ruleId);
}
