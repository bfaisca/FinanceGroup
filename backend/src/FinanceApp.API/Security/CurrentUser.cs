using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinanceApp.API.Security;

/// <summary>Lê a identidade exclusivamente dos claims do JWT validado.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor, CurrentUserOverride userOverride) : ICurrentUser
{
    public const string SessionIdClaim = "sid";

    // O override só é definido pelo webhook do Telegram, a partir do vínculo verificado (D-053); em requisições com JWT fica nulo.
    public Guid? UserId => userOverride.UserId ?? ReadGuid(JwtRegisteredClaimNames.Sub);

    public Guid? SessionId => ReadGuid(SessionIdClaim);

    public Guid RequireUserId() => UserId ?? throw UnauthorizedException.NotAuthenticated();

    private Guid? ReadGuid(string claimType)
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return Guid.TryParse(principal.FindFirst(claimType)?.Value, out var id) ? id : null;
    }
}
