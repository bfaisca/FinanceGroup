using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceApp.Application.Telegram;

/// <summary>
/// Interpreta o texto da despesa por regras fixas (D-054), sem IA: "45,90 mercado", "mercado 45,90", "R$ 1.234,56 aluguel",
/// "/despesa 45.90 padaria". O valor é o primeiro ou o último número do texto; o restante é a descrição.
/// </summary>
public static partial class TelegramExpenseParser
{
    public sealed record ParsedExpense(decimal Amount, string Description);

    // 1.234,56 | 1234,56 | 1234.56 | 45 | 45,9
    [GeneratedRegex(@"^(?:R\$\s*)?(?<n>\d{1,3}(?:\.\d{3})+(?:,\d{1,2})?|\d+(?:[.,]\d{1,2})?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountRegex();

    public static ParsedExpense? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.StartsWith("/despesa", StringComparison.OrdinalIgnoreCase))
        {
            value = value["/despesa".Length..];
            var at = value.IndexOf(' ');
            value = value.StartsWith('@') && at >= 0 ? value[at..] : value;
        }

        value = Regex.Replace(value.Trim(), @"\bR\$\s*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var hasFirst = TryAmount(parts[0], out var first);
        var hasLast = TryAmount(parts[^1], out var last);
        if (hasFirst && hasLast)
        {
            // "2 pizzas 45,90": ambíguo — o bot pede para reenviar com um único valor.
            return null;
        }

        if (hasFirst)
        {
            return Build(first, parts[1..]);
        }

        return hasLast ? Build(last, parts[..^1]) : null;
    }

    private static ParsedExpense? Build(decimal amount, string[] descriptionParts)
    {
        var description = string.Join(' ', descriptionParts).Trim();
        return amount > 0 && description.Length > 0 ? new ParsedExpense(amount, description) : null;
    }

    private static bool TryAmount(string token, out decimal amount)
    {
        amount = 0;
        var match = AmountRegex().Match(token);
        if (!match.Success)
        {
            return false;
        }

        var raw = match.Groups["n"].Value;
        string normalized;
        if (raw.Contains(',', StringComparison.Ordinal))
        {
            normalized = raw.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }
        else if (raw.Count(c => c == '.') > 1 || (raw.Contains('.', StringComparison.Ordinal) && raw[(raw.IndexOf('.', StringComparison.Ordinal) + 1)..].Length == 3))
        {
            normalized = raw.Replace(".", string.Empty, StringComparison.Ordinal);
        }
        else
        {
            normalized = raw;
        }

        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount) && amount is > 0 and < 1_000_000_000_000m;
    }
}
