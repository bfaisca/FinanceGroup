using FinanceApp.Application.Accounts;
using FinanceApp.Application.CreditCards;
using FinanceApp.Application.Reports;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa3;

public class CardLimitsProjectionAndReportMathTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Theory]
    [InlineData(5000, 1200, 3800)]
    [InlineData(1000, 1000, 0)]
    [InlineData(1000, 1300.50, -300.50)]
    [InlineData(0, 0, 0)]
    public void AvailableLimit_IsLimitMinusUsed(double limit, double used, double expected) =>
        Assert.Equal((decimal)expected, CreditCardLimits.Available((decimal)limit, (decimal)used));

    [Fact]
    public async Task Projection_CountsCardPurchasesOnCardAccountAtInvoiceDueDate_AndIgnoresCanceled()
    {
        var db = TestDb.Create();
        var account = NewAccount();
        var other = NewAccount("Outra");
        var card = CreditCard.Create(UserId, "Visa", null, 5000m, 10, 20, account, Now);
        var category = ExpenseCategory();
        db.AddRange(account, other, card, category);
        db.AccountBalanceSnapshots.Add(account.RecordBalance(1000m, D(2026, 6, 1), D(2026, 6, 1), Now));
        db.AccountBalanceSnapshots.Add(other.RecordBalance(500m, D(2026, 6, 1), D(2026, 6, 1), Now));
        // compra em 06-05 → vencimento 06-20; compra em 06-12 → vencimento 07-20 (depois do fechamento).
        var p1 = Transaction.Create(UserId, "A", TransactionType.Expense, 100m, category, null, null, TransactionStatus.Pending, D(2026, 6, 5), null, null, Now, card);
        var p2 = Transaction.Create(UserId, "B", TransactionType.Expense, 50m, category, null, null, TransactionStatus.Pending, D(2026, 6, 12), null, null, Now, card);
        var p3 = Transaction.Create(UserId, "C", TransactionType.Expense, 25m, category, null, null, TransactionStatus.Pending, D(2026, 6, 6), null, null, Now, card);
        p3.Cancel(UserId, Now);
        db.Transactions.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();
        var service = new BalanceProjectionService(db);

        var at0630 = await service.GetAsync([account.Id, other.Id], D(2026, 6, 30), CancellationToken.None);
        var at0720 = await service.GetAsync([account.Id, other.Id], D(2026, 7, 20), CancellationToken.None);
        var before = await service.GetAsync([account.Id], D(2026, 6, 19), CancellationToken.None);

        Assert.Equal(900m, at0630[account.Id].ProjectedBalance);   // só a compra de 06-20 (a de 07-20 ainda não)
        Assert.Equal(850m, at0720[account.Id].ProjectedBalance);
        Assert.Equal(500m, at0720[other.Id].ProjectedBalance);     // outra conta não é afetada
        Assert.Equal(1000m, before[account.Id].ProjectedBalance);  // antes do vencimento nada é descontado
    }

    [Theory]
    [InlineData(1, 3, 33.33)]
    [InlineData(2, 3, 66.67)]
    [InlineData(1450, 1450, 100)]
    [InlineData(0, 100, 0)]
    [InlineData(50, 410, 12.20)]
    [InlineData(1040, 1450, 71.72)]
    public void Percentage_RoundsToTwoDecimals(double part, double total, double expected) =>
        Assert.Equal((decimal)expected, ReportMath.Percentage((decimal)part, (decimal)total));

    [Fact]
    public void Percentage_ZeroTotal_IsZero() => Assert.Equal(0m, ReportMath.Percentage(10m, 0m));

    [Fact]
    public void Keys_AreInvariantAndZeroPadded()
    {
        Assert.Equal("2026-06", ReportMath.MonthKey(D(2026, 6, 15)));
        Assert.Equal("0999-01", ReportMath.MonthKey(999, 1));
        Assert.Equal("2026-06-05", ReportMath.DayKey(D(2026, 6, 5)));
    }

    [Fact]
    public void CumulativeSum_IsRunningTotal() =>
        Assert.Equal([0m, 2050m, 1973m], ReportMath.CumulativeSum([0m, 2050m, -77m]));

    [Theory]
    [InlineData("2026-06", true, 2026, 6)]
    [InlineData("2026-12", true, 2026, 12)]
    [InlineData("2026-13", false, 0, 0)]
    [InlineData("2026-00", false, 0, 0)]
    [InlineData("2026-6", false, 0, 0)]
    [InlineData("26-06", false, 0, 0)]
    [InlineData("2026/06", false, 0, 0)]
    [InlineData("1800-06", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void TryParseMonth(string? value, bool ok, int year, int month)
    {
        Assert.Equal(ok, ReportMath.TryParseMonth(value, out var parsed));
        if (ok)
        {
            Assert.Equal(D(year, month, 1), parsed);
        }
    }
}
