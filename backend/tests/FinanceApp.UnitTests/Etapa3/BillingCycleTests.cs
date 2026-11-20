using FinanceApp.Domain.Common;
using FinanceApp.Domain.CreditCards;

namespace FinanceApp.UnitTests.Etapa3;

/// <summary>D-025: fechamento e vencimento da fatura.</summary>
public class BillingCycleTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Theory]
    [InlineData(2026, 6, 9, 10, 20, 2026, 6, 10, 2026, 6, 20)]   // antes do fechamento
    [InlineData(2026, 6, 10, 10, 20, 2026, 6, 10, 2026, 6, 20)]  // no dia do fechamento: ainda entra
    [InlineData(2026, 6, 11, 10, 20, 2026, 7, 10, 2026, 7, 20)]  // depois do fechamento: próxima fatura
    [InlineData(2026, 6, 5, 25, 5, 2026, 6, 25, 2026, 7, 5)]     // vencimento ≤ fechamento: mês seguinte
    [InlineData(2026, 6, 26, 25, 5, 2026, 7, 25, 2026, 8, 5)]
    [InlineData(2026, 6, 5, 10, 10, 2026, 6, 10, 2026, 7, 10)]   // fechamento = vencimento: mês seguinte
    [InlineData(2026, 12, 20, 10, 20, 2027, 1, 10, 2027, 1, 20)] // virada de ano
    [InlineData(2026, 12, 20, 25, 5, 2026, 12, 25, 2027, 1, 5)]
    [InlineData(2026, 12, 26, 25, 5, 2027, 1, 25, 2027, 2, 5)]
    public void Calculate_FollowsClosingAndDueRules(
        int y, int m, int d, int closing, int due, int cy, int cm, int cd, int dy, int dm, int dd)
    {
        var invoice = BillingCycle.Calculate(D(y, m, d), closing, due);

        Assert.Equal(D(cy, cm, cd), invoice.ClosingDate);
        Assert.Equal(D(dy, dm, dd), invoice.DueDate);
    }

    [Theory]
    [InlineData(2026, 2, 28, 31, 31, 2026, 2, 28, 2026, 3, 31)] // fev não bissexto: fechamento 31 → 28; vencimento (31 ≤ 31) no mês seguinte
    [InlineData(2028, 2, 29, 31, 31, 2028, 2, 29, 2028, 3, 31)] // fev bissexto
    [InlineData(2026, 2, 10, 5, 31, 2026, 3, 5, 2026, 3, 31)]   // venc. 31 > fechamento 5 no mesmo mês de fechamento
    [InlineData(2026, 1, 10, 5, 30, 2026, 2, 5, 2026, 2, 28)]   // dia 30 → 28 em fevereiro
    [InlineData(2028, 1, 10, 5, 30, 2028, 2, 5, 2028, 2, 29)]   // dia 30 → 29 em fevereiro bissexto
    [InlineData(2026, 4, 30, 29, 31, 2026, 5, 29, 2026, 5, 31)]
    [InlineData(2026, 4, 30, 30, 29, 2026, 4, 30, 2026, 5, 29)] // dias 29–31: fechamento 30 em abril existe
    [InlineData(2026, 2, 28, 30, 10, 2026, 2, 28, 2026, 3, 10)] // compra no último dia de fev com fechamento 30 → 28: ainda entra
    public void Calculate_ClampsDaysThatDoNotExist(
        int y, int m, int d, int closing, int due, int cy, int cm, int cd, int dy, int dm, int dd)
    {
        var invoice = BillingCycle.Calculate(D(y, m, d), closing, due);

        Assert.Equal(D(cy, cm, cd), invoice.ClosingDate);
        Assert.Equal(D(dy, dm, dd), invoice.DueDate);
    }

    [Fact]
    public void CalculateDueDate_MatchesInvoiceDueDate() =>
        Assert.Equal(D(2026, 7, 20), BillingCycle.CalculateDueDate(D(2026, 6, 11), 10, 20));

    [Theory]
    [InlineData(0, 10)]
    [InlineData(32, 10)]
    [InlineData(10, 0)]
    [InlineData(10, 32)]
    public void Calculate_InvalidDay_Throws(int closing, int due) =>
        Assert.Throws<DomainException>(() => BillingCycle.Calculate(D(2026, 6, 1), closing, due));
}
