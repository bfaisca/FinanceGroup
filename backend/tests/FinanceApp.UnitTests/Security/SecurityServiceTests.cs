using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.Users;
using FinanceApp.Infrastructure.Security;

namespace FinanceApp.UnitTests.Security;

public class SecureTokenServiceTests
{
    private readonly SecureTokenService service = new();

    [Fact]
    public void GenerateToken_IsBase64UrlWithExpectedEntropy()
    {
        var token = service.GenerateToken();

        // 64 bytes → 86 caracteres Base64Url sem padding.
        Assert.Equal(86, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void GenerateToken_IsUnique()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => service.GenerateToken(32)).ToHashSet();
        Assert.Equal(1000, tokens.Count);
    }

    [Fact]
    public void GenerateToken_RejectsTooShort()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GenerateToken(8));
    }

    [Fact]
    public void Hash_IsSha256LowerHex()
    {
        // Vetor de teste NIST para "abc".
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", service.Hash("abc"));
    }

    [Fact]
    public void Hash_IsDeterministicAndDiffersFromToken()
    {
        var token = service.GenerateToken();
        Assert.Equal(service.Hash(token), service.Hash(token));
        Assert.NotEqual(token, service.Hash(token));
        Assert.Equal(64, service.Hash(token).Length);
    }
}

public class PasswordHasherTests
{
    private readonly PasswordHasher hasher = new();

    [Fact]
    public void HashAndVerify_RoundTrip()
    {
        var hash = hasher.Hash("Senha123");

        Assert.NotEqual("Senha123", hash);
        Assert.Equal(PasswordVerificationOutcome.Success, hasher.Verify(hash, "Senha123"));
        Assert.Equal(PasswordVerificationOutcome.Failed, hasher.Verify(hash, "senha123"));
    }

    [Fact]
    public void Hash_UsesRandomSalt()
    {
        Assert.NotEqual(hasher.Hash("Senha123"), hasher.Hash("Senha123"));
    }

    [Theory]
    [InlineData(User.DisabledPasswordHash)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    public void Verify_InvalidHash_Fails(string hash)
    {
        Assert.Equal(PasswordVerificationOutcome.Failed, hasher.Verify(hash, "Senha123"));
    }
}
