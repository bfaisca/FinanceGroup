using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace FinanceApp.Infrastructure.Security;

/// <summary>
/// D-007: PasswordHasher do ASP.NET Core Identity (formato V3: PBKDF2-HMAC-SHA512, 100k iterações, salt aleatório).
/// </summary>
internal sealed class PasswordHasher : Application.Common.Abstractions.IPasswordHasher
{
    private readonly PasswordHasher<User> inner = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return inner.HashPassword(null!, password);
    }

    public PasswordVerificationOutcome Verify(string passwordHash, string providedPassword)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(providedPassword))
        {
            return PasswordVerificationOutcome.Failed;
        }

        try
        {
            return inner.VerifyHashedPassword(null!, passwordHash, providedPassword) switch
            {
                PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
                _ => PasswordVerificationOutcome.Failed,
            };
        }
        catch (FormatException)
        {
            // Hash inválido (ex.: conta anonimizada): nunca autentica.
            return PasswordVerificationOutcome.Failed;
        }
    }
}
