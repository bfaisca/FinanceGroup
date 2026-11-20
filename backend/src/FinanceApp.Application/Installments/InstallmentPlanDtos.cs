using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Installments;

/// <summary>Parcelamento com agregados das parcelas não canceladas (contrato §12, D-026).</summary>
public sealed record InstallmentPlanDto(
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
    InstallmentPlanStatus Status,
    int PaidCount,
    int OpenCount,
    int CanceledCount,
    decimal PaidAmount,
    decimal RemainingAmount,
    DateOnly? NextDueDate,
    DateTimeOffset CreatedAt);

/// <summary>Detalhe: repete os campos de <see cref="InstallmentPlanDto"/> e inclui as parcelas em ordem de número.</summary>
public sealed record InstallmentPlanDetailDto(
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
    InstallmentPlanStatus Status,
    int PaidCount,
    int OpenCount,
    int CanceledCount,
    decimal PaidAmount,
    decimal RemainingAmount,
    DateOnly? NextDueDate,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TransactionDto> Installments)
{
    public static InstallmentPlanDetailDto From(InstallmentPlanDto p, IReadOnlyList<TransactionDto> installments) =>
        new(
            p.Id, p.Description, p.TotalAmount, p.InstallmentCount, p.FirstDate, p.CategoryId, p.CategoryName, p.AccountId, p.AccountName,
            p.CreditCardId, p.CreditCardName, p.PaymentMethod, p.Status, p.PaidCount, p.OpenCount, p.CanceledCount, p.PaidAmount,
            p.RemainingAmount, p.NextDueDate, p.CreatedAt, installments);
}

public sealed class CreateInstallmentPlanRequest
{
    public string Description { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public int InstallmentCount { get; init; }

    public DateOnly? FirstDate { get; init; }

    public Guid CategoryId { get; init; }

    public Guid? AccountId { get; init; }

    public Guid? CreditCardId { get; init; }

    public PaymentMethod? PaymentMethod { get; init; }

    public string? Notes { get; init; }
}

/// <summary>Parâmetros de <c>GET /installment-plans</c>.</summary>
public sealed class InstallmentPlanListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const string DefaultSort = "-createdAt";

    public static readonly IReadOnlyList<string> SortFields = ["firstDate", "description", "totalAmount", "createdAt"];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public string? Sort { get; init; }

    public InstallmentPlanStatus? Status { get; init; }
}
