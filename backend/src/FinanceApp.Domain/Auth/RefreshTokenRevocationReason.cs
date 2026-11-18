namespace FinanceApp.Domain.Auth;

public enum RefreshTokenRevocationReason
{
    /// <summary>Substituído por um novo token (rotação).</summary>
    Rotated,
    Logout,
    PasswordChanged,
    PasswordReset,
    /// <summary>Reutilização de token rotacionado detectada: família inteira revogada.</summary>
    ReuseDetected,
    AccountDeleted,
}
