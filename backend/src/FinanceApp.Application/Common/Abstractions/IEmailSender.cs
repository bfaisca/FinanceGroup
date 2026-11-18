namespace FinanceApp.Application.Common.Abstractions;

public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string TextBody, string HtmlBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
