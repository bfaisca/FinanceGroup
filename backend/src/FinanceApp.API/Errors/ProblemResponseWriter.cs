using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Errors;

public static class ProblemResponseWriter
{
    /// <summary>Escreve o ProblemDetails como <c>application/problem+json</c>, independentemente do Accept.</summary>
    public static async Task WriteAsync(HttpContext httpContext, ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(problem);
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        var service = httpContext.RequestServices.GetService<IProblemDetailsService>();
        if (service is not null
            && await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem }).ConfigureAwait(false))
        {
            return;
        }

        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json").ConfigureAwait(false);
    }
}
