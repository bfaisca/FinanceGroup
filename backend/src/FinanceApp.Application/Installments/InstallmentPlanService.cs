using System.Linq.Expressions;
using FinanceApp.Application.Common;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.Application.Installments;

/// <summary>
/// Parcelamentos (D-026, contrato §12). O plano e todas as parcelas são criados na mesma transação
/// (um único <c>SaveChanges</c>); toda consulta é filtrada pelo usuário autenticado (recurso alheio → 404).
/// </summary>
public sealed class InstallmentPlanService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IValidator<InstallmentPlanListQuery> listValidator,
    IValidator<CreateInstallmentPlanRequest> createValidator)
{
    public async Task<PagedResult<InstallmentPlanDto>> ListAsync(InstallmentPlanListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();

        var plans = db.InstallmentPlans.AsNoTracking().Where(p => p.UserId == userId);
        if (query.Status is { } status)
        {
            plans = plans.WhereStatus(db, status);
        }

        var total = await plans.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = await ApplySort(plans, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToRows(db)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult.Create(page.Select(r => r.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public Task<InstallmentPlanDetailDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        GetDetailAsync(id, cancellationToken);

    public async Task<InstallmentPlanDetailDto> CreateAsync(CreateInstallmentPlanRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && (c.UserId == null || c.UserId == userId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ValidationException("categoryId", "Categoria não encontrada.");
        if (!category.Accepts(TransactionType.Expense))
        {
            throw new ValidationException("categoryId", "A categoria de um parcelamento deve ser de despesa.");
        }

        var account = request.AccountId is { } accountId
            ? await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken).ConfigureAwait(false)
                ?? throw new ValidationException("accountId", "Conta não encontrada.")
            : null;
        var card = request.CreditCardId is { } cardId
            ? await db.CreditCards.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId, cancellationToken).ConfigureAwait(false)
                ?? throw new ValidationException("creditCardId", "Cartão de crédito não encontrado.")
            : null;

        var (plan, installments) = InstallmentPlan.Create(
            userId,
            request.Description,
            request.TotalAmount,
            request.InstallmentCount,
            request.FirstDate!.Value,
            category,
            account,
            card,
            request.PaymentMethod,
            request.Notes,
            clock.Today,
            clock.UtcNow);

        db.InstallmentPlans.Add(plan);
        db.Transactions.AddRange(installments);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(plan.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Cancela as parcelas SEM pagamento que ainda não estão canceladas; as com pagamento permanecem (D-026).</summary>
    public async Task<InstallmentPlanDetailDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        await EnsureOwnedAsync(id, userId, cancellationToken).ConfigureAwait(false);

        var cancelable = await db.Transactions
            .AsTracking()
            .Where(t => t.InstallmentPlanId == id && t.CreatedByUserId == userId && t.GroupId == null && t.Status != TransactionStatus.Canceled && t.PaidAmount == 0)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = clock.UtcNow;
        foreach (var installment in cancelable)
        {
            installment.Cancel(userId, now);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Exclui o plano e as parcelas somente se NENHUMA parcela tiver pagamento (D-026); caso contrário, 422.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var plan = await db.InstallmentPlans.AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken).ConfigureAwait(false)
            ?? throw PlanNotFound();

        var installments = await db.Transactions.AsTracking()
            .Where(t => t.InstallmentPlanId == id && t.CreatedByUserId == userId && t.GroupId == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (installments.Any(t => t.PaidAmount > 0))
        {
            throw new BusinessRuleException(
                "Não é possível excluir um parcelamento com parcelas pagas. Cancele o parcelamento para preservar o histórico.");
        }

        db.Transactions.RemoveRange(installments);
        db.InstallmentPlans.Remove(plan);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ consultas

    private async Task<InstallmentPlanDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = clock.Today;

        var row = await db.InstallmentPlans.AsNoTracking()
            .Where(p => p.Id == id && p.UserId == userId)
            .ToRows(db)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw PlanNotFound();

        var installments = await db.Transactions.AsNoTracking()
            .Where(t => t.InstallmentPlanId == id && t.CreatedByUserId == userId && t.GroupId == null)
            .OrderBy(t => t.InstallmentNumber)
            .ToRows()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return InstallmentPlanDetailDto.From(row.ToDto(), installments.Select(r => r.ToDto(today)).ToList());
    }

    private async Task EnsureOwnedAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        if (!await db.InstallmentPlans.AnyAsync(p => p.Id == id && p.UserId == userId, cancellationToken).ConfigureAwait(false))
        {
            throw PlanNotFound();
        }
    }

    private static IQueryable<InstallmentPlan> ApplySort(IQueryable<InstallmentPlan> query, string? sort)
    {
        var spec = string.IsNullOrWhiteSpace(sort) ? InstallmentPlanListQuery.DefaultSort : sort.Trim();
        var descending = spec.StartsWith('-');
        var field = spec.TrimStart('-').ToLowerInvariant();

        var ordered = field switch
        {
            "firstdate" => Order(query, p => p.FirstDate, descending),
            "description" => Order(query, p => p.Description, descending),
            "totalamount" => Order(query, p => p.TotalAmount, descending),
            _ => Order(query, p => p.CreatedAt, descending),
        };

        return ordered.ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id);
    }

    private static IOrderedQueryable<InstallmentPlan> Order<TKey>(
        IQueryable<InstallmentPlan> query,
        Expression<Func<InstallmentPlan, TKey>> key,
        bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    private static NotFoundException PlanNotFound() => new("Parcelamento não encontrado.");
}
