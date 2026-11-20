namespace FinanceApp.Domain.Recurrences;

/// <summary>
/// Cálculo puro das datas de uma recorrência (D-027). Passo: <c>Daily</c>=1 dia, <c>Weekly</c>=7, <c>Biweekly</c>=14,
/// <c>Monthly</c>=1 mês, <c>Bimonthly</c>=2, <c>Quarterly</c>=3, <c>Semiannual</c>=6, <c>Annual</c>=12 meses,
/// multiplicado por <c>interval</c>; <c>Custom</c> = a cada <c>interval</c> dias. A ocorrência <em>k</em> é
/// <c>startDate + k×passo</c>, SEMPRE a partir de <c>startDate</c> (31/jan → 28/fev → 31/mar, sem "deriva").
/// </summary>
public static class RecurrenceSchedule
{
    public const int MinInterval = 1;
    public const int MaxInterval = 365;

    /// <summary>Passo base e se é em meses (<c>true</c>) ou dias (<c>false</c>).</summary>
    public static (int Step, bool InMonths) BaseStep(RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily => (1, false),
        RecurrenceFrequency.Weekly => (7, false),
        RecurrenceFrequency.Biweekly => (14, false),
        RecurrenceFrequency.Monthly => (1, true),
        RecurrenceFrequency.Bimonthly => (2, true),
        RecurrenceFrequency.Quarterly => (3, true),
        RecurrenceFrequency.Semiannual => (6, true),
        RecurrenceFrequency.Annual => (12, true),
        RecurrenceFrequency.Custom => (1, false),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Frequência inválida."),
    };

    /// <summary>Ocorrência de índice <paramref name="k"/> (0 = <paramref name="startDate"/>); <c>null</c> se estoura o intervalo de datas.</summary>
    public static DateOnly? Nth(RecurrenceFrequency frequency, int interval, DateOnly startDate, int k)
    {
        var (step, inMonths) = BaseStep(frequency);
        var offset = (long)k * step * interval;
        try
        {
            if (inMonths)
            {
                return offset > int.MaxValue ? null : startDate.AddMonths((int)offset);
            }

            return offset > int.MaxValue ? null : startDate.AddDays((int)offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Datas em ordem crescente de <paramref name="startDate"/> até <paramref name="upTo"/> (inclusive), respeitando <paramref name="endDate"/>.</summary>
    public static IEnumerable<DateOnly> Occurrences(
        RecurrenceFrequency frequency,
        int interval,
        DateOnly startDate,
        DateOnly? endDate,
        DateOnly upTo)
    {
        var limit = endDate is { } end && end < upTo ? end : upTo;
        for (var k = 0; ; k++)
        {
            if (Nth(frequency, interval, startDate, k) is not { } date || date > limit)
            {
                yield break;
            }

            yield return date;
        }
    }

    /// <summary>
    /// Primeira data ≥ <paramref name="today"/> do cronograma que não esteja em <paramref name="closedDates"/>
    /// (ocorrências já pagas/canceladas); <c>null</c> se a regra terminou. Limita a busca a <paramref name="maxSteps"/> datas.
    /// </summary>
    public static DateOnly? NextOpenOccurrence(
        RecurrenceFrequency frequency,
        int interval,
        DateOnly startDate,
        DateOnly? endDate,
        DateOnly today,
        IReadOnlySet<DateOnly> closedDates,
        int maxSteps = 5000)
    {
        var steps = 0;
        foreach (var date in Occurrences(frequency, interval, startDate, endDate, DateOnly.MaxValue))
        {
            if (++steps > maxSteps)
            {
                return null;
            }

            if (date >= today && !closedDates.Contains(date))
            {
                return date;
            }
        }

        return null;
    }
}
