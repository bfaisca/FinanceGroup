namespace FinanceApp.Application.Common.Options;

/// <summary>Seção <c>Jwt</c> (variáveis <c>Jwt__*</c>).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinSecretLength = 32;

    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Secret) || Secret.Length < MinSecretLength)
        {
            yield return $"Jwt:Secret deve ser configurado com ao menos {MinSecretLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            yield return "Jwt:Issuer é obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            yield return "Jwt:Audience é obrigatório.";
        }

        if (AccessTokenMinutes is < 1 or > 1440)
        {
            yield return "Jwt:AccessTokenMinutes deve estar entre 1 e 1440.";
        }

        if (RefreshTokenDays is < 1 or > 365)
        {
            yield return "Jwt:RefreshTokenDays deve estar entre 1 e 365.";
        }
    }
}
