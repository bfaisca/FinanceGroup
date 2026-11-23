namespace FinanceApp.Application.Common.Abstractions;

/// <summary>
/// Identidade definida pelo servidor para o escopo da requisição, usada somente pelo webhook do Telegram (D-053):
/// o usuário é obtido do vínculo verificado (chat → conta), nunca de dados enviados pelo cliente.
/// </summary>
public sealed class CurrentUserOverride
{
    public Guid? UserId { get; set; }
}
