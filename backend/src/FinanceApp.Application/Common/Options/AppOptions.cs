namespace FinanceApp.Application.Common.Options;

/// <summary>Seção <c>App</c> (variáveis <c>App__*</c>).</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>URL pública do frontend, usada nos links enviados por e-mail.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>Validade do token de redefinição de senha.</summary>
    public int PasswordResetTokenMinutes { get; set; } = 60;

    public IEnumerable<string> Validate()
    {
        if (!Uri.TryCreate(FrontendBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return "App:FrontendBaseUrl deve ser uma URL absoluta http(s).";
        }

        if (PasswordResetTokenMinutes is < 5 or > 1440)
        {
            yield return "App:PasswordResetTokenMinutes deve estar entre 5 e 1440.";
        }
    }
}
