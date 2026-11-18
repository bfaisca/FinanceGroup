using FinanceApp.Domain.Users;

namespace FinanceApp.Application.Common.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtTokenGenerator
{
    /// <summary>
    /// Gera o access token. <paramref name="sessionId"/> é a família do refresh token
    /// (claim <c>sid</c>) e identifica a sessão atual sem depender do cookie.
    /// </summary>
    AccessToken Generate(User user, Guid sessionId);
}
