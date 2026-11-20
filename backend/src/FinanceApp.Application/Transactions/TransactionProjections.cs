using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Transactions;

/// <summary>Linha lida do banco (projeção SQL) antes de aplicar o estado apresentado.</summary>
internal sealed record TransactionRow(
    Guid Id,
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
    Guid? AccountId,
    string? AccountName,
    PaymentMethod? PaymentMethod,
    DateOnly TransactionDate,
    DateOnly? DueDate,
    DateOnly? PaidAt,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? CreditCardId,
    string? CreditCardName,
    Guid? InstallmentPlanId,
    int? InstallmentNumber,
    int? InstallmentCount,
    Guid? RecurrenceRuleId);

internal static class TransactionProjections
{
    public static IQueryable<TransactionRow> ToRows(this IQueryable<Transaction> query) =>
        query.Select(t => new TransactionRow(
            t.Id,
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
            t.AccountId,
            t.Account == null ? null : t.Account.Name,
            t.PaymentMethod,
            t.TransactionDate,
            t.DueDate,
            t.PaidAt,
            t.Notes,
            t.CreatedAt,
            t.UpdatedAt,
            t.CreditCardId,
            t.CreditCard == null ? null : t.CreditCard.Name,
            t.InstallmentPlanId,
            t.InstallmentNumber,
            t.InstallmentPlan == null ? null : t.InstallmentPlan.InstallmentCount,
            t.RecurrenceRuleId));

    public static TransactionDto ToDto(this TransactionRow row, DateOnly today)
    {
        var remaining = row.Amount - row.PaidAmount;
        return new TransactionDto(
            row.Id,
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
            row.AccountId,
            row.AccountName,
            row.PaymentMethod,
            row.TransactionDate,
            row.DueDate,
            row.PaidAt,
            row.Notes,
            row.CreatedAt,
            row.UpdatedAt,
            row.CreditCardId,
            row.CreditCardName,
            row.InstallmentPlanId is { } planId && row.InstallmentNumber is { } number && row.InstallmentCount is { } count
                ? new InstallmentRefDto(planId, number, count)
                : null,
            row.RecurrenceRuleId);
    }
}
