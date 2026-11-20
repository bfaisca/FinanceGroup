using System.Globalization;
using System.Net;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FinanceApp.Application.Emails;

/// <summary>Monta o e-mail de convite para um grupo (pt-BR) com o link para a rota do frontend <c>/invitations/{token}</c> (D-031).</summary>
public sealed class GroupInvitationEmailFactory(IOptions<AppOptions> appOptions)
{
    public string BuildInvitationLink(string token) =>
        $"{appOptions.Value.FrontendBaseUrl.TrimEnd('/')}/invitations/{Uri.EscapeDataString(token)}";

    public EmailMessage Create(string toAddress, string inviterName, string groupName, string token, DateTimeOffset expiresAt)
    {
        var link = BuildInvitationLink(token);
        var expires = expiresAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        var text =
            $"""
            Olá!

            {inviterName} convidou você para participar do grupo "{groupName}" no GroupFinance.
            Para aceitar o convite, entre (ou crie uma conta) com este mesmo e-mail e acesse o link abaixo.
            O convite é válido até {expires}:

            {link}

            Participar de um grupo permite ver as movimentações compartilhadas dele. Suas finanças individuais continuam privadas.
            Se você não esperava este convite, ignore este e-mail.

            Equipe GroupFinance
            """;

        var safeInviter = WebUtility.HtmlEncode(inviterName);
        var safeGroup = WebUtility.HtmlEncode(groupName);
        var safeLink = WebUtility.HtmlEncode(link);
        var html =
            $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <body style="font-family: Arial, sans-serif; color: #1f2937;">
              <p>Olá!</p>
              <p><strong>{safeInviter}</strong> convidou você para participar do grupo <strong>{safeGroup}</strong> no <strong>GroupFinance</strong>.</p>
              <p>Para aceitar, entre (ou crie uma conta) com este mesmo e-mail.</p>
              <p>
                <a href="{safeLink}" style="display:inline-block;padding:10px 16px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;">
                  Ver convite
                </a>
              </p>
              <p>O convite é válido até {expires}.</p>
              <p>Se o botão não funcionar, copie e cole este endereço no navegador:<br><span>{safeLink}</span></p>
              <p>Participar de um grupo permite ver as movimentações compartilhadas dele. Suas finanças individuais continuam privadas.</p>
              <p>Se você não esperava este convite, ignore este e-mail.</p>
              <p>Equipe GroupFinance</p>
            </body>
            </html>
            """;

        return new EmailMessage(toAddress, toAddress, $"Convite para o grupo \"{groupName}\" — GroupFinance", text, html);
    }
}
