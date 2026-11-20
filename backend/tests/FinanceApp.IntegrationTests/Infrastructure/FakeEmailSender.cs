using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FinanceApp.Application.Common.Abstractions;

namespace FinanceApp.IntegrationTests.Infrastructure;

public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => sent;

    /// <summary>Quando verdadeiro, o envio falha (simula SMTP indisponível).</summary>
    public bool Fail { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new InvalidOperationException("SMTP indisponível");
        }

        sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> To(string address) =>
        sent.Where(m => string.Equals(m.ToAddress, address, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Extrai o token do link de redefinição do último e-mail enviado ao endereço.</summary>
    public string LastResetTokenFor(string address)
    {
        var messages = To(address);
        var message = (messages.Count > 0 ? messages[^1] : null) ?? throw new InvalidOperationException($"Nenhum e-mail para {address}.");
        var match = TokenRegex().Match(message.TextBody);
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : throw new InvalidOperationException("Link sem token.");
    }

    /// <summary>Extrai o token do link de convite do último e-mail enviado ao endereço.</summary>
    public string LastInvitationTokenFor(string address)
    {
        var messages = To(address);
        var message = (messages.Count > 0 ? messages[^1] : null) ?? throw new InvalidOperationException($"Nenhum e-mail para {address}.");
        var match = InvitationRegex().Match(message.TextBody);
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : throw new InvalidOperationException("Link sem token.");
    }

    [GeneratedRegex(@"/invitations/([^\s""<]+)")]
    private static partial Regex InvitationRegex();

    [GeneratedRegex(@"/reset-password\?token=([^\s""<]+)")]
    private static partial Regex TokenRegex();
}
