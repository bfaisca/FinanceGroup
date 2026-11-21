using FinanceApp.Application.Groups;
using FinanceApp.Domain.Groups;

namespace FinanceApp.UnitTests.Etapa4;

public class GroupDashboardMathTests
{
    private static readonly Guid Ana = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Bia = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid Caio = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    private static readonly Dictionary<Guid, string> Names = new() { [Ana] = "Ana", [Bia] = "Bia", [Caio] = "Caio" };
    private static readonly Dictionary<Guid, GroupMemberStatus> Statuses = new()
    {
        [Ana] = GroupMemberStatus.Active,
        [Bia] = GroupMemberStatus.Left,
    };

    [Theory]
    [InlineData(150, 100, 50)]
    [InlineData(50, 100, -50)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 300, -66.67)]
    [InlineData(0, 100, -100)]
    public void ChangePercent_IsRoundedToTwoDecimals(double current, double previous, double expected) =>
        Assert.Equal((decimal)expected, GroupDashboardMath.ChangePercent((decimal)current, (decimal)previous));

    [Fact]
    public void ChangePercent_ZeroBase_IsNull()
    {
        Assert.Null(GroupDashboardMath.ChangePercent(100m, 0m));
        Assert.Null(GroupDashboardMath.ChangePercent(0m, 0m));
    }

    [Fact]
    public void PreviousPeriod_HasTheSameDurationRightBeforeFrom()
    {
        // 01/11 a 30/11 (30 dias) -> 02/10 a 31/10.
        var (from, to) = GroupDashboardMath.PreviousPeriod(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        Assert.Equal(new DateOnly(2026, 10, 2), from);
        Assert.Equal(new DateOnly(2026, 10, 31), to);

        // Um único dia.
        var (f1, t1) = GroupDashboardMath.PreviousPeriod(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));
        Assert.Equal(new DateOnly(2026, 2, 28), f1);
        Assert.Equal(new DateOnly(2026, 2, 28), t1);
    }

    [Fact]
    public void Totals_PaidIsPrincipalPlusInterest_PendingIsOriginalMinusPrincipal()
    {
        var totals = GroupDashboardMath.Totals(expenses: 500m, paidPrincipal: 200m, interest: 23.50m);

        Assert.Equal(new GroupDashboardTotalsDto(500m, 200m, 23.50m, 223.50m, 300m), totals);
    }

    [Fact]
    public void Totals_PendingNeverGoesNegative() =>
        Assert.Equal(0m, GroupDashboardMath.Totals(100m, 120m, 0m).ExpensesPending);

    [Fact]
    public void BuildPaidBy_PercentagesAreOverThePaidTotal_OrderedByAmountThenName()
    {
        var amounts = new List<PayerAmount>
        {
            new(Ana, Principal: 60m, Interest: 10m),
            new(Bia, Principal: 20m, Interest: 0m),
            new(Caio, Principal: 10m, Interest: 0m),
        };

        var items = GroupDashboardMath.BuildPaidBy(amounts, Names, Statuses);

        Assert.Equal(new[] { "Ana", "Bia", "Caio" }, items.Select(i => i.Name).ToArray());
        Assert.Equal((70m, 10m, 70m), (items[0].PaidAmount, items[0].InterestAmount, items[0].Percent));
        Assert.Equal(20m, items[1].Percent);
        Assert.Equal(10m, items[2].Percent);
        Assert.Equal(100m, items.Sum(i => i.Percent));
    }

    [Fact]
    public void BuildPaidBy_ExposesMembershipStatus_DefaultingToLeft_AndSkipsZeroRows()
    {
        var amounts = new List<PayerAmount> { new(Ana, 10m, 0m), new(Bia, 10m, 0m), new(Caio, 0m, 0m) };

        var items = GroupDashboardMath.BuildPaidBy(amounts, Names, Statuses);

        Assert.Equal(2, items.Count);
        Assert.Equal(GroupMemberStatus.Active, items.Single(i => i.UserId == Ana).Status);
        Assert.Equal(GroupMemberStatus.Left, items.Single(i => i.UserId == Bia).Status);
    }

    [Fact]
    public void BuildPaidBy_UnknownName_ShowsRemovedUser() =>
        Assert.Equal("Usuário removido", GroupDashboardMath.BuildPaidBy([new PayerAmount(Guid.NewGuid(), 5m, 0m)], Names, Statuses).Single().Name);

    [Fact]
    public void BuildPaidBy_ThirdsRoundToTwoDecimals()
    {
        var amounts = new List<PayerAmount> { new(Ana, 10m, 0m), new(Bia, 10m, 0m), new(Caio, 10m, 0m) };

        Assert.All(GroupDashboardMath.BuildPaidBy(amounts, Names, Statuses), i => Assert.Equal(33.33m, i.Percent));
    }
}
