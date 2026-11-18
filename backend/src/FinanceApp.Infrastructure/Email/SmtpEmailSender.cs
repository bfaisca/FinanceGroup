using FinanceApp.Application.Common.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FinanceApp.Infrastructure.Email;

/// <summary>Envio de e-mails via SMTP (MailKit). Em desenvolvimento, o Mailpit captura as mensagens (D-004).</summary>
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var smtp = options.Value;

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(smtp.FromName, smtp.From));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = smtp.TimeoutSeconds * 1000 };
        var socketOptions = !smtp.UseSsl
            ? SecureSocketOptions.None
            : smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(smtp.User))
        {
            await client.AuthenticateAsync(smtp.User, smtp.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }
}
