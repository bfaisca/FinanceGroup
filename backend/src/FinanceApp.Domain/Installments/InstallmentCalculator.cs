using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Installments;

/// <summary>Cálculos puros de parcelamentos (D-026).</summary>
public static class InstallmentCalculator
{
    public const int MinCount = 2;
    public const int MaxCount = 120;

    /// <summary>
    /// Distribui <paramref name="total"/> em <paramref name="count"/> parcelas: <c>base = floor(total/N, 2 casas)</c> e a
    /// PRIMEIRA parcela absorve toda a diferença (no máximo N−1 centavos). A soma é sempre igual ao total.
    /// Ex.: 100,00 em 3x = 33,34 + 33,33 + 33,33.
    /// </summary>
    public static IReadOnlyList<decimal> Distribute(decimal total, int count)
    {
        if (count is < MinCount or > MaxCount)
        {
            throw new DomainException($"O número de parcelas deve estar entre {MinCount} e {MaxCount}.");
        }

        if (total <= 0 || !MoneyRules.HasValidScale(total) || !MoneyRules.IsWithinRange(total))
        {
            throw new DomainException("O valor total é inválido (maior que zero, no máximo 2 casas decimais).");
        }

        var totalCents = decimal.ToInt64(total * 100m);
        var baseCents = totalCents / count;
        if (baseCents < 1)
        {
            throw new DomainException("O valor total deve ser de ao menos R$ 0,01 por parcela.");
        }

        var remainderCents = totalCents - (baseCents * count);
        var amounts = new decimal[count];
        for (var i = 0; i < count; i++)
        {
            amounts[i] = (baseCents + (i == 0 ? remainderCents : 0)) / 100m;
        }

        return amounts;
    }

    /// <summary>
    /// Datas das parcelas: <c>firstDate + (i−1) meses</c>, sempre calculadas a partir do dia de <paramref name="firstDate"/>
    /// (31/jan → 28/fev → 31/mar), ajustando para o último dia do mês quando o dia não existe.
    /// </summary>
    public static IReadOnlyList<DateOnly> Dates(DateOnly firstDate, int count)
    {
        var dates = new DateOnly[count];
        for (var i = 0; i < count; i++)
        {
            try
            {
                dates[i] = firstDate.AddMonths(i);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new DomainException("A data da primeira parcela é inválida.", ex);
            }
        }

        return dates;
    }
}
