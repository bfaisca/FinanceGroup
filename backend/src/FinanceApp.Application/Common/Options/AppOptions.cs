namespace FinanceApp.Application.Common.Options;

/// <summary>Seção <c>App</c> (variáveis <c>App__*</c>).</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>URL pública do frontend, usada nos links enviados por e-mail.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>Validade do token de redefinição de senha.</summary>
    public int PasswordResetTokenMinutes { get; set; } = 60;

    /// <summary>Fuso horário IANA da aplicação (D-021), usado para calcular "hoje".</summary>
    public string TimeZone { get; set; } = DefaultTimeZone;

    public const string DefaultTimeZone = "America/Sao_Paulo";

    public IEnumerable<string> Validate()
    {
        if (!TryResolveTimeZone(TimeZone, out _))
        {
            yield return $"App:TimeZone '{TimeZone}' não é um fuso horário válido (ex.: America/Sao_Paulo); verifique se o tzdata está instalado.";
        }

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

    public static bool TryResolveTimeZone(string? id, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
