namespace FinanceApp.Application.Common.Abstractions;

/// <summary>Geração de tokens opacos aleatórios (refresh e redefinição de senha) e hash SHA-256.</summary>
public interface ISecureTokenService
{
    /// <summary>Gera um token aleatório criptograficamente seguro, codificado em Base64Url.</summary>
    string GenerateToken(int byteLength = 64);

    /// <summary>Hash SHA-256 em hexadecimal minúsculo. Apenas este valor é persistido.</summary>
    string Hash(string token);
}
