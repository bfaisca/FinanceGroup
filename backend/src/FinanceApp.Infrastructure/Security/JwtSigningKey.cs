using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FinanceApp.Infrastructure.Security;

/// <summary>Chave simétrica (HS256) compartilhada entre a emissão e a validação do JWT.</summary>
public static class JwtSigningKey
{
    public static SymmetricSecurityKey Create(string secret) => new(Encoding.UTF8.GetBytes(secret));
}
