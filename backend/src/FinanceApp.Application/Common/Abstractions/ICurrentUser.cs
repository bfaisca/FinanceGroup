namespace FinanceApp.Application.Common.Abstractions;

/// <summary>Identidade do usuário autenticado, obtida exclusivamente do token (nunca do corpo da requisição).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>Família do refresh token da sessão atual (claim <c>sid</c>), quando disponível.</summary>
    Guid? SessionId { get; }

    /// <exception cref="Exceptions.UnauthorizedException">Quando não há usuário autenticado.</exception>
    Guid RequireUserId();
}
