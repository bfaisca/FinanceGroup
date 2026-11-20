namespace FinanceApp.Domain.Common;

/// <summary>
/// O usuário é um participante legítimo do contexto, mas o seu papel/autoria não permite a ação (HTTP 403).
/// Distingue-se das demais violações de regra (422, <see cref="DomainException"/>).
/// </summary>
public sealed class PermissionDeniedException : DomainException
{
    public PermissionDeniedException(string message)
        : base(message)
    {
    }

    public PermissionDeniedException()
    {
    }

    public PermissionDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
