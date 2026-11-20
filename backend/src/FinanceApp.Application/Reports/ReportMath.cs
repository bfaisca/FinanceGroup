using System.Globalization;

namespace FinanceApp.Application.Reports;

/// <summary>Cálculos puros dos relatórios.</summary>
public static class ReportMath
{
    /// <summary>Percentual 0–100 com 2 casas (arredondamento comercial); total zero → 0.</summary>
    public static decimal Percentage(decimal part, decimal total) =>
        total == 0 ? 0m : Math.Round(part / total * 100m, 2, MidpointRounding.AwayFromZero);

    public static string MonthKey(int year, int month) =>
        string.Create(CultureInfo.InvariantCulture, $"{year:0000}-{month:00}");

    public static string MonthKey(DateOnly date) => MonthKey(date.Year, date.Month);

    public static string DayKey(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Resultado acumulado dentro da janela (soma corrente do <c>result</c>).</summary>
    public static IReadOnlyList<decimal> CumulativeSum(IEnumerable<decimal> values)
    {
        var running = 0m;
        var result = new List<decimal>();
        foreach (var value in values)
        {
            running += value;
            result.Add(running);
        }

        return result;
    }

    public static bool TryParseMonth(string? value, out DateOnly firstDayOfMonth)
    {
        firstDayOfMonth = default;
        if (value is null || value.Length != 7 || value[4] != '-')
        {
            return false;
        }

        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year is < 1900 or > 2200
            || month is < 1 or > 12)
        {
            return false;
        }

        firstDayOfMonth = new DateOnly(year, month, 1);
        return true;
    }
}
