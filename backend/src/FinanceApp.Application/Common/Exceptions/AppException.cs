namespace FinanceApp.Application.Common.Exceptions;

/// <summary>
/// Exceção de aplicação traduzida para ProblemDetails pelo tratador global.
/// <see cref="Title"/> é a mensagem amigável (pt-BR); <see cref="Detail"/> é opcional.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(int statusCode, string code, string title, string? detail = null)
        : base(detail ?? title)
    {
        StatusCode = statusCode;
        Code = code;
        Title = title;
        Detail = detail;
    }

    public int StatusCode { get; }

    public string Code { get; }

    public string Title { get; }

    public string? Detail { get; }
}

public sealed class ValidationException : AppException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(400, ErrorCodes.ValidationError, "Dados inválidos.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    /// <summary>Campo (camelCase) → mensagens.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>400 com código específico (ex.: <c>invalid_token</c>, <c>invalid_password</c>).</summary>
public sealed class BadRequestException(string code, string title, string? detail = null)
    : AppException(400, code, title, detail);

public sealed class UnauthorizedException(string code, string title, string? detail = null)
    : AppException(401, code, title, detail)
{
    public static UnauthorizedException NotAuthenticated() =>
        new(ErrorCodes.Unauthorized, "Não autenticado.");
}

public sealed class ForbiddenException(string title = "Acesso negado.", string? detail = null)
    : AppException(403, ErrorCodes.Forbidden, title, detail);

/// <summary>404 — recurso inexistente ou pertencente a outro usuário (não revela existência).</summary>
public sealed class NotFoundException(string title = "Recurso não encontrado.", string? detail = null)
    : AppException(404, ErrorCodes.NotFound, title, detail);

public sealed class ConflictException(string code, string title, string? detail = null)
    : AppException(409, code, title, detail);

public sealed class BusinessRuleException(string detail)
    : AppException(422, ErrorCodes.BusinessRuleViolation, "Regra de negócio violada.", detail);
