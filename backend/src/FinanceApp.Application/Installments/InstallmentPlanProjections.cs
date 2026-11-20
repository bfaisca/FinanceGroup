using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Installments;

/// <summary>Linha do plano com agregados calculados no banco (somente parcelas NÃO canceladas nas somas).</summary>
internal sealed record InstallmentPlanRow(
    Guid Id,
    string Description,
    decimal TotalAmount,
    int InstallmentCount,
    DateOnly FirstDate,
    Guid CategoryId,
    string CategoryName,
    Guid? AccountId,
    string? AccountName,
    Guid? CreditCardId,
    string? CreditCardName,
    PaymentMethod? PaymentMethod,
    DateTimeOffset CreatedAt,
    int PaidCount,
    int OpenCount,
    int CanceledCount,
    decimal PaidAmount,
    decimal RemainingAmount,
    DateOnly? NextDueDate)
{
    public InstallmentPlanDto ToDto() =>
        new(
            Id, Description, TotalAmount, InstallmentCount, FirstDate, CategoryId, CategoryName, AccountId, AccountName,
            CreditCardId, CreditCardName, PaymentMethod, InstallmentPlanStatusRules.Derive(OpenCount, PaidCount, CanceledCount),
            PaidCount, OpenCount, CanceledCount, PaidAmount, RemainingAmount, NextDueDate, CreatedAt);
}

internal static class InstallmentPlanProjections
{
    /// <summary>
    /// Filtro por estado derivado (D-026), aplicado ANTES da projeção (o EF não ordena/filtra por membros de construtores com subconsultas):
    /// <c>Open</c> = há parcela em aberto; <c>Canceled</c> = todas canceladas; <c>Completed</c> = sem parcela em aberto e não cancelado.
    /// </summary>
    public static IQueryable<InstallmentPlan> WhereStatus(this IQueryable<InstallmentPlan> plans, IAppDbContext db, InstallmentPlanStatus status) =>
        status switch
        {
            InstallmentPlanStatus.Open => plans.Where(p =>
                db.Transactions.Any(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled && t.PaidAmount < t.Amount)),
            InstallmentPlanStatus.Canceled => plans.Where(p =>
                !db.Transactions.Any(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled)
                && db.Transactions.Any(t => t.InstallmentPlanId == p.Id)),
            _ => plans.Where(p =>
                !db.Transactions.Any(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled && t.PaidAmount < t.Amount)
                && (db.Transactions.Any(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled)
                    || !db.Transactions.Any(t => t.InstallmentPlanId == p.Id))),
        };

    public static IQueryable<InstallmentPlanRow> ToRows(this IQueryable<InstallmentPlan> plans, IAppDbContext db) =>
        plans.Select(p => new InstallmentPlanRow(
            p.Id,
            p.Description,
            p.TotalAmount,
            p.InstallmentCount,
            p.FirstDate,
            p.CategoryId,
            p.Category!.Name,
            p.AccountId,
            p.Account == null ? null : p.Account.Name,
            p.CreditCardId,
            p.CreditCard == null ? null : p.CreditCard.Name,
            p.PaymentMethod,
            p.CreatedAt,
            db.Transactions.Count(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled && t.PaidAmount == t.Amount),
            db.Transactions.Count(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled && t.PaidAmount < t.Amount),
            db.Transactions.Count(t => t.InstallmentPlanId == p.Id && t.Status == TransactionStatus.Canceled),
            db.Transactions.Where(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled).Sum(t => t.PaidAmount),
            db.Transactions.Where(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled).Sum(t => t.Amount - t.PaidAmount),
            db.Transactions
                .Where(t => t.InstallmentPlanId == p.Id && t.Status != TransactionStatus.Canceled && t.PaidAmount < t.Amount)
                .Min(t => (DateOnly?)(t.DueDate ?? t.TransactionDate))));
}
