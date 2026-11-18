using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace FinanceApp.API.Errors;

/// <summary>Tratamento centralizado de exceções → ProblemDetails (sem detalhes internos fora de Development).</summary>
public sealed partial class GlobalExceptionHandler(IHostEnvironment environment, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted();
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var problem = exception switch
        {
            ValidationException ve => ProblemDetailsBuilder.Create(httpContext, ve.StatusCode, ve.Code, ve.Title, ve.Detail, ve.Errors),
            AppException ae => ProblemDetailsBuilder.Create(httpContext, ae.StatusCode, ae.Code, ae.Title, ae.Detail),
            DomainException de => ProblemDetailsBuilder.Create(
                httpContext, StatusCodes.Status422UnprocessableEntity, ErrorCodes.BusinessRuleViolation, "Regra de negócio violada.", de.Message),
            ConcurrencyConflictException => ProblemDetailsBuilder.Create(
                httpContext, StatusCodes.Status409Conflict, ErrorCodes.Conflict, "O registro foi alterado por outra operação. Tente novamente."),
            UniqueConstraintViolationException => ProblemDetailsBuilder.Create(
                httpContext, StatusCodes.Status409Conflict, ErrorCodes.Conflict, "Conflito com um registro existente."),
            BadHttpRequestException bad => ProblemDetailsBuilder.Create(httpContext, bad.StatusCode, title: "Requisição inválida."),
            _ => null,
        };

        if (problem is null)
        {
            LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path);
            problem = ProblemDetailsBuilder.Create(
                httpContext,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalError,
                "Erro interno do servidor.",
                environment.IsDevelopment() ? exception.ToString() : null);
        }

        await ProblemResponseWriter.WriteAsync(httpContext, problem).ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado em {Method} {Path}.")]
    private partial void LogUnhandled(Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Requisição cancelada pelo cliente.")]
    private partial void LogRequestAborted();
}
