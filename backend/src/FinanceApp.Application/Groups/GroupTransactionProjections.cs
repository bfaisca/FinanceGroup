using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;

namespace FinanceApp.Application.Groups;

/// <summary>Linha de movimentação de grupo lida do banco. Propositalmente SEM conta, cartão ou dados bancários.</summary>
internal sealed record GroupTransactionRow(
    Guid Id,
    Guid GroupId,
    string Description,
    TransactionType Type,
    decimal Amount,
    decimal PaidAmount,
    TransactionStatus Status,
    Guid CategoryId,
    string CategoryName,
    string? ParentCategoryName,
    string? CategoryColor,
    string? CategoryIcon,
    PaymentMethod? PaymentMethod,
    DateOnly TransactionDate,
    DateOnly? DueDate,
    DateOnly? PaidAt,
    string? Notes,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal static class GroupTransactionProjections
{
    public static IQueryable<GroupTransactionRow> ToGroupRows(this IQueryable<Transaction> query) =>
        query.Select(t => new GroupTransactionRow(
            t.Id,
            t.GroupId!.Value,
            t.Description,
            t.Type,
            t.Amount,
            t.PaidAmount,
            t.Status,
            t.CategoryId,
            t.Category!.Name,
            t.Category.ParentCategory == null ? null : t.Category.ParentCategory.Name,
            t.Category.Color,
            t.Category.Icon,
            t.PaymentMethod,
            t.TransactionDate,
            t.DueDate,
            t.PaidAt,
            t.Notes,
            t.CreatedByUserId,
            t.CreatedAt,
            t.UpdatedAt));

    public static GroupTransactionDto ToDto(
        this GroupTransactionRow row,
        DateOnly today,
        GroupMembership viewer,
        IReadOnlyDictionary<Guid, string> names)
    {
        var remaining = row.Amount - row.PaidAmount;
        var isAuthor = row.CreatedByUserId == viewer.UserId;
        var canceled = row.Status == TransactionStatus.Canceled;
        var permissions = new GroupTransactionPermissionsDto(
            CanEdit: viewer.GroupIsActive && isAuthor && !canceled,
            // Também habilita "reabrir" quando a movimentação está cancelada (mesma permissão do autor).
            CanCancel: viewer.GroupIsActive && isAuthor,
            CanDelete: viewer.GroupIsActive && isAuthor && row.PaidAmount == 0,
            CanPay: viewer.GroupIsActive && !canceled && remaining > 0);

        return new GroupTransactionDto(
            row.Id,
            row.GroupId,
            row.Description,
            row.Type,
            row.Amount,
            row.PaidAmount,
            remaining,
            Transaction.ComputePresentedStatus(row.Status, row.DueDate, remaining, today),
            row.CategoryId,
            row.CategoryName,
            row.ParentCategoryName,
            row.CategoryColor,
            row.CategoryIcon,
            row.PaymentMethod,
            row.TransactionDate,
            row.DueDate,
            row.PaidAt,
            row.Notes,
            new GroupUserRefDto(row.CreatedByUserId, names.GetValueOrDefault(row.CreatedByUserId, User.AnonymizedName)),
            row.CreatedAt,
            row.UpdatedAt,
            permissions);
    }
}
