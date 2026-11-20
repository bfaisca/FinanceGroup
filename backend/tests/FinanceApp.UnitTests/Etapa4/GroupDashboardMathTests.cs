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
    public void BuildParticipation_PercentagesAreOverTheSameTypeTotal_OrderedByTotalThenName()
    {
        var amounts = new List<ParticipationAmount>
        {
            new(Ana, Expense: 70m, Income: 0m),
            new(Bia, Expense: 20m, Income: 500m),
            new(Caio, Expense: 10m, Income: 0m),
        };

        var items = GroupDashboardMath.BuildParticipation(amounts, Names, Statuses);

        Assert.Equal(new[] { "Bia", "Ana", "Caio" }, items.Select(i => i.Name).ToArray());
        var ana = items.Single(i => i.UserId == Ana);
        Assert.Equal(70m, ana.ExpensePercent);
        Assert.Equal(0m, ana.IncomePercent);
        var bia = items.Single(i => i.UserId == Bia);
        Assert.Equal(20m, bia.ExpensePercent);
        Assert.Equal(100m, bia.IncomePercent);
        Assert.Equal(100m, items.Sum(i => i.ExpensePercent));
    }

    [Fact]
    public void BuildParticipation_ExposesMembershipStatus_DefaultingToLeft_AndSkipsZeroRows()
    {
        var amounts = new List<ParticipationAmount>
        {
            new(Ana, 10m, 0m),
            new(Bia, 10m, 0m),
            new(Caio, 0m, 0m),
        };

        var items = GroupDashboardMath.BuildParticipation(amounts, Names, Statuses);

        Assert.Equal(2, items.Count);
        Assert.Equal(GroupMemberStatus.Active, items.Single(i => i.UserId == Ana).Status);
        Assert.Equal(GroupMemberStatus.Left, items.Single(i => i.UserId == Bia).Status);
    }

    [Fact]
    public void BuildParticipation_UnknownName_ShowsRemovedUser()
    {
        var items = GroupDashboardMath.BuildParticipation([new ParticipationAmount(Guid.NewGuid(), 5m, 0m)], Names, Statuses);

        Assert.Equal("Usuário removido", items.Single().Name);
    }

    [Fact]
    public void BuildParticipation_ThirdsRoundToTwoDecimals()
    {
        var amounts = new List<ParticipationAmount> { new(Ana, 10m, 0m), new(Bia, 10m, 0m), new(Caio, 10m, 0m) };

        var items = GroupDashboardMath.BuildParticipation(amounts, Names, Statuses);

        Assert.All(items, i => Assert.Equal(33.33m, i.ExpensePercent));
    }
}
