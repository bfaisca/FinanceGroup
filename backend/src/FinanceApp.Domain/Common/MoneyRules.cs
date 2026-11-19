using System.Globalization;

namespace FinanceApp.Domain.Common;

/// <summary>Regras compartilhadas para valores monetários (<c>decimal</c> / <c>NUMERIC(18,2)</c>).</summary>
public static class MoneyRules
{
    /// <summary>Maior valor absoluto aceito pela API (contrato §6/§8).</summary>
    public const decimal MaxAmount = 9_999_999_999_999.99m;

    public static bool HasValidScale(decimal value) => decimal.Round(value, 2) == value;

    public static bool IsWithinRange(decimal value) => Math.Abs(value) <= MaxAmount;

    /// <summary>Formata em pt-BR para mensagens ao usuário (ex.: <c>R$ 1.234,50</c>).</summary>
    public static string Format(decimal value)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo("pt-BR");
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
        }

        return "R$ " + value.ToString("N2", culture);
    }
}
