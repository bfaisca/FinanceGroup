namespace FinanceApp.Domain.Common;

/// <summary>
/// Violação de uma regra ou invariante do domínio. Mapeada para HTTP 422 (<c>business_rule_violation</c>).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DomainException()
    {
    }
}
