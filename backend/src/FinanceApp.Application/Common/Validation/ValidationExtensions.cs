using FluentValidation;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.Application.Common.Validation;

public static class ValidationExtensions
{
    /// <summary>
    /// Valida a instância e lança <see cref="ValidationException"/> com chaves em camelCase
    /// (iguais aos campos do request), conforme o contrato.
    /// </summary>
    public static async Task EnsureValidAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken).ConfigureAwait(false);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(e => ToCamelCasePath(e.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        throw new ValidationException(errors);
    }

    /// <summary>"Items[0].UnitPrice" → "items[0].unitPrice".</summary>
    public static string ToCamelCasePath(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
        {
            return propertyPath;
        }

        var segments = propertyPath.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var s = segments[i];
            if (s.Length > 0 && char.IsUpper(s[0]))
            {
                segments[i] = char.ToLowerInvariant(s[0]) + s[1..];
            }
        }

        return string.Join('.', segments);
    }
}
