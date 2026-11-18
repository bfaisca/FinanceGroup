namespace FinanceApp.Application.Common.Abstractions;

public enum PasswordVerificationOutcome
{
    Failed,
    Success,
    /// <summary>Senha correta, mas o hash usa parâmetros antigos e deve ser recalculado.</summary>
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationOutcome Verify(string passwordHash, string providedPassword);
}
