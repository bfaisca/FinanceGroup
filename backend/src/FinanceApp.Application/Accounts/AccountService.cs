using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Accounts;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Accounts;

/// <summary>Casos de uso de contas e saldos informados. Toda consulta é filtrada pelo usuário autenticado.</summary>
public sealed class AccountService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    BalanceProjectionService projection,
    IValidator<CreateAccountRequest> createValidator,
    IValidator<UpdateAccountRequest> updateValidator,
    IValidator<CreateBalanceSnapshotRequest> snapshotValidator)
{
    public async Task<IReadOnlyList<AccountDto>> ListAsync(bool includeInactive, DateOnly? asOf, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = db.Accounts.AsNoTracking().Where(a => a.UserId == userId);
        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        var accounts = await query.OrderBy(a => a.Name).ThenBy(a => a.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtosAsync(accounts, asOf ?? clock.Today, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountDto> GetAsync(Guid id, DateOnly? asOf, CancellationToken cancellationToken)
    {
        var account = await FindAsync(id, tracking: false, cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([account], asOf ?? clock.Today, cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;
        var today = clock.Today;

        var account = Account.Create(userId, request.Name, request.Institution, request.Type!.Value, now);
        await EnsureNameAvailableAsync(userId, account.Name, null, cancellationToken).ConfigureAwait(false);
        db.Accounts.Add(account);

        if (request.InitialBalance is { } balance)
        {
            var snapshot = account.RecordBalance(balance, request.BalanceReferenceDate ?? today, today, now);
            db.AccountBalanceSnapshots.Add(snapshot);
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([account], today, cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<AccountDto> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var account = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);

        account.Update(request.Name, request.Institution, request.Type!.Value, clock.UtcNow);
        await EnsureNameAvailableAsync(account.UserId, account.Name, account.Id, cancellationToken).ConfigureAwait(false);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([account], clock.Today, cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<AccountDto> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        account.Deactivate(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([account], clock.Today, cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<AccountDto> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await FindAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        account.Activate(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await ToDtosAsync([account], clock.Today, cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>Histórico de saldos informados, do mais recente para o mais antigo.</summary>
    public async Task<IReadOnlyList<BalanceSnapshotDto>> ListSnapshotsAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await FindAsync(id, tracking: false, cancellationToken).ConfigureAwait(false);
        return await db.AccountBalanceSnapshots
            .AsNoTracking()
            .Where(s => s.AccountId == account.Id)
            .OrderByDescending(s => s.ReferenceDate)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new BalanceSnapshotDto(s.Id, s.Balance, s.ReferenceDate, s.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BalanceSnapshotDto> CreateSnapshotAsync(Guid id, CreateBalanceSnapshotRequest request, CancellationToken cancellationToken)
    {
        await snapshotValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var account = await FindAsync(id, tracking: false, cancellationToken).ConfigureAwait(false);

        var snapshot = account.RecordBalance(request.Balance!.Value, request.ReferenceDate!.Value, clock.Today, clock.UtcNow);
        db.AccountBalanceSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BalanceSnapshotDto.From(snapshot);
    }

    private async Task<Account> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = tracking ? db.Accounts.AsTracking() : db.Accounts.AsNoTracking();
        return await query.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Conta não encontrada.");
    }

    private async Task EnsureNameAvailableAsync(Guid userId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
#pragma warning disable CA1862, CA1304, CA1311 // Traduzido para SQL lower(): o provedor só traduz ToLower() e não StringComparison.
        var lower = name.ToLowerInvariant();
        var exists = await db.Accounts.AnyAsync(
                a => a.UserId == userId && a.Id != exceptId && a.Name.ToLower() == lower, cancellationToken)
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
        new(ErrorCodes.Conflict, "Conflito com um registro existente.", "Já existe uma conta com este nome.");

    private async Task<IReadOnlyList<AccountDto>> ToDtosAsync(IReadOnlyList<Account> accounts, DateOnly asOf, CancellationToken cancellationToken)
    {
        var balances = await projection.GetAsync(accounts.Select(a => a.Id).ToList(), asOf, cancellationToken).ConfigureAwait(false);
        return accounts.Select(a =>
        {
            var info = balances[a.Id];
            return new AccountDto(
                a.Id,
                a.Name,
                a.Institution,
                a.Type,
                a.Currency,
                a.IsActive,
                info.Reference is { } r ? new LatestSnapshotDto(r.Balance, r.ReferenceDate) : null,
                info.ProjectedBalance,
                asOf,
                a.CreatedAt,
                a.UpdatedAt);
        }).ToList();
    }
}
