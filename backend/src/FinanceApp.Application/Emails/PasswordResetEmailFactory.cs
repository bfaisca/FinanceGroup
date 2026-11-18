using System.Net;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FinanceApp.Application.Emails;

/// <summary>Monta o e-mail de redefinição de senha (pt-BR) com o link para o frontend.</summary>
public sealed class PasswordResetEmailFactory(IOptions<AppOptions> appOptions)
{
    public string BuildResetLink(string token) =>
        $"{appOptions.Value.FrontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

    public EmailMessage Create(string toAddress, string toName, string token)
    {
        var link = BuildResetLink(token);
        var minutes = appOptions.Value.PasswordResetTokenMinutes;
        var validity = minutes % 60 == 0
            ? (minutes / 60 == 1 ? "1 hora" : $"{minutes / 60} horas")
            : $"{minutes} minutos";

        var text =
            $"""
            Olá, {toName}.

            Recebemos uma solicitação para redefinir a senha da sua conta no GroupFinance.
            Para criar uma nova senha, acesse o link abaixo (válido por {validity} e de uso único):

            {link}

            Se você não solicitou a redefinição, ignore este e-mail. Sua senha atual continuará válida.

            Equipe GroupFinance
            """;

        var safeName = WebUtility.HtmlEncode(toName);
        var safeLink = WebUtility.HtmlEncode(link);
        var html =
            $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <body style="font-family: Arial, sans-serif; color: #1f2937;">
              <p>Olá, {safeName}.</p>
              <p>Recebemos uma solicitação para redefinir a senha da sua conta no <strong>GroupFinance</strong>.</p>
              <p>
                <a href="{safeLink}" style="display:inline-block;padding:10px 16px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;">
                  Redefinir senha
                </a>
              </p>
              <p>O link é válido por {validity} e pode ser usado apenas uma vez.</p>
              <p>Se o botão não funcionar, copie e cole este endereço no navegador:<br><span>{safeLink}</span></p>
              <p>Se você não solicitou a redefinição, ignore este e-mail. Sua senha atual continuará válida.</p>
              <p>Equipe GroupFinance</p>
            </body>
            </html>
            """;

        return new EmailMessage(toAddress, toName, "Redefinição de senha — GroupFinance", text, html);
    }
}
