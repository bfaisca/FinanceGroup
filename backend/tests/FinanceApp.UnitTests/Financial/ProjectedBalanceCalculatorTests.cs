using FinanceApp.Application.Accounts;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.UnitTests.Financial;

/// <summary>Saldo projetado (D-019).</summary>
public class ProjectedBalanceCalculatorTests
{
    private static readonly DateOnly Ref = new(2026, 11, 1);
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static SnapshotReference Snap(decimal balance, DateOnly? date = null, DateTimeOffset? createdAt = null) =>
        new(balance, date ?? Ref, createdAt ?? T0);

    private static ProjectionMovement Income(decimal amount, DateOnly date, bool canceled = false) =>
        new(TransactionType.Income, amount, date, canceled);

    private static ProjectionMovement Expense(decimal amount, DateOnly date, bool canceled = false) =>
        new(TransactionType.Expense, amount, date, canceled);

    private static decimal? Project(IEnumerable<SnapshotReference> snapshots, IEnumerable<ProjectionMovement> movements, DateOnly asOf) =>
        ProjectedBalanceCalculator.Project(snapshots, movements, asOf);

    [Fact]
    public void WithoutSnapshot_IsUnavailable() =>
        Assert.Null(Project([], [Income(100m, Ref.AddDays(1))], Ref.AddDays(10)));

    [Fact]
    public void SnapshotWithoutMovements_EqualsReportedBalance() =>
        Assert.Equal(1000m, Project([Snap(1000m)], [], Ref.AddDays(30)));

    [Fact]
    public void SnapshotOnSameDayAsAsOf_EqualsReportedBalance_EvenWithMovementsThatDay() =>
        Assert.Equal(1000m, Project([Snap(1000m)], [Income(50m, Ref), Expense(20m, Ref)], Ref));

    [Fact]
    public void AddsIncomeAndSubtractsExpenseAfterReference() =>
        Assert.Equal(1000m + 300m - 120m, Project([Snap(1000m)], [Income(300m, Ref.AddDays(2)), Expense(120m, Ref.AddDays(3))], Ref.AddDays(10)));

    [Fact]
    public void MovementOnReferenceDate_IsNotCounted_AlreadyInReportedBalance() =>
        Assert.Equal(1000m, Project([Snap(1000m)], [Income(500m, Ref), Expense(200m, Ref)], Ref.AddDays(5)));

    [Fact]
    public void MovementBeforeReferenceDate_IsNotCounted() =>
        Assert.Equal(1000m, Project([Snap(1000m)], [Income(500m, Ref.AddDays(-1)), Expense(200m, Ref.AddDays(-30))], Ref.AddDays(5)));

    [Fact]
    public void MovementOnAsOfDate_IsCounted() =>
        Assert.Equal(1050m, Project([Snap(1000m)], [Income(50m, Ref.AddDays(5))], Ref.AddDays(5)));

    [Fact]
    public void MovementAfterAsOf_IsNotCounted() =>
        Assert.Equal(1000m, Project([Snap(1000m)], [Income(50m, Ref.AddDays(6))], Ref.AddDays(5)));

    [Fact]
    public void AsOfBeforeReference_ReturnsReportedBalance_IgnoringMovements() =>
        Assert.Equal(1000m, Project([Snap(1000m)], [Income(50m, Ref.AddDays(-2)), Expense(10m, Ref.AddDays(1))], Ref.AddDays(-5)));

    [Fact]
    public void CanceledMovements_AreExcluded() =>
        Assert.Equal(1000m + 10m, Project(
            [Snap(1000m)],
            [Income(10m, Ref.AddDays(1)), Income(900m, Ref.AddDays(1), canceled: true), Expense(500m, Ref.AddDays(2), canceled: true)],
            Ref.AddDays(5)));

    [Fact]
    public void CanBeNegative() =>
        Assert.Equal(-150m, Project([Snap(50m)], [Expense(200m, Ref.AddDays(1))], Ref.AddDays(2)));

    [Fact]
    public void NegativeSnapshotBalance_IsRespected() =>
        Assert.Equal(-80m, Project([Snap(-100m)], [Income(20m, Ref.AddDays(1))], Ref.AddDays(2)));

    [Fact]
    public void EffectiveDate_IsWhatDecidesWindow()
    {
        // O chamador passa EffectiveDate = dueDate ?? transactionDate: aqui o vencimento cai após a referência
        // (mesmo que a data da movimentação fosse anterior) e portanto conta.
        var dueAfter = Expense(100m, Ref.AddDays(10));
        var dueBeforeTransactionAfter = Expense(100m, Ref.AddDays(-1)); // vencimento antes da referência: não conta
        Assert.Equal(900m, Project([Snap(1000m)], [dueAfter, dueBeforeTransactionAfter], Ref.AddDays(15)));
    }

    [Fact]
    public void NewSnapshot_ChangesTheReference()
    {
        var first = Snap(1000m, Ref, T0);
        var second = Snap(2000m, Ref.AddDays(10), T0.AddDays(10));
        var movements = new[] { Income(100m, Ref.AddDays(5)), Income(30m, Ref.AddDays(12)) };

        Assert.Equal(1130m, Project([first], movements, Ref.AddDays(20)));
        Assert.Equal(2030m, Project([first, second], movements, Ref.AddDays(20)));
    }

    [Fact]
    public void SameReferenceDate_LatestCreatedWins()
    {
        var older = Snap(1000m, Ref, T0);
        var newer = Snap(1500m, Ref, T0.AddHours(1));

        Assert.Equal(1500m, Project([older, newer], [], Ref.AddDays(1)));
        Assert.Equal(1500m, Project([newer, older], [], Ref.AddDays(1)));
        Assert.Same(newer, ProjectedBalanceCalculator.SelectReference([older, newer]));
    }

    [Fact]
    public void LaterReferenceDate_WinsEvenIfCreatedEarlier()
    {
        var backfilled = Snap(900m, Ref.AddDays(-5), T0.AddDays(3));
        var current = Snap(1000m, Ref, T0);
        Assert.Same(current, ProjectedBalanceCalculator.SelectReference([backfilled, current]));
    }

    [Fact]
    public void PartialPayments_DoNotChangeProjectedValue() =>
        // O cálculo usa o valor total da movimentação; o pagamento parcial não aparece na entrada.
        Assert.Equal(900m, Project([Snap(1000m)], [Expense(100m, Ref.AddDays(1))], Ref.AddDays(1)));

    [Fact]
    public void Arithmetic_UsesDecimalPrecision() =>
        Assert.Equal(0.30m, Project([Snap(0m)], [Income(0.10m, Ref.AddDays(1)), Income(0.20m, Ref.AddDays(1))], Ref.AddDays(1)));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Counts_WindowIsExclusiveOfReferenceAndInclusiveOfAsOf(int offset, bool expected) =>
        Assert.Equal(expected, ProjectedBalanceCalculator.Counts(Income(1m, Ref.AddDays(offset)), Ref, Ref.AddDays(10)));
}
