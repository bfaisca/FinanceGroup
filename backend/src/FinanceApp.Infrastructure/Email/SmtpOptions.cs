namespace FinanceApp.Infrastructure.Email;

/// <summary>Seção <c>Smtp</c> (variáveis <c>Smtp__*</c>).</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 25;

    public string? User { get; set; }

    public string? Password { get; set; }

    public bool UseSsl { get; set; }

    public string From { get; set; } = string.Empty;

    public string FromName { get; set; } = "GroupFinance";

    public int TimeoutSeconds { get; set; } = 15;

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            yield return "Smtp:Host é obrigatório.";
        }

        if (Port is < 1 or > 65535)
        {
            yield return "Smtp:Port deve estar entre 1 e 65535.";
        }

        if (string.IsNullOrWhiteSpace(From) || !MimeKit.MailboxAddress.TryParse(From, out _))
        {
            yield return "Smtp:From deve ser um endereço de e-mail válido.";
        }

        if (TimeoutSeconds is < 1 or > 300)
        {
            yield return "Smtp:TimeoutSeconds deve estar entre 1 e 300.";
        }
    }
}
