using FinanceApp.Application.Recurrences;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa3;

/// <summary>Gerador de recorrências (D-027): horizonte, idempotência e regeneração.</summary>
public class RecurrenceGeneratorTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private readonly FinanceApp.Infrastructure.Persistence.AppDbContext db = TestDb.Create();
    private readonly FakeClock clock = new(Now);
    private readonly RecurrenceGenerator generator;

    public RecurrenceGeneratorTests() => generator = new RecurrenceGenerator(db, clock, NullLogger<RecurrenceGenerator>.Instance);

    private async Task<RecurrenceRule> AddRuleAsync(RecurrenceFrequency frequency = RecurrenceFrequency.Monthly, int interval = 1, DateOnly? start = null, DateOnly? end = null)
    {
        var category = ExpenseCategory("Assinaturas");
        db.Categories.Add(category);
        var rule = RecurrenceRule.Create(
            UserId, "Streaming", TransactionType.Expense, 30m, category, null, null, PaymentMethod.Pix, null, frequency, interval, start ?? Today, end, Now);
        db.RecurrenceRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    [Fact]
    public async Task Generate_FillsHorizon_AndIsIdempotent()
    {
        var rule = await AddRuleAsync(start: D(2026, 11, 20));

        var first = await generator.GenerateAsync(rule.Id, Today, 12, CancellationToken.None);
        var second = await generator.GenerateAsync(rule.Id, Today, 12, CancellationToken.None);

        Assert.Equal(12, first);
        Assert.Equal(0, second);
        var occurrences = await db.Transactions.Where(t => t.RecurrenceRuleId == rule.Id).OrderBy(t => t.TransactionDate).ToListAsync();
        Assert.Equal(12, occurrences.Count);
        Assert.Equal(D(2026, 11, 20), occurrences[0].TransactionDate);
        Assert.All(occurrences, o => Assert.Equal(TransactionStatus.Planned, o.Status));
        Assert.Equal(occurrences.Count, occurrences.Select(o => o.OccurrenceDate).Distinct().Count());
    }

    [Fact]
    public async Task Generate_AdvancingTheClock_OnlyAddsTheMissingOnes()
    {
        var rule = await AddRuleAsync(start: D(2026, 11, 20));
        await generator.GenerateAsync(rule.Id, Today, 12, CancellationToken.None);

        var more = await generator.GenerateAsync(rule.Id, Today.AddMonths(3), 12, CancellationToken.None);

        Assert.Equal(3, more);
        Assert.Equal(15, await db.Transactions.CountAsync(t => t.RecurrenceRuleId == rule.Id));
    }

    [Fact]
    public async Task Generate_PastStart_CreatesPendingForPastAndPlannedForFuture()
    {
        var rule = await AddRuleAsync(start: D(2026, 9, 18), end: D(2026, 12, 18));

        await generator.GenerateAsync(rule.Id, Today, 12, CancellationToken.None);

        var statuses = await db.Transactions.Where(t => t.RecurrenceRuleId == rule.Id).OrderBy(t => t.TransactionDate).Select(t => t.Status).ToListAsync();
        Assert.Equal([TransactionStatus.Pending, TransactionStatus.Pending, TransactionStatus.Pending, TransactionStatus.Planned], statuses);
    }

    [Fact]
    public async Task Generate_RespectsEndDate_AndInactiveRuleGeneratesNothing()
    {
        var rule = await AddRuleAsync(end: Today.AddMonths(2));
        var inactive = await AddRuleAsync();
        db.RecurrenceRules.Single(r => r.Id == inactive.Id).Deactivate(Now);
        await db.SaveChangesAsync();

        Assert.Equal(3, await generator.GenerateAsync(rule.Id, Today, 12, CancellationToken.None));
        Assert.Equal(0, await generator.GenerateAsync(inactive.Id, Today, 12, CancellationToken.None));
    }

    [Fact]
    public async Task Generate_DoesNotRecreateCanceledOrDeletedHistory_ButFillsGaps()
    {
        var rule = await AddRuleAsync(start: D(2026, 11, 20));
        await generator.GenerateAsync(rule.Id, Today, 12, CancellationToken.None);
        var canceled = await db.Transactions.FirstAsync(t => t.RecurrenceRuleId == rule.Id);
        canceled.Cancel(UserId, Now);
        await db.SaveChangesAsync();

        Assert.Equal(0, await generator.GenerateAsync(rule.Id, Today, 12, CancellationToken.None));
        Assert.Equal(12, await db.Transactions.CountAsync(t => t.RecurrenceRuleId == rule.Id));
    }

    [Fact]
    public async Task GenerateAll_CoversEveryActiveRule()
    {
        await AddRuleAsync(end: Today.AddMonths(1));
        await AddRuleAsync(RecurrenceFrequency.Weekly, end: Today.AddDays(14));

        var total = await generator.GenerateAllAsync(Today, 12, CancellationToken.None);

        Assert.Equal(2 + 3, total);
    }

    [Fact]
    public async Task Prepare_IgnoredExistingDates_AreRegenerated_WithoutPersisting()
    {
        var rule = await AddRuleAsync(end: Today.AddMonths(3));
        await generator.GenerateAsync(rule.Id, Today, 12, CancellationToken.None);
        var removed = new HashSet<DateOnly> { Today.AddMonths(1), Today.AddMonths(2) };

        var prepared = await generator.PrepareAsync(rule, Today, 12, removed, CancellationToken.None);

        Assert.Equal(removed.OrderBy(d => d), prepared.Select(t => t.OccurrenceDate!.Value).OrderBy(d => d));
        Assert.Equal(4, await db.Transactions.CountAsync(t => t.RecurrenceRuleId == rule.Id));
    }
}
