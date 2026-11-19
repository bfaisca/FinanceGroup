using FinanceApp.Domain.Accounts;

namespace FinanceApp.Application.Accounts;

public sealed record LatestSnapshotDto(decimal Balance, DateOnly ReferenceDate);

/// <summary>
/// Conta com o saldo informado de referência e o saldo PROJETADO (não é saldo bancário). Contrato §6.
/// </summary>
public sealed record AccountDto(
    Guid Id,
    string Name,
    string? Institution,
    AccountType Type,
    string Currency,
    bool IsActive,
    LatestSnapshotDto? LatestSnapshot,
    decimal? ProjectedBalance,
    DateOnly ProjectedAsOf,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BalanceSnapshotDto(Guid Id, decimal Balance, DateOnly ReferenceDate, DateTimeOffset CreatedAt)
{
    public static BalanceSnapshotDto From(AccountBalanceSnapshot snapshot) =>
        new(snapshot.Id, snapshot.Balance, snapshot.ReferenceDate, snapshot.CreatedAt);
}

public sealed class CreateAccountRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Institution { get; init; }

    public AccountType? Type { get; init; }

    public decimal? InitialBalance { get; init; }

    public DateOnly? BalanceReferenceDate { get; init; }
}

public sealed class UpdateAccountRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Institution { get; init; }

    public AccountType? Type { get; init; }
}

public sealed class CreateBalanceSnapshotRequest
{
    public decimal? Balance { get; init; }

    public DateOnly? ReferenceDate { get; init; }
}
