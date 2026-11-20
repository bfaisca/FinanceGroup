using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.Application.Recurrences;

/// <summary>
/// Casos de uso de recorrências (D-027, contrato §13). Criação, edição, ativação e desativação regeneram as ocorrências
/// futuras na MESMA transação (um único <c>SaveChanges</c>). Toda consulta é filtrada pelo usuário autenticado.
/// </summary>
public sealed class RecurrenceService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IRecurrenceGenerator generator,
    IOptions<RecurrenceOptions> options,
    IValidator<CreateRecurrenceRequest> createValidator,
    IValidator<UpdateRecurrenceRequest> updateValidator)
{
    public async Task<IReadOnlyList<RecurrenceDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = WithReferences(db.RecurrenceRules.AsNoTracking()).Where(r => r.UserId == userId);
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        var rules = await query.OrderBy(r => r.Description).ThenBy(r => r.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtosAsync(rules, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RecurrenceDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(id, tracking: false, cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([rule], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<RecurrenceDto> CreateAsync(CreateRecurrenceRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var type = request.Type!.Value;

        var category = await ResolveCategoryAsync(userId, request.CategoryId, type, cancellationToken).ConfigureAwait(false);
        var account = await ResolveAccountAsync(userId, request.AccountId, cancellationToken).ConfigureAwait(false);
        var card = await ResolveCardAsync(userId, request.CreditCardId, cancellationToken).ConfigureAwait(false);

        var rule = RecurrenceRule.Create(
            userId,
            request.Description,
            type,
            request.Amount,
            category,
            account,
            card,
            request.PaymentMethod,
            request.Notes,
            request.Frequency!.Value,
            request.Interval ?? 1,
            request.StartDate!.Value,
            request.EndDate,
            clock.UtcNow);

        db.RecurrenceRules.Add(rule);
        var occurrences = await generator
            .PrepareAsync(rule, clock.Today, options.Value.HorizonMonths, null, cancellationToken)
            .ConfigureAwait(false);
        db.Transactions.AddRange(occurrences);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await ToDtosAsync([rule], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>Atualiza a regra e, se ativa, regenera as ocorrências futuras (D-027) na mesma transação.</summary>
    public async Task<RecurrenceDto> UpdateAsync(Guid id, UpdateRecurrenceRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var rule = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);

        if (request.EndDate is { } endDate && endDate < rule.StartDate)
        {
            throw new ValidationException("endDate", "A data final não pode ser anterior à data inicial.");
        }

        var category = await ResolveCategoryAsync(userId, request.CategoryId, rule.Type, cancellationToken).ConfigureAwait(false);
        var account = await ResolveAccountAsync(userId, request.AccountId, cancellationToken).ConfigureAwait(false);
        var card = await ResolveCardAsync(userId, request.CreditCardId, cancellationToken).ConfigureAwait(false);
        if (card is not null && rule.Type != TransactionType.Expense)
        {
            throw new ValidationException("creditCardId", "Somente despesas podem ser lançadas no cartão de crédito.");
        }

        var before = Fingerprint(rule);

        rule.Update(
            request.Description,
            request.Amount,
            category,
            account,
            card,
            request.PaymentMethod,
            request.Notes,
            request.Frequency!.Value,
            request.Interval ?? 1,
            request.EndDate,
            clock.UtcNow);

        // Sem alteração real na regra, as ocorrências futuras permanecem como estão (ids estáveis).
        if (rule.IsActive && !Fingerprint(rule).Equals(before))
        {
            await RegenerateAsync(rule, cancellationToken).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([rule], cancellationToken).ConfigureAwait(false))[0];
    }

    private static (string, decimal, Guid, Guid?, Guid?, PaymentMethod?, string?, RecurrenceFrequency, int, DateOnly?) Fingerprint(RecurrenceRule rule) =>
        (rule.Description, rule.Amount, rule.CategoryId, rule.AccountId, rule.CreditCardId, rule.PaymentMethod, rule.Notes, rule.Frequency, rule.Interval, rule.EndDate);

    /// <summary>Desativa a regra e remove as ocorrências futuras elegíveis (D-027); as demais permanecem.</summary>
    public async Task<RecurrenceDto> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        if (rule.IsActive)
        {
            rule.Deactivate(clock.UtcNow);
            db.Transactions.RemoveRange(await LoadRemovableAsync(rule.Id, cancellationToken).ConfigureAwait(false));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return (await ToDtosAsync([rule], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>Reativa a regra e gera as ocorrências que faltam até o horizonte.</summary>
    public async Task<RecurrenceDto> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        if (!rule.IsActive)
        {
            var category = rule.Category ?? throw new InvalidOperationException("Categoria da recorrência não carregada.");
            rule.Activate(category, rule.Account, rule.CreditCard, clock.UtcNow);
            var occurrences = await generator
                .PrepareAsync(rule, clock.Today, options.Value.HorizonMonths, null, cancellationToken)
                .ConfigureAwait(false);
            db.Transactions.AddRange(occurrences);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return (await ToDtosAsync([rule], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// Remove as ocorrências futuras (<c>transactionDate &gt; hoje</c>) em Planned/Pending, sem pagamentos e nunca editadas
    /// à mão, e gera de novo conforme a regra atual. Pagas, vencidas, canceladas e editadas à mão permanecem.
    /// </summary>
    private async Task RegenerateAsync(RecurrenceRule rule, CancellationToken cancellationToken)
    {
        var removable = await LoadRemovableAsync(rule.Id, cancellationToken).ConfigureAwait(false);
        db.Transactions.RemoveRange(removable);

        var removedDates = removable.Where(t => t.OccurrenceDate is not null).Select(t => t.OccurrenceDate!.Value).ToHashSet();
        var occurrences = await generator
            .PrepareAsync(rule, clock.Today, options.Value.HorizonMonths, removedDates, cancellationToken)
            .ConfigureAwait(false);
        db.Transactions.AddRange(occurrences);
    }

    private async Task<List<Transaction>> LoadRemovableAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        var today = clock.Today;
        return await db.Transactions
            .AsTracking()
            .Where(t => t.RecurrenceRuleId == ruleId && t.GroupId == null
                        && t.TransactionDate > today
                        && (t.Status == TransactionStatus.Planned || t.Status == TransactionStatus.Pending)
                        && t.PaidAmount == 0
                        && !t.IsManuallyEdited)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ consultas e referências

    private static IQueryable<RecurrenceRule> WithReferences(IQueryable<RecurrenceRule> query) =>
        query.Include(r => r.Category).Include(r => r.Account).Include(r => r.CreditCard);

    private async Task<RecurrenceRule> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = WithReferences(tracking ? db.RecurrenceRules.AsTracking() : db.RecurrenceRules.AsNoTracking());
        return await query.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Recorrência não encontrada.");
    }

    private async Task<Category> ResolveCategoryAsync(Guid userId, Guid categoryId, TransactionType type, CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && (c.UserId == null || c.UserId == userId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ValidationException("categoryId", "Categoria não encontrada.");

        return category.Accepts(type)
            ? category
            : throw new ValidationException("categoryId", "A categoria deve ser do mesmo tipo da recorrência.");
    }

    private async Task<Account?> ResolveAccountAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        if (accountId is not { } id)
        {
            return null;
        }

        return await db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken).ConfigureAwait(false)
            ?? throw new ValidationException("accountId", "Conta não encontrada.");
    }

    private async Task<CreditCard?> ResolveCardAsync(Guid userId, Guid? creditCardId, CancellationToken cancellationToken)
    {
        if (creditCardId is not { } id)
        {
            return null;
        }

        return await db.CreditCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken).ConfigureAwait(false)
            ?? throw new ValidationException("creditCardId", "Cartão de crédito não encontrado.");
    }

    private async Task<IReadOnlyList<RecurrenceDto>> ToDtosAsync(IReadOnlyList<RecurrenceRule> rules, CancellationToken cancellationToken)
    {
        var derived = await db.LoadDerivedAsync(rules, clock.Today, cancellationToken).ConfigureAwait(false);
        return rules.Select(r =>
        {
            var d = derived[r.Id];
            return new RecurrenceDto(
                r.Id, r.Description, r.Type, r.Amount, r.CategoryId, r.Category!.Name, r.AccountId, r.Account?.Name, r.CreditCardId,
                r.CreditCard?.Name, r.PaymentMethod, r.Notes, r.Frequency, r.Interval, r.StartDate, r.EndDate, r.IsActive,
                d.NextOccurrenceDate, d.GeneratedCount, r.CreatedAt, r.UpdatedAt);
        }).ToList();
    }
}
