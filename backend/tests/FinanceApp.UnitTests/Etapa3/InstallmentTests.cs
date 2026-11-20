using FinanceApp.Domain.Common;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa3;

/// <summary>D-026: distribuição de centavos, datas e geração das parcelas.</summary>
public class InstallmentTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Distribute_FirstInstallmentAbsorbsRemainder()
    {
        var amounts = InstallmentCalculator.Distribute(100m, 3);

        Assert.Equal([33.34m, 33.33m, 33.33m], amounts);
        Assert.Equal(100m, amounts.Sum());
    }

    [Theory]
    [InlineData(100, 2, 50, 50)]
    [InlineData(0.05, 2, 0.03, 0.02)]
    [InlineData(0.02, 2, 0.01, 0.01)]
    [InlineData(10, 3, 3.34, 3.33)]
    [InlineData(1000, 120, 8.73, 8.33)]
    public void Distribute_TwoAndManyInstallments(double total, int count, double first, double others)
    {
        var amounts = InstallmentCalculator.Distribute((decimal)total, count);

        Assert.Equal(count, amounts.Count);
        Assert.Equal((decimal)total, amounts.Sum());
        Assert.Equal((decimal)first, amounts[0]);
        Assert.All(amounts.Skip(1), a => Assert.Equal((decimal)others, a));
        Assert.True(amounts[0] - amounts[1] <= 0.01m * (count - 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(121)]
    [InlineData(0)]
    public void Distribute_CountOutOfRange_Throws(int count) =>
        Assert.Throws<DomainException>(() => InstallmentCalculator.Distribute(100m, count));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    public void Distribute_InvalidTotal_Throws(double total) =>
        Assert.Throws<DomainException>(() => InstallmentCalculator.Distribute((decimal)total, 2));

    [Fact]
    public void Distribute_LessThanOneCentPerInstallment_Throws() =>
        Assert.Throws<DomainException>(() => InstallmentCalculator.Distribute(0.01m, 2));

    [Fact]
    public void Dates_MonthEndClampsFromFirstDateWithoutDrift() =>
        Assert.Equal([D(2026, 1, 31), D(2026, 2, 28), D(2026, 3, 31), D(2026, 4, 30), D(2026, 5, 31)], InstallmentCalculator.Dates(D(2026, 1, 31), 5));

    [Fact]
    public void Dates_LeapYear() =>
        Assert.Equal([D(2027, 12, 30), D(2028, 1, 30), D(2028, 2, 29), D(2028, 3, 30)], InstallmentCalculator.Dates(D(2027, 12, 30), 4));

    [Fact]
    public void Dates_120Installments_LastIsTenYearsLater()
    {
        var dates = InstallmentCalculator.Dates(D(2026, 1, 31), 120);

        Assert.Equal(120, dates.Count);
        Assert.Equal(D(2035, 12, 31), dates[^1]);
        Assert.Equal(D(2026, 2, 28), dates[1]);
    }

    [Theory]
    [InlineData(2, 100, 50)]
    [InlineData(120, 1000, 8.33)]
    public void Create_GeneratesOneTransactionPerInstallment_WithConsistentTotal(int count, double total, double baseAmount)
    {
        var (plan, installments) = InstallmentPlan.Create(
            UserId, "Compra", (decimal)total, count, D(2026, 12, 1), ExpenseCategory(), null, null, null, null, Today, Now);

        Assert.Equal(count, installments.Count);
        Assert.Equal((decimal)total, installments.Sum(i => i.Amount));
        Assert.Equal((decimal)baseAmount, installments[^1].Amount);
        Assert.Equal(Enumerable.Range(1, count), installments.Select(i => i.InstallmentNumber!.Value));
        Assert.All(installments, i => Assert.Equal(plan.Id, i.InstallmentPlanId));
    }

    [Fact]
    public void Create_StatusPlannedForFutureAndPendingForTodayOrPast_WithoutCardDueDateIsTransactionDate()
    {
        var (_, installments) = InstallmentPlan.Create(
            UserId, "Compra", 90m, 3, Today.AddMonths(-1), ExpenseCategory(), null, null, PaymentMethod.Pix, null, Today, Now);

        Assert.Equal([TransactionStatus.Pending, TransactionStatus.Pending, TransactionStatus.Planned], installments.Select(i => i.Status));
        Assert.Equal(installments.Select(i => (DateOnly?)i.TransactionDate), installments.Select(i => i.DueDate));
        Assert.All(installments, i => Assert.Equal(PaymentMethod.Pix, i.PaymentMethod));
    }

    [Fact]
    public void Create_WithCard_UsesInvoiceDueDateAndCreditCardMethod()
    {
        var card = CreditCard.Create(UserId, "Visa", null, 5000m, 10, 20, null, Now);

        var (plan, installments) = InstallmentPlan.Create(
            UserId, "Compra", 60m, 2, D(2026, 12, 5), ExpenseCategory(), null, card, null, null, Today, Now);

        Assert.Equal(PaymentMethod.CreditCard, plan.PaymentMethod);
        Assert.Equal<DateOnly?>([D(2026, 12, 20), D(2027, 1, 20)], installments.Select(i => i.DueDate));
        Assert.All(installments, i => Assert.Equal(card.Id, i.CreditCardId));
    }

    [Fact]
    public void Create_AmountOrDateOfInstallmentCannotChange()
    {
        var (_, installments) = InstallmentPlan.Create(
            UserId, "Compra", 90m, 3, Today.AddDays(5), ExpenseCategory(), null, null, PaymentMethod.Pix, null, Today, Now);
        var first = installments[0];

        Assert.Throws<DomainException>(() => first.Update(
            UserId, "Compra", 10m, ExpenseCategory(), null, PaymentMethod.Pix, first.TransactionDate, first.DueDate, null, null, Now));
        Assert.Throws<DomainException>(() => first.Update(
            UserId, "Compra", first.Amount, ExpenseCategory(), null, PaymentMethod.Pix, first.TransactionDate.AddDays(1), first.DueDate, null, null, Now));
        Assert.Throws<DomainException>(() => first.EnsureCanBeDeleted());
    }

    [Theory]
    [InlineData(2, 0, 0, InstallmentPlanStatus.Open)]
    [InlineData(0, 3, 0, InstallmentPlanStatus.Completed)]
    [InlineData(0, 0, 3, InstallmentPlanStatus.Canceled)]
    [InlineData(0, 2, 1, InstallmentPlanStatus.Completed)]
    [InlineData(1, 1, 1, InstallmentPlanStatus.Open)]
    public void Status_IsDerivedFromInstallments(int open, int paid, int canceled, InstallmentPlanStatus expected) =>
        Assert.Equal(expected, InstallmentPlanStatusRules.Derive(open, paid, canceled));
}
