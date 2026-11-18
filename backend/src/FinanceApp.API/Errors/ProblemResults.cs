using FinanceApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Errors;

/// <summary>Resultados MVC no formato ProblemDetails do contrato.</summary>
public static class ProblemResults
{
    public static ObjectResult From(HttpContext httpContext, AppException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var errors = (exception as ValidationException)?.Errors;
        return ToResult(ProblemDetailsBuilder.Create(httpContext, exception.StatusCode, exception.Code, exception.Title, exception.Detail, errors));
    }

    public static ObjectResult ToResult(ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        var result = new ObjectResult(problem) { StatusCode = problem.Status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
