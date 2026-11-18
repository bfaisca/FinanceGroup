using FinanceApp.Application.Users;

namespace FinanceApp.Application.Auth;

public sealed class RegisterRequest
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string Token { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>Corpo das respostas de cadastro, login e refresh.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserDto User);

/// <summary>Resultado interno: o refresh token vai para o cookie, nunca para o corpo.</summary>
public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
