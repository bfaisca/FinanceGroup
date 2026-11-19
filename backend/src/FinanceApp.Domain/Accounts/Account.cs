using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Accounts;

/// <summary>Conta financeira individual (privada do usuário).</summary>
public sealed class Account
{
    public const int NameMaxLength = 100;
    public const int InstitutionMaxLength = 100;
    public const string DefaultCurrency = "BRL";

    private Account()
    {
        Name = string.Empty;
        Currency = DefaultCurrency;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; }

    public string? Institution { get; private set; }

    public AccountType Type { get; private set; }

    public string Currency { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Account Create(Guid userId, string name, string? institution, AccountType type, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guard.NotEmpty(userId, "O usuário é obrigatório."),
            Name = NormalizeName(name),
            Institution = NormalizeInstitution(institution),
            Type = EnsureType(type),
            Currency = DefaultCurrency,
            IsActive = true,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    public void Update(string name, string? institution, AccountType type, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        Institution = NormalizeInstitution(institution);
        Type = EnsureType(type);
        UpdatedAt = Guard.Utc(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = Guard.Utc(now);
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = Guard.Utc(now);
    }

    /// <summary>Registra um novo saldo informado (snapshot). Conta inativa não aceita snapshots.</summary>
    public AccountBalanceSnapshot RecordBalance(decimal balance, DateOnly referenceDate, DateOnly today, DateTimeOffset now)
    {
        EnsureActive("Conta desativada não aceita novos saldos informados.");
        if (referenceDate > today)
        {
            throw new DomainException("A data de referência do saldo não pode ser futura.");
        }

        return AccountBalanceSnapshot.Create(Id, balance, referenceDate, now);
    }

    /// <summary>Garante que a conta pode receber novas movimentações/pagamentos.</summary>
    public void EnsureActive(string message)
    {
        if (!IsActive)
        {
            throw new DomainException(message);
        }
    }

    private static string NormalizeName(string name)
    {
        var trimmed = Guard.NotBlank(name, "O nome da conta é obrigatório.").Trim();
        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"O nome da conta deve ter no máximo {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeInstitution(string? institution)
    {
        var trimmed = string.IsNullOrWhiteSpace(institution) ? null : institution.Trim();
        if (trimmed is { Length: > InstitutionMaxLength })
        {
            throw new DomainException($"A instituição deve ter no máximo {InstitutionMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static AccountType EnsureType(AccountType type) =>
        Enum.IsDefined(type) ? type : throw new DomainException("Tipo de conta inválido.");
}
