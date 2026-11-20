namespace FinanceApp.Domain.CreditCards;

/// <summary>Fechamento e vencimento da fatura que recebe uma compra.</summary>
public readonly record struct InvoiceDates(DateOnly ClosingDate, DateOnly DueDate);

/// <summary>
/// Cálculo puro do ciclo de fatura do cartão (D-025, que detalha D-003).
/// <list type="bullet">
/// <item>Compra com dia ≤ <c>closingDay</c> (ajustado ao último dia do mês quando não existe) entra na fatura que fecha
/// no próprio mês; senão, na do mês seguinte.</item>
/// <item>O vencimento é o <c>dueDay</c> do mês de fechamento, ou do mês seguinte se <c>dueDay ≤ closingDay</c>
/// (inclusive quando fechamento = vencimento). Dias inexistentes no mês viram o último dia do mês.</item>
/// </list>
/// A comparação <c>dueDay ≤ closingDay</c> usa os dias nominais configurados no cartão, não os ajustados ao mês.
/// </summary>
public static class BillingCycle
{
    public const int MinDay = 1;
    public const int MaxDay = 31;

    public static InvoiceDates Calculate(DateOnly purchaseDate, int closingDay, int dueDay)
    {
        EnsureDay(closingDay);
        EnsureDay(dueDay);

        var purchaseMonth = new DateOnly(purchaseDate.Year, purchaseDate.Month, 1);
        var closingThisMonth = ClampToMonth(purchaseMonth, closingDay);
        var closingMonth = purchaseDate <= closingThisMonth ? purchaseMonth : purchaseMonth.AddMonths(1);
        var closingDate = ClampToMonth(closingMonth, closingDay);

        var dueMonth = dueDay > closingDay ? closingMonth : closingMonth.AddMonths(1);
        return new InvoiceDates(closingDate, ClampToMonth(dueMonth, dueDay));
    }

    public static DateOnly CalculateDueDate(DateOnly purchaseDate, int closingDay, int dueDay) =>
        Calculate(purchaseDate, closingDay, dueDay).DueDate;

    /// <summary>Dia <paramref name="day"/> no mês de <paramref name="anyDayOfMonth"/>, limitado ao último dia do mês.</summary>
    private static DateOnly ClampToMonth(DateOnly anyDayOfMonth, int day) =>
        new(anyDayOfMonth.Year, anyDayOfMonth.Month, Math.Min(day, DateTime.DaysInMonth(anyDayOfMonth.Year, anyDayOfMonth.Month)));

    private static void EnsureDay(int day)
    {
        if (day is < MinDay or > MaxDay)
        {
            throw new Common.DomainException("O dia deve estar entre 1 e 31.");
        }
    }
}
