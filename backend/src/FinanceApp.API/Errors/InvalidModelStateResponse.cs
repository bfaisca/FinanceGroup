using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Errors;

/// <summary>
/// Erros de model binding (JSON malformado, corpo ausente, tipo inválido) no mesmo formato de
/// <c>validation_error</c> do FluentValidation, com chaves em camelCase e mensagens em pt-BR.
/// </summary>
public static class InvalidModelStateResponse
{
    public const string BodyKey = "body";

    public static IActionResult Create(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            var field = NormalizeKey(key);
            var message = field == BodyKey ? "Corpo da requisição ausente ou inválido." : "Valor inválido.";
            errors[field] = [message];
        }

        var problem = ProblemDetailsBuilder.Create(
            context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, "Dados inválidos.", errors: errors);

        return ProblemResults.ToResult(problem);
    }

    private static string NormalizeKey(string key)
    {
        var k = key;
        if (k.StartsWith("$.", StringComparison.Ordinal))
        {
            k = k[2..];
        }

        if (k.StartsWith("request.", StringComparison.OrdinalIgnoreCase))
        {
            k = k["request.".Length..];
        }

        return k is "" or "$" || k.Equals("request", StringComparison.OrdinalIgnoreCase)
            ? BodyKey
            : ValidationExtensions.ToCamelCasePath(k);
    }
}
