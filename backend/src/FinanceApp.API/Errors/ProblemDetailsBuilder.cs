using System.Diagnostics;
using FinanceApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Errors;

/// <summary>
/// Monta respostas de erro no formato do contrato (D-008): ProblemDetails com
/// <c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>, <c>code</c>, <c>traceId</c> e, em validação, <c>errors</c>.
/// </summary>
public static class ProblemDetailsBuilder
{
    public const string CodeKey = "code";
    public const string TraceIdKey = "traceId";
    public const string ErrorsKey = "errors";

    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string? code = null,
        string? title = null,
        string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var problem = new ProblemDetails
        {
            Status = status,
            Type = TypeFor(status),
            Title = title ?? DefaultTitle(status),
            Detail = detail,
        };
        problem.Extensions[CodeKey] = code ?? DefaultCode(status);
        problem.Extensions[TraceIdKey] = TraceId(httpContext);
        if (errors is not null)
        {
            problem.Extensions[ErrorsKey] = errors;
        }

        return problem;
    }

    /// <summary>Completa ProblemDetails gerados pelo framework (ex.: 404 de rota, 405, 415).</summary>
    public static void ApplyDefaults(HttpContext httpContext, ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        var status = problem.Status ?? httpContext.Response.StatusCode;
        problem.Status = status;
        problem.Type = TypeFor(status);
        if (!problem.Extensions.ContainsKey(CodeKey))
        {
            // Gerado pelo framework: título padrão em inglês substituído pelo equivalente em pt-BR.
            problem.Title = DefaultTitle(status);
            problem.Extensions[CodeKey] = DefaultCode(status);
        }

        problem.Extensions.Remove("exception");
        problem.Extensions[TraceIdKey] = TraceId(httpContext);
    }

    public static string TypeFor(int status) => $"https://httpstatuses.io/{status}";

    public static string DefaultCode(int status) => status switch
    {
        400 => ErrorCodes.ValidationError,
        401 => ErrorCodes.Unauthorized,
        403 => ErrorCodes.Forbidden,
        404 => ErrorCodes.NotFound,
        409 => ErrorCodes.Conflict,
        422 => ErrorCodes.BusinessRuleViolation,
        429 => ErrorCodes.TooManyRequests,
        405 => "method_not_allowed",
        415 => "unsupported_media_type",
        >= 500 => ErrorCodes.InternalError,
        _ => $"http_{status}",
    };

    public static string DefaultTitle(int status) => status switch
    {
        400 => "Requisição inválida.",
        401 => "Não autenticado.",
        403 => "Acesso negado.",
        404 => "Recurso não encontrado.",
        405 => "Método não permitido.",
        409 => "Conflito.",
        413 => "Requisição muito grande.",
        415 => "Tipo de conteúdo não suportado.",
        422 => "Regra de negócio violada.",
        429 => "Muitas requisições. Tente novamente mais tarde.",
        503 => "Serviço indisponível.",
        >= 500 => "Erro interno do servidor.",
        _ => "Erro na requisição.",
    };

    private static string TraceId(HttpContext httpContext) => Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
