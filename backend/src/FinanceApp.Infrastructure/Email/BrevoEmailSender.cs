using System.Net.Http.Json;
using FinanceApp.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceApp.Infrastructure.Email;

/// <summary>Envio de e-mails pela API transacional da Brevo (HTTPS, porta 443). A chave nunca é registrada em log.</summary>
internal sealed class BrevoEmailSender(HttpClient http, IOptions<BrevoOptions> brevo, IOptions<SmtpOptions> smtp) : IEmailSender
{
    public const string Endpoint = "https://api.brevo.com/v3/smtp/email";

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var sender = smtp.Value;

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = sender.FromName, email = sender.From },
                to = new[] { new { email = message.ToAddress, name = message.ToName } },
                subject = message.Subject,
                htmlContent = message.HtmlBody,
                textContent = message.TextBody,
            }),
        };
        request.Headers.Add("api-key", brevo.Value.ApiKey);
        request.Headers.Add("accept", "application/json");

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // Sem corpo/URL/cabeçalhos na mensagem: o corpo pode ecoar dados do destinatário.
            throw new InvalidOperationException($"A Brevo recusou o envio do e-mail (HTTP {(int)response.StatusCode}).");
        }
    }
}
