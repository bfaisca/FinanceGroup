using FinanceApp.Domain.Recurrences;

namespace FinanceApp.UnitTests.Etapa3;

/// <summary>D-027: cronograma das recorrências.</summary>
public class RecurrenceScheduleTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private static List<DateOnly> Dates(RecurrenceFrequency f, int interval, DateOnly start, DateOnly upTo, DateOnly? end = null) =>
        RecurrenceSchedule.Occurrences(f, interval, start, end, upTo).ToList();

    [Theory]
    [InlineData(RecurrenceFrequency.Daily, 1, false, 1)]
    [InlineData(RecurrenceFrequency.Weekly, 7, false, 7)]
    [InlineData(RecurrenceFrequency.Biweekly, 14, false, 14)]
    [InlineData(RecurrenceFrequency.Monthly, 1, true, 1)]
    [InlineData(RecurrenceFrequency.Bimonthly, 2, true, 2)]
    [InlineData(RecurrenceFrequency.Quarterly, 3, true, 3)]
    [InlineData(RecurrenceFrequency.Semiannual, 6, true, 6)]
    [InlineData(RecurrenceFrequency.Annual, 12, true, 12)]
    [InlineData(RecurrenceFrequency.Custom, 1, false, 1)]
    public void BaseStep_PerFrequency(RecurrenceFrequency frequency, int step, bool inMonths, int _) =>
        Assert.Equal((step, inMonths), RecurrenceSchedule.BaseStep(frequency));

    [Fact]
    public void Daily() =>
        Assert.Equal([D(2026, 6, 1), D(2026, 6, 2), D(2026, 6, 3)], Dates(RecurrenceFrequency.Daily, 1, D(2026, 6, 1), D(2026, 6, 3)));

    [Fact]
    public void Daily_WithInterval3() =>
        Assert.Equal([D(2026, 6, 1), D(2026, 6, 4), D(2026, 6, 7)], Dates(RecurrenceFrequency.Daily, 3, D(2026, 6, 1), D(2026, 6, 9)));

    [Fact]
    public void Weekly() =>
        Assert.Equal([D(2026, 6, 1), D(2026, 6, 8), D(2026, 6, 15)], Dates(RecurrenceFrequency.Weekly, 1, D(2026, 6, 1), D(2026, 6, 20)));

    [Fact]
    public void Weekly_Interval2_IsEveryFourteenDays() =>
        Assert.Equal([D(2026, 6, 1), D(2026, 6, 15), D(2026, 6, 29)], Dates(RecurrenceFrequency.Weekly, 2, D(2026, 6, 1), D(2026, 6, 30)));

    [Fact]
    public void Biweekly() =>
        Assert.Equal([D(2026, 6, 1), D(2026, 6, 15), D(2026, 6, 29)], Dates(RecurrenceFrequency.Biweekly, 1, D(2026, 6, 1), D(2026, 6, 30)));

    [Fact]
    public void Monthly() =>
        Assert.Equal([D(2026, 6, 10), D(2026, 7, 10), D(2026, 8, 10)], Dates(RecurrenceFrequency.Monthly, 1, D(2026, 6, 10), D(2026, 8, 31)));

    [Fact]
    public void Monthly_Interval2_IsEveryTwoMonths() =>
        Assert.Equal([D(2026, 6, 10), D(2026, 8, 10), D(2026, 10, 10)], Dates(RecurrenceFrequency.Monthly, 2, D(2026, 6, 10), D(2026, 10, 31)));

    [Fact]
    public void Bimonthly() =>
        Assert.Equal([D(2026, 1, 5), D(2026, 3, 5), D(2026, 5, 5)], Dates(RecurrenceFrequency.Bimonthly, 1, D(2026, 1, 5), D(2026, 6, 1)));

    [Fact]
    public void Quarterly() =>
        Assert.Equal([D(2026, 1, 5), D(2026, 4, 5), D(2026, 7, 5), D(2026, 10, 5)], Dates(RecurrenceFrequency.Quarterly, 1, D(2026, 1, 5), D(2026, 12, 31)));

    [Fact]
    public void Semiannual() =>
        Assert.Equal([D(2026, 1, 5), D(2026, 7, 5), D(2027, 1, 5)], Dates(RecurrenceFrequency.Semiannual, 1, D(2026, 1, 5), D(2027, 1, 5)));

    [Fact]
    public void Annual_LeapDayClampsAndReturnsToLeapDay() =>
        Assert.Equal([D(2028, 2, 29), D(2029, 2, 28), D(2030, 2, 28), D(2031, 2, 28), D(2032, 2, 29)],
            Dates(RecurrenceFrequency.Annual, 1, D(2028, 2, 29), D(2032, 12, 31)));

    [Fact]
    public void Annual_Interval2() =>
        Assert.Equal([D(2026, 3, 1), D(2028, 3, 1)], Dates(RecurrenceFrequency.Annual, 2, D(2026, 3, 1), D(2029, 1, 1)));

    [Fact]
    public void Custom_IsEveryIntervalDays() =>
        Assert.Equal([D(2026, 6, 1), D(2026, 6, 11), D(2026, 6, 21), D(2026, 7, 1)], Dates(RecurrenceFrequency.Custom, 10, D(2026, 6, 1), D(2026, 7, 5)));

    [Fact]
    public void Custom_Interval365()
    {
        var dates = Dates(RecurrenceFrequency.Custom, 365, D(2026, 1, 1), D(2028, 1, 1));

        Assert.Equal([D(2026, 1, 1), D(2027, 1, 1), D(2028, 1, 1)], dates);
    }

    [Fact]
    public void Monthly_Day31_ClampsWithoutDrift() =>
        Assert.Equal(
            [D(2026, 1, 31), D(2026, 2, 28), D(2026, 3, 31), D(2026, 4, 30), D(2026, 5, 31), D(2026, 6, 30)],
            Dates(RecurrenceFrequency.Monthly, 1, D(2026, 1, 31), D(2026, 6, 30)));

    [Fact]
    public void Monthly_Day30InLeapYear() =>
        Assert.Equal([D(2028, 1, 30), D(2028, 2, 29), D(2028, 3, 30)], Dates(RecurrenceFrequency.Monthly, 1, D(2028, 1, 30), D(2028, 3, 31)));

    [Fact]
    public void Quarterly_Day31_DoesNotDrift() =>
        Assert.Equal([D(2026, 1, 31), D(2026, 4, 30), D(2026, 7, 31), D(2026, 10, 31)],
            Dates(RecurrenceFrequency.Quarterly, 1, D(2026, 1, 31), D(2026, 12, 31)));

    [Fact]
    public void EndDate_IsInclusiveAndStopsGeneration() =>
        Assert.Equal([D(2026, 6, 10), D(2026, 7, 10)], Dates(RecurrenceFrequency.Monthly, 1, D(2026, 6, 10), D(2027, 6, 10), end: D(2026, 7, 10)));

    [Fact]
    public void EndDate_BeforeNextOccurrence_ExcludesIt() =>
        Assert.Equal([D(2026, 6, 10)], Dates(RecurrenceFrequency.Monthly, 1, D(2026, 6, 10), D(2027, 6, 10), end: D(2026, 7, 9)));

    [Fact]
    public void Horizon_IncludesUpToLimit_AndNothingBeyond()
    {
        var today = D(2026, 6, 15);

        var dates = Dates(RecurrenceFrequency.Monthly, 1, D(2026, 6, 20), today.AddMonths(12));

        Assert.Equal(12, dates.Count);
        Assert.Equal(D(2027, 5, 20), dates[^1]);
    }

    [Fact]
    public void Horizon_StartAfterLimit_IsEmpty() =>
        Assert.Empty(Dates(RecurrenceFrequency.Daily, 1, D(2030, 1, 1), D(2026, 6, 15)));

    [Fact]
    public void Nth_IsAlwaysComputedFromStart() =>
        Assert.Equal(D(2026, 3, 31), RecurrenceSchedule.Nth(RecurrenceFrequency.Monthly, 1, D(2026, 1, 31), 2));

    [Fact]
    public void Nth_OverflowReturnsNull() =>
        Assert.Null(RecurrenceSchedule.Nth(RecurrenceFrequency.Annual, 365, D(9000, 1, 1), 100));

    [Fact]
    public void NextOpenOccurrence_SkipsClosedDatesAndReturnsNullAfterEnd()
    {
        var closed = new HashSet<DateOnly> { D(2026, 7, 10) };

        Assert.Equal(D(2026, 8, 10), RecurrenceSchedule.NextOpenOccurrence(RecurrenceFrequency.Monthly, 1, D(2026, 6, 10), null, D(2026, 6, 15), closed));
        Assert.Equal(D(2026, 6, 15), RecurrenceSchedule.NextOpenOccurrence(RecurrenceFrequency.Daily, 1, D(2026, 6, 10), null, D(2026, 6, 15), new HashSet<DateOnly>()));
        Assert.Null(RecurrenceSchedule.NextOpenOccurrence(RecurrenceFrequency.Monthly, 1, D(2026, 6, 10), D(2026, 7, 10), D(2026, 6, 15), closed));
    }
}
