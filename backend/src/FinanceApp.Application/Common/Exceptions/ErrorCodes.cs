namespace FinanceApp.Application.Common.Exceptions;

/// <summary>Códigos estáveis de erro (campo <c>code</c> do ProblemDetails). Ver docs/API_CONTRACTS.md §1.2.</summary>
public static class ErrorCodes
{
    public const string ValidationError = "validation_error";
    public const string InvalidToken = "invalid_token";
    public const string Unauthorized = "unauthorized";
    public const string InvalidCredentials = "invalid_credentials";
    public const string InvalidRefreshToken = "invalid_refresh_token";
    public const string InvalidPassword = "invalid_password";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string EmailAlreadyRegistered = "email_already_registered";
    public const string Conflict = "conflict";
    public const string BusinessRuleViolation = "business_rule_violation";
    public const string TooManyRequests = "too_many_requests";
    public const string InternalError = "internal_error";
}
