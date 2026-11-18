using System.Security.Cryptography;
using System.Text;
using FinanceApp.Application.Common.Abstractions;

namespace FinanceApp.Infrastructure.Security;

internal sealed class SecureTokenService : ISecureTokenService
{
    public const int MinByteLength = 16;

    public string GenerateToken(int byteLength = 64)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(byteLength, MinByteLength);
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64UrlEncode(bytes);
    }

    public string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
