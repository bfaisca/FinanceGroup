namespace FinanceApp.Infrastructure.Email;

/// <summary>
/// Seção <c>Brevo</c> (variáveis <c>Brevo__*</c>, D-055). Com <see cref="ApiKey"/> preenchida, os e-mails são enviados pela API
/// HTTPS da Brevo em vez de SMTP — necessário em hospedagens que bloqueiam as portas SMTP (ex.: Render gratuito).
/// O remetente continua sendo <c>Smtp:From</c>/<c>Smtp:FromName</c> e deve estar validado na Brevo.
/// </summary>
public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string? ApiKey { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
