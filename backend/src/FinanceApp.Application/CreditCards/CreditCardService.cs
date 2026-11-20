using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.CreditCards;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.Application.CreditCards;

/// <summary>Casos de uso de cartões de crédito (contrato §11). Toda consulta é filtrada pelo usuário autenticado.</summary>
public sealed class CreditCardService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    CreditCardSummaryService summaries,
    IValidator<CreateCreditCardRequest> createValidator,
    IValidator<UpdateCreditCardRequest> updateValidator)
{
    public async Task<IReadOnlyList<CreditCardDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = db.CreditCards.AsNoTracking().Include(c => c.Account).Where(c => c.UserId == userId);
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var cards = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtosAsync(cards, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CreditCardDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var card = await FindAsync(id, tracking: false, cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([card], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<CreditCardDto> CreateAsync(CreateCreditCardRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var account = await ResolveAccountAsync(userId, request.AccountId, cancellationToken).ConfigureAwait(false);

        var card = CreditCard.Create(
            userId, request.Name, request.Institution, request.CreditLimit!.Value, request.ClosingDay!.Value, request.DueDay!.Value, account, clock.UtcNow);
        await EnsureNameAvailableAsync(userId, card.Name, null, cancellationToken).ConfigureAwait(false);
        db.CreditCards.Add(card);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([card], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<CreditCardDto> UpdateAsync(Guid id, UpdateCreditCardRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var card = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        var account = await ResolveAccountAsync(userId, request.AccountId, cancellationToken).ConfigureAwait(false);

        card.Update(
            request.Name, request.Institution, request.CreditLimit!.Value, request.ClosingDay!.Value, request.DueDay!.Value, account, clock.UtcNow);
        await EnsureNameAvailableAsync(userId, card.Name, card.Id, cancellationToken).ConfigureAwait(false);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([card], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<CreditCardDto> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var card = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        card.Deactivate(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([card], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<CreditCardDto> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var card = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        card.Activate(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([card], cancellationToken).ConfigureAwait(false))[0];
    }

    private async Task<CreditCard> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = (tracking ? db.CreditCards.AsTracking() : db.CreditCards.AsNoTracking()).Include(c => c.Account);
        return await query.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Cartão de crédito não encontrado.");
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

    private async Task EnsureNameAvailableAsync(Guid userId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
#pragma warning disable CA1862, CA1304, CA1311 // Traduzido para SQL lower(): o provedor só traduz ToLower() e não StringComparison.
        var lower = name.ToLowerInvariant();
        var exists = await db.CreditCards.AnyAsync(
                c => c.UserId == userId && c.Id != exceptId && c.Name.ToLower() == lower, cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA1311, CA1304, CA1862
        if (exists)
        {
            throw DuplicateName();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException)
        {
            throw DuplicateName();
        }
    }

    private static ConflictException DuplicateName() =>
        new(ErrorCodes.Conflict, "Conflito com um registro existente.", "Já existe um cartão com este nome.");

    private async Task<IReadOnlyList<CreditCardDto>> ToDtosAsync(IReadOnlyList<CreditCard> cards, CancellationToken cancellationToken)
    {
        var summary = await summaries.GetAsync(cards, clock.Today, cancellationToken).ConfigureAwait(false);
        return cards.Select(c =>
        {
            var s = summary[c.Id];
            return new CreditCardDto(
                c.Id, c.Name, c.Institution, c.CreditLimit, c.ClosingDay, c.DueDay, c.AccountId, c.Account?.Name, c.IsActive,
                s.UsedLimit, s.AvailableLimit, s.OpenInvoice, c.CreatedAt, c.UpdatedAt);
        }).ToList();
    }
}
