using System.Linq.Expressions;
using FinanceApp.Application.Common;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.Application.Transactions;

/// <summary>
/// Casos de uso de movimentações individuais e seus pagamentos. As regras de estado vivem no domínio
/// (<see cref="Transaction"/>); este serviço resolve referências do usuário, persiste e monta os DTOs.
/// Toda consulta é filtrada pelo usuário autenticado (recurso alheio → 404).
/// </summary>
public sealed class TransactionService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IValidator<TransactionListQuery> listValidator,
    IValidator<CreateTransactionRequest> createValidator,
    IValidator<UpdateTransactionRequest> updateValidator,
    IValidator<AddPaymentRequest> paymentValidator)
{
    public async Task<PagedResult<TransactionDto>> ListAsync(TransactionListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var today = clock.Today;

        var filtered = ApplyFilters(OwnQuery(userId), query, today);
        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await ApplySort(filtered, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToRows()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.Create(rows.Select(r => r.ToDto(today)).ToList(), query.Page, query.PageSize, total);
    }

    public Task<TransactionDetailDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        GetDetailAsync(id, cancellationToken);

    public async Task<TransactionDetailDto> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var type = request.Type!.Value;

        var category = await ResolveCategoryAsync(userId, request.CategoryId, type, cancellationToken).ConfigureAwait(false);
        var account = await ResolveAccountAsync(userId, request.AccountId, cancellationToken).ConfigureAwait(false);

        var transaction = Transaction.Create(
            userId,
            request.Description,
            type,
            request.Amount,
            category,
            account,
            request.PaymentMethod,
            request.Status ?? TransactionStatus.Pending,
            request.TransactionDate!.Value,
            request.DueDate,
            request.Notes,
            clock.UtcNow);

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(transaction.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TransactionDetailDto> UpdateAsync(Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var transaction = await LoadTrackedAsync(id, withReferences: true, cancellationToken).ConfigureAwait(false);

        var category = await ResolveCategoryAsync(userId, request.CategoryId, transaction.Type, cancellationToken).ConfigureAwait(false);
        var account = await ResolveAccountAsync(userId, request.AccountId, cancellationToken).ConfigureAwait(false);

        transaction.Update(
            userId,
            request.Description,
            request.Amount,
            category,
            account,
            request.PaymentMethod,
            request.TransactionDate!.Value,
            request.DueDate,
            request.Notes,
            request.Status,
            clock.UtcNow);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Exclui somente movimentações sem pagamentos (D-020); caso contrário 422.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await LoadTrackedAsync(id, withReferences: false, cancellationToken, includePayments: false).ConfigureAwait(false);
        transaction.EnsureCanBeDeleted();
        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TransactionDetailDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await LoadTrackedAsync(id, withReferences: false, cancellationToken, includePayments: false).ConfigureAwait(false);
        transaction.Cancel(currentUser.RequireUserId(), clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TransactionDetailDto> ReopenAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await LoadTrackedAsync(id, withReferences: false, cancellationToken).ConfigureAwait(false);
        transaction.Reopen(currentUser.RequireUserId(), clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Histórico (mais recente primeiro).</summary>
    public async Task<IReadOnlyList<TransactionHistoryEntryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var exists = await db.Transactions.AnyAsync(t => t.Id == id && t.CreatedByUserId == userId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            throw TransactionNotFound();
        }

        return await db.TransactionAuditEntries
            .AsNoTracking()
            .Where(e => e.TransactionId == id)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Sequence)
            .Select(e => new TransactionHistoryEntryDto(e.Id, e.Action, e.Field, e.OldValue, e.NewValue, e.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TransactionDetailDto> AddPaymentAsync(Guid id, AddPaymentRequest request, CancellationToken cancellationToken)
    {
        await paymentValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var transaction = await LoadTrackedAsync(id, withReferences: false, cancellationToken).ConfigureAwait(false);
        var account = await ResolveAccountAsync(userId, request.AccountId, cancellationToken).ConfigureAwait(false);

        var payment = transaction.AddPayment(
            userId, request.Amount, request.PaymentDate!.Value, clock.Today, account, request.PaymentMethod, clock.UtcNow);
        db.TransactionPayments.Add(payment);

        // Concorrência otimista (xmin): pagamentos simultâneos nunca ultrapassam o valor — o perdedor recebe 409.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TransactionDetailDto> RemovePaymentAsync(Guid id, Guid paymentId, CancellationToken cancellationToken)
    {
        var transaction = await LoadTrackedAsync(id, withReferences: false, cancellationToken).ConfigureAwait(false);
        var payment = transaction.Payments.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new NotFoundException("Pagamento não encontrado.");

        transaction.RemovePayment(currentUser.RequireUserId(), paymentId, clock.UtcNow);
        db.TransactionPayments.Remove(payment);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ consultas

    private IQueryable<Transaction> OwnQuery(Guid userId) =>
        db.Transactions.AsNoTracking().Where(t => t.CreatedByUserId == userId);

    private async Task<TransactionDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = clock.Today;

        var row = await OwnQuery(userId).Where(t => t.Id == id).ToRows().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw TransactionNotFound();

        var payments = await db.TransactionPayments
            .AsNoTracking()
            .Where(p => p.TransactionId == id)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedAt)
            .Select(p => new TransactionPaymentDto(
                p.Id, p.Amount, p.PaymentDate, p.AccountId, p.Account == null ? null : p.Account.Name, p.PaymentMethod, p.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return TransactionDetailDto.From(row.ToDto(today), payments);
    }

    private static IQueryable<Transaction> ApplyFilters(IQueryable<Transaction> query, TransactionListQuery filter, DateOnly today)
    {
        if (filter.From is { } from)
        {
            query = query.Where(t => t.TransactionDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(t => t.TransactionDate <= to);
        }

        if (filter.DueFrom is { } dueFrom)
        {
            query = query.Where(t => t.DueDate != null && t.DueDate >= dueFrom);
        }

        if (filter.DueTo is { } dueTo)
        {
            query = query.Where(t => t.DueDate != null && t.DueDate <= dueTo);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(t => t.Type == type);
        }

        if (filter.CategoryId is { } categoryId)
        {
            // Inclui subcategorias da categoria informada.
            query = query.Where(t => t.CategoryId == categoryId || t.Category!.ParentCategoryId == categoryId);
        }

        if (filter.AccountId is { } accountId)
        {
            query = query.Where(t => t.AccountId == accountId);
        }

        if (filter.Status is { } status)
        {
            query = status switch
            {
                // Overdue é calculado: persistido Planned/Pending (restante > 0) com vencimento anterior a hoje.
                TransactionStatus.Overdue => query.Where(t =>
                    (t.Status == TransactionStatus.Planned || t.Status == TransactionStatus.Pending)
                    && t.DueDate != null && t.DueDate < today),
                TransactionStatus.Planned or TransactionStatus.Pending => query.Where(t =>
                    t.Status == status && !(t.DueDate != null && t.DueDate < today)),
                _ => query.Where(t => t.Status == status),
            };
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // Traduzido para SQL (lower/strpos): o provedor só traduz ToLower() e não StringComparison.
            query = query.Where(t => t.Description.ToLower().Contains(term));
#pragma warning restore CA1311, CA1304, CA1862
        }

        return query;
    }

    private static IQueryable<Transaction> ApplySort(IQueryable<Transaction> query, string? sort)
    {
        var spec = string.IsNullOrWhiteSpace(sort) ? TransactionListQuery.DefaultSort : sort.Trim();
        var descending = spec.StartsWith('-');
        var field = spec.TrimStart('-').ToLowerInvariant();

        var ordered = field switch
        {
            "transactiondate" => Order(query, t => t.TransactionDate, descending),
            "duedate" => Order(query, t => t.DueDate, descending),
            "amount" => Order(query, t => t.Amount, descending),
            "description" => Order(query, t => t.Description, descending),
            "createdat" => Order(query, t => t.CreatedAt, descending),
            _ => Order(query, t => t.TransactionDate, descending: true),
        };

        // Desempate determinístico para paginação estável.
        return ordered.ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id);
    }

    private static IOrderedQueryable<Transaction> Order<TKey>(
        IQueryable<Transaction> query,
        Expression<Func<Transaction, TKey>> key,
        bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    // ------------------------------------------------------------------ referências do usuário

    private async Task<Transaction> LoadTrackedAsync(
        Guid id,
        bool withReferences,
        CancellationToken cancellationToken,
        bool includePayments = true)
    {
        var userId = currentUser.RequireUserId();
        IQueryable<Transaction> query = db.Transactions.AsTracking();
        if (includePayments)
        {
            query = query.Include(t => t.Payments);
        }

        if (withReferences)
        {
            query = query.Include(t => t.Category).Include(t => t.Account);
        }

        return await query.FirstOrDefaultAsync(t => t.Id == id && t.CreatedByUserId == userId, cancellationToken).ConfigureAwait(false)
            ?? throw TransactionNotFound();
    }

    private async Task<Category> ResolveCategoryAsync(Guid userId, Guid categoryId, TransactionType type, CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && (c.UserId == null || c.UserId == userId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ValidationException("categoryId", "Categoria não encontrada.");

        return category.Accepts(type)
            ? category
            : throw new ValidationException("categoryId", "A categoria deve ser do mesmo tipo da movimentação.");
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

    private static NotFoundException TransactionNotFound() => new("Movimentação não encontrada.");
}
