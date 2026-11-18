using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Users;

/// <summary>Usuário da aplicação.</summary>
public sealed class User
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const string AnonymizedName = "Usuário removido";
    public const string AnonymizedEmailDomain = "anonymized.invalid";

    /// <summary>Valor que nunca corresponde a um hash válido: impede login após anonimização.</summary>
    public const string DisabledPasswordHash = "!disabled";

    private User()
    {
        Name = string.Empty;
        Email = string.Empty;
        PasswordHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>E-mail normalizado (sem espaços nas extremidades e em minúsculas).</summary>
    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive { get; private set; }

    public static User Create(string name, string email, string passwordHash, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        return new User
        {
            Id = Guid.NewGuid(),
            Name = NormalizeName(name),
            Email = NormalizeEmail(email),
            PasswordHash = Guard.NotBlank(passwordHash, "O hash da senha é obrigatório."),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            IsActive = true,
        };
    }

    /// <summary>Normaliza o e-mail (trim + minúsculas) para armazenamento e comparação.</summary>
    public static string NormalizeEmail(string email)
    {
        var normalized = Guard.NotBlank(email, "O e-mail é obrigatório.").Trim().ToLowerInvariant();
        if (normalized.Length > EmailMaxLength)
        {
            throw new DomainException($"O e-mail deve ter no máximo {EmailMaxLength} caracteres.");
        }

        return normalized;
    }

    public void Rename(string name, DateTimeOffset now)
    {
        EnsureActive();
        Name = NormalizeName(name);
        UpdatedAt = Guard.Utc(now);
    }

    public void ChangePasswordHash(string passwordHash, DateTimeOffset now)
    {
        EnsureActive();
        PasswordHash = Guard.NotBlank(passwordHash, "O hash da senha é obrigatório.");
        UpdatedAt = Guard.Utc(now);
    }

    /// <summary>
    /// Anonimiza e desativa a conta (D-014): remove dados pessoais, invalida a senha
    /// e preserva o registro para manter a integridade do histórico de grupos.
    /// </summary>
    public void Anonymize(DateTimeOffset now)
    {
        EnsureActive();
        Name = AnonymizedName;
        Email = $"removed-{Guid.NewGuid():N}@{AnonymizedEmailDomain}";
        PasswordHash = DisabledPasswordHash;
        IsActive = false;
        UpdatedAt = Guard.Utc(now);
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException("A conta do usuário está inativa.");
        }
    }

    private static string NormalizeName(string name)
    {
        var trimmed = Guard.NotBlank(name, "O nome é obrigatório.").Trim();
        if (trimmed.Length is < NameMinLength or > NameMaxLength)
        {
            throw new DomainException($"O nome deve ter entre {NameMinLength} e {NameMaxLength} caracteres.");
        }

        return trimmed;
    }
}
