namespace FinanceApp.Application.Common.Exceptions;

/// <summary>
/// Sinaliza violação de índice/constraint única na persistência, sem expor o provedor de banco.
/// Os serviços traduzem para o erro de contrato adequado.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception("Violação de restrição única.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}

/// <summary>Conflito de concorrência otimista (registro alterado por outra operação).</summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("Conflito de concorrência.", innerException);
