using FinanceApp.Application.Common;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Notifications;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.Application.Groups;

/// <summary>
/// Movimentações de grupo (contrato §16.2). Reaproveita a máquina de estados/pagamentos de <see cref="Transaction"/>;
/// este serviço apenas resolve o participante autenticado (404 se não for ativo), aplica o grupo arquivado (422), carrega
/// referências e monta DTOs SEM dados privados (conta/cartão). Autoria: somente o criador altera (403 aos demais).
/// </summary>
public sealed class GroupTransactionService(
    IAppDbContext db,
    IDateTimeProvider clock,
    GroupAccess access,
    IValidator<GroupTransactionListQuery> listValidator,
    IValidator<CreateGroupTransactionRequest> createValidator,
    IValidator<UpdateGroupTransactionRequest> updateValidator,
    IValidator<AddPaymentRequest> paymentValidator)
{
    public async Task<PagedResult<GroupTransactionDto>> ListAsync(Guid groupId, GroupTransactionListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken).ConfigureAwait(false);
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        var today = clock.Today;

        var filtered = ApplyFilters(GroupQuery(groupId), query, today);
        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await filtered.ApplyContractSort(query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToGroupRows()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await access.LoadUserNamesAsync(rows.Select(r => r.CreatedByUserId), cancellationToken).ConfigureAwait(false);
        var items = rows.Select(r => r.ToDto(today, membership, names)).ToList();
        return PagedResult.Create(items, query.Page, query.PageSize, total);
    }

    public async Task<GroupTransactionDetailDto> GetAsync(Guid groupId, Guid transactionId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(membership, transactionId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupTransactionDetailDto> CreateAsync(Guid groupId, CreateGroupTransactionRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        membership.EnsureWritable();
        var type = request.Type!.Value;

        var category = await ResolveCategoryAsync(membership.UserId, request.CategoryId, type, cancellationToken).ConfigureAwait(false);
        var transaction = Transaction.CreateForGroup(
            groupId,
            membership.UserId,
            request.Description,
            type,
            request.Amount,
            category,
            request.PaymentMethod,
            request.Status ?? TransactionStatus.Pending,
            request.TransactionDate!.Value,
            request.DueDate,
            request.Notes,
            clock.UtcNow);

        db.Transactions.Add(transaction);
        // D-042a: notifica os demais membros ativos na mesma transação da criação.
        await NotificationRecorder.AddTransactionCreatedAsync(db, transaction, groupId, membership.UserId, clock.UtcNow, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(membership, transaction.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupTransactionDetailDto> UpdateAsync(
        Guid groupId,
        Guid transactionId,
        UpdateGroupTransactionRequest request,
        CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        var transaction = await LoadTrackedAsync(groupId, transactionId, cancellationToken).ConfigureAwait(false);
        transaction.EnsureCanBeChangedBy(membership.UserId);
        membership.EnsureWritable();

        var category = await ResolveCategoryAsync(membership.UserId, request.CategoryId, transaction.Type, cancellationToken).ConfigureAwait(false);
        transaction.Update(
            membership.UserId,
            request.Description,
            request.Amount,
            category,
            account: null,
            request.PaymentMethod,
            request.TransactionDate!.Value,
            request.DueDate,
            request.Notes,
            request.Status,
            clock.UtcNow);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(membership, transactionId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Somente o criador e somente sem pagamentos (D-020/D-032); com pagamentos: 422 (cancele).</summary>
    public async Task DeleteAsync(Guid groupId, Guid transactionId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        var transaction = await LoadTrackedAsync(groupId, transactionId, cancellationToken).ConfigureAwait(false);
        transaction.EnsureCanBeChangedBy(membership.UserId);
        membership.EnsureWritable();

        transaction.EnsureCanBeDeleted(membership.UserId);
        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupTransactionDetailDto> CancelAsync(Guid groupId, Guid transactionId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        var transaction = await LoadTrackedAsync(groupId, transactionId, cancellationToken).ConfigureAwait(false);
        transaction.EnsureCanBeChangedBy(membership.UserId);
        membership.EnsureWritable();

        transaction.Cancel(membership.UserId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(membership, transactionId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GroupTransactionDetailDto> ReopenAsync(Guid groupId, Guid transactionId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        var transaction = await LoadTrackedAsync(groupId, transactionId, cancellationToken).ConfigureAwait(false);
        transaction.EnsureCanBeChangedBy(membership.UserId);
        membership.EnsureWritable();

        transaction.Reopen(membership.UserId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(membership, transactionId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Histórico com o nome de quem agiu (mais recente primeiro).</summary>
    public async Task<IReadOnlyList<GroupTransactionHistoryEntryDto>> GetHistoryAsync(Guid groupId, Guid transactionId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        var exists = await GroupQuery(groupId).AnyAsync(t => t.Id == transactionId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            throw TransactionNotFound();
        }

        var entries = await db.TransactionAuditEntries
            .AsNoTracking()
            .Where(e => e.TransactionId == transactionId)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await access.LoadUserNamesAsync(entries.Select(e => e.UserId), cancellationToken).ConfigureAwait(false);
        return entries
            .Select(e => new GroupTransactionHistoryEntryDto(
                e.Id, e.Action, e.Field, e.OldValue, e.NewValue, names.GetValueOrDefault(e.UserId, User.AnonymizedName), e.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Qualquer participante ativo registra o PRÓPRIO pagamento/recebimento (D-032). O <c>accountId</c> opcional deve ser conta
    /// do próprio pagador e nunca é devolvido: serve apenas ao saldo projetado individual (D-033).
    /// </summary>
    public async Task<GroupTransactionDetailDto> AddPaymentAsync(
        Guid groupId,
        Guid transactionId,
        AddPaymentRequest request,
        CancellationToken cancellationToken)
    {
        await paymentValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        var transaction = await LoadTrackedAsync(groupId, transactionId, cancellationToken).ConfigureAwait(false);
        membership.EnsureWritable();
        var account = await ResolvePayerAccountAsync(membership.UserId, request.AccountId, cancellationToken).ConfigureAwait(false);

        var payment = transaction.AddPayment(
            membership.UserId, request.Amount, request.PaymentDate!.Value, clock.Today, account, request.PaymentMethod, clock.UtcNow);
        db.TransactionPayments.Add(payment);
        // D-042b: notifica os demais membros ativos (valor = o do pagamento) na mesma transação do pagamento.
        await NotificationRecorder.AddPaymentRegisteredAsync(db, transaction, groupId, membership.UserId, payment.Amount, clock.UtcNow, cancellationToken).ConfigureAwait(false);

        // Concorrência otimista (xmin): pagamentos simultâneos de membros diferentes nunca ultrapassam o valor (o perdedor recebe 409).
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(membership, transactionId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Somente o próprio pagador remove o seu pagamento (403 aos demais), e com o grupo ativo.</summary>
    public async Task<GroupTransactionDetailDto> RemovePaymentAsync(
        Guid groupId,
        Guid transactionId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var membership = await access.RequireMembershipAsync(groupId, cancellationToken).ConfigureAwait(false);
        var transaction = await LoadTrackedAsync(groupId, transactionId, cancellationToken).ConfigureAwait(false);
        var payment = transaction.Payments.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new NotFoundException("Pagamento não encontrado.");
        if (payment.CreatedByUserId != membership.UserId)
        {
            throw new ForbiddenException(detail: "Somente quem registrou o pagamento pode removê-lo.");
        }

        membership.EnsureWritable();
        transaction.RemovePayment(membership.UserId, paymentId, clock.UtcNow);
        db.TransactionPayments.Remove(payment);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDetailAsync(membership, transactionId, cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ consultas

    /// <summary>Base de toda consulta: movimentações DO grupo (um registro por movimentação, D-032).</summary>
    private IQueryable<Transaction> GroupQuery(Guid groupId) =>
        db.Transactions.AsNoTracking().Where(t => t.GroupId == groupId);

    private async Task<GroupTransactionDetailDto> GetDetailAsync(GroupMembership membership, Guid transactionId, CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var row = await GroupQuery(membership.GroupId).Where(t => t.Id == transactionId).ToGroupRows().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw TransactionNotFound();

        var payments = await db.TransactionPayments
            .AsNoTracking()
            .Where(p => p.TransactionId == transactionId)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Amount, p.PaymentDate, p.PaymentMethod, p.CreatedByUserId, p.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await access.LoadUserNamesAsync(payments.Select(p => p.CreatedByUserId).Append(row.CreatedByUserId), cancellationToken).ConfigureAwait(false);
        var dto = row.ToDto(today, membership, names);
        var canRemoveAny = membership.GroupIsActive && row.Status != TransactionStatus.Canceled;
        var paymentDtos = payments
            .Select(p => new GroupTransactionPaymentDto(
                p.Id,
                p.Amount,
                p.PaymentDate,
                p.PaymentMethod,
                new GroupUserRefDto(p.CreatedByUserId, names.GetValueOrDefault(p.CreatedByUserId, User.AnonymizedName)),
                p.CreatedAt,
                canRemoveAny && p.CreatedByUserId == membership.UserId))
            .ToList();

        return GroupTransactionDetailDto.From(dto, paymentDtos);
    }

    private static IQueryable<Transaction> ApplyFilters(IQueryable<Transaction> query, GroupTransactionListQuery filter, DateOnly today)
    {
        if (filter.From is { } from)
        {
            query = query.Where(t => t.TransactionDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(t => t.TransactionDate <= to);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(t => t.Type == type);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.WhereCategoryOrChildren(categoryId);
        }

        if (filter.CreatedByUserId is { } createdBy)
        {
            query = query.Where(t => t.CreatedByUserId == createdBy);
        }

        if (filter.PaidByUserId is { } paidBy)
        {
            query = query.Where(t => t.Payments.Any(p => p.CreatedByUserId == paidBy));
        }

        if (filter.Status is { } status)
        {
            query = query.WherePresentedStatus(status, today);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.WhereDescriptionContains(filter.Search);
        }

        return query;
    }

    // ------------------------------------------------------------------ referências

    private async Task<Transaction> LoadTrackedAsync(Guid groupId, Guid transactionId, CancellationToken cancellationToken) =>
        await db.Transactions.AsTracking()
            .Include(t => t.Payments)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.GroupId == groupId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw TransactionNotFound();

    /// <summary>Categoria padrão do sistema ou do próprio usuário (nome/cor/ícone ficam visíveis ao grupo, D-032).</summary>
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

    /// <summary>Conta do PRÓPRIO pagador; inexistente ou de outro usuário → 400 (mesma mensagem, sem vazar existência).</summary>
    private async Task<Account?> ResolvePayerAccountAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
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
