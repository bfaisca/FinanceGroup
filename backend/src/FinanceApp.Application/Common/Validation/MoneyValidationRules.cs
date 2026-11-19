using FinanceApp.Domain.Common;
using FluentValidation;

namespace FinanceApp.Application.Common.Validation;

/// <summary>Regras de valores monetários (2 casas, limite de 9.999.999.999.999,99), mensagens em pt-BR.</summary>
public static class MoneyValidationRules
{
    /// <summary>Valor monetário positivo (movimentações e pagamentos).</summary>
    public static IRuleBuilderOptionsConditions<T, decimal> PositiveAmount<T>(this IRuleBuilder<T, decimal> rule, string label = "valor") =>
        rule.Custom((value, context) => Report(context, Check(value, allowNegative: false, label)));

    /// <summary>Valor monetário positivo opcional (ignora nulo).</summary>
    public static IRuleBuilderOptionsConditions<T, decimal?> PositiveAmount<T>(this IRuleBuilder<T, decimal?> rule, string label = "valor") =>
        rule.Custom((value, context) =>
        {
            if (value is { } v)
            {
                Report(context, Check(v, allowNegative: false, label));
            }
        });

    /// <summary>Saldo: pode ser negativo, 2 casas, dentro do limite.</summary>
    public static IRuleBuilderOptionsConditions<T, decimal?> SignedAmount<T>(this IRuleBuilder<T, decimal?> rule, string label = "saldo") =>
        rule.Custom((value, context) =>
        {
            if (value is { } v)
            {
                Report(context, Check(v, allowNegative: true, label));
            }
        });

    private static void Report<T>(ValidationContext<T> context, string? error)
    {
        if (error is not null)
        {
            context.AddFailure(error);
        }
    }

    private static string? Check(decimal value, bool allowNegative, string label)
    {
        if (!allowNegative && value <= 0)
        {
            return $"O {label} deve ser maior que zero.";
        }

        if (!MoneyRules.HasValidScale(value))
        {
            return $"O {label} deve ter no máximo 2 casas decimais.";
        }

        return MoneyRules.IsWithinRange(value) ? null : $"O {label} excede o limite permitido.";
    }
}
