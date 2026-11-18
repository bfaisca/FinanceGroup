using System.Security.Claims;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinanceApp.Infrastructure.Security;

internal sealed class JwtTokenGenerator(IOptions<JwtOptions> options, IDateTimeProvider clock) : IJwtTokenGenerator
{
    public const string SessionIdClaim = "sid";

    private readonly JsonWebTokenHandler handler = new();

    public AccessToken Generate(User user, Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(user);
        var jwt = options.Value;
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(SessionIdClaim, sessionId.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(JwtSigningKey.Create(jwt.Secret), SecurityAlgorithms.HmacSha256),
        };

        // O JWT tem precisão de segundos: alinhar o retorno ao valor efetivo do claim "exp".
        var token = handler.CreateToken(descriptor);
        var effectiveExpiry = DateTimeOffset.FromUnixTimeSeconds(expiresAt.ToUnixTimeSeconds());
        return new AccessToken(token, effectiveExpiry);
    }
}
