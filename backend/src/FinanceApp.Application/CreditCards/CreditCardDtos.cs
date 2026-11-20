namespace FinanceApp.Application.CreditCards;

/// <summary>Fatura que recebe compras feitas hoje (D-025): datas calculadas e valor em aberto.</summary>
public sealed record OpenInvoiceDto(DateOnly ClosingDate, DateOnly DueDate, decimal Amount);

/// <summary>Cartão de crédito com limite utilizado/disponível e fatura aberta (contrato §11).</summary>
public sealed record CreditCardDto(
    Guid Id,
    string Name,
    string? Institution,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay,
    Guid? AccountId,
    string? AccountName,
    bool IsActive,
    decimal UsedLimit,
    decimal AvailableLimit,
    OpenInvoiceDto OpenInvoice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateCreditCardRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Institution { get; init; }

    public decimal? CreditLimit { get; init; }

    public int? ClosingDay { get; init; }

    public int? DueDay { get; init; }

    public Guid? AccountId { get; init; }
}

/// <summary>O corpo do <c>PUT</c> é o mesmo do <c>POST</c> (contrato §11).</summary>
public sealed class UpdateCreditCardRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Institution { get; init; }

    public decimal? CreditLimit { get; init; }

    public int? ClosingDay { get; init; }

    public int? DueDay { get; init; }

    public Guid? AccountId { get; init; }
}

/// <summary>Resumo de limite/fatura usado também pelo dashboard (§15).</summary>
public sealed record CreditCardSummary(decimal UsedLimit, decimal AvailableLimit, OpenInvoiceDto OpenInvoice);
