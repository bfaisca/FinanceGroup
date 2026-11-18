namespace FinanceApp.API.RateLimiting;

/// <summary>Seção <c>RateLimiting</c> (D-016): janela fixa por IP nos endpoints de autenticação.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>Login, cadastro, esqueci a senha e redefinição.</summary>
    public FixedWindowLimit Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Refresh (chamado na inicialização do app e a cada expiração do access token).</summary>
    public FixedWindowLimit Refresh { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
}

public sealed class FixedWindowLimit
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Refresh = "auth-refresh";
}
