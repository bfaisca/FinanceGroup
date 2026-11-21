using System.Net;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Groups;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>
/// Dashboard do grupo (§16.3, D-046/D-048) com números conferidos à mão. "Hoje" fixado em 2026-11-18 e restaurado ao final.
/// Despesas de novembro: Mercado 300 (criada por Ana; Bia paga 100 + 5 de juros, Ana paga 200), Aluguel 1000 (da Dona; paga 400 + 20 de
/// juros), Cinema 100 (da Bia; sem pagamento), Cancelada 999 (ignorada). Outubro: 700 (fora do período).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class GroupDashboardApiTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 11, 18);
    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public GroupDashboardApiTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly Nov(int day) => new(2026, 11, day);

    [Fact]
    public async Task Dashboard_PaidIsPrincipalPlusInterest_PendingOnlyPrincipal_PaidByWithPercentages_NoCreatedByNoIncome()
    {
        var owner = await h.NewUserAsync("Dona");
        var ana = await h.NewUserAsync("Ana");
        var bia = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner, "Casa");
        await h.JoinAsync(owner, group.Id, ana);
        await h.JoinAsync(owner, group.Id, bia);
        var gid = group.Id;
        var food = await h.CategoryIdAsync(ana, "Alimentação");
        var housing = await h.CategoryIdAsync(owner, "Moradia");
        var leisure = await h.CategoryIdAsync(bia, "Lazer");

        var market = await h.CreateGroupTxAsync(ana, gid, "Mercado", 300m, Nov(5), categoryId: food);
        await h.PayGroupTxAsync(bia, gid, market.Id, 100m, Nov(6), interest: 5m);
        await h.PayGroupTxAsync(ana, gid, market.Id, 200m, Nov(7));
        var rent = await h.CreateGroupTxAsync(owner, gid, "Aluguel", 1000m, Nov(10), categoryId: housing);
        await h.PayGroupTxAsync(owner, gid, rent.Id, 400m, Nov(11), interest: 20m);
        await h.CreateGroupTxAsync(bia, gid, "Cinema", 100m, Nov(12), categoryId: leisure);
        var canceled = await h.CreateGroupTxAsync(bia, gid, "Cancelada", 999m, Nov(13), categoryId: leisure);
        await bia.Post($"/api/v1/groups/{gid}/transactions/{canceled.Id}/cancel");
        await h.CreateGroupTxAsync(owner, gid, "Outubro", 700m, new DateOnly(2026, 10, 15), categoryId: housing);

        var raw = await GroupSupport.RawAsync(await ana.Get($"/api/v1/groups/{gid}/dashboard?from=2026-11-01&to=2026-11-30"));
        GroupSupport.AssertNoPrivateData(raw, owner.Session.Email, ana.Session.Email, bia.Session.Email);
        using (var doc = System.Text.Json.JsonDocument.Parse(raw))
        {
            var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.DoesNotContain("createdBy", names);
            Assert.DoesNotContain(names, n => n.Contains("income", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("balance", doc.RootElement.GetProperty("totals").EnumerateObject().Select(p => p.Name).ToArray());
        }

        var d = System.Text.Json.JsonSerializer.Deserialize<GroupDashboardDto>(raw, ApiUser.Json)!;

        // Originais 1400 | principal pago 100 + 200 + 400 = 700 | juros 5 + 20 = 25 | pago 725 | pendente 1400 − 700 = 700.
        Assert.Equal(new GroupDashboardTotalsDto(1400m, 700m, 25m, 725m, 700m), d.Totals);

        // Contas Pagas (725): Dona 420 (57,93%), Ana 200 (27,59%), Bia 105 (14,48%); o pendente não conta; juros separados.
        Assert.Equal(new[] { "Dona", "Ana", "Bia" }, d.PaidBy.Select(p => p.Name).ToArray());
        Assert.Equal((420m, 20m, 57.93m), (d.PaidBy[0].PaidAmount, d.PaidBy[0].InterestAmount, d.PaidBy[0].Percent));
        Assert.Equal((200m, 0m, 27.59m), (d.PaidBy[1].PaidAmount, d.PaidBy[1].InterestAmount, d.PaidBy[1].Percent));
        Assert.Equal((105m, 5m, 14.48m), (d.PaidBy[2].PaidAmount, d.PaidBy[2].InterestAmount, d.PaidBy[2].Percent));

        // Categorias pelo valor original das despesas lançadas (sem canceladas).
        Assert.Equal(new[] { ("Moradia", 1000m, 71.43m), ("Alimentação", 300m, 21.43m), ("Lazer", 100m, 7.14m) },
            d.ExpensesByCategory.Select(c => (c.Name, c.Amount, c.Percent)).ToArray());

        var month = d.Monthly.Single(m => m.Month == "2026-11");
        Assert.Equal((1400m, 725m), (month.Expenses, month.Paid));
        Assert.Equal((700m, 0m), (d.Monthly.Single(m => m.Month == "2026-10").Expenses, d.Monthly.Single(m => m.Month == "2026-10").Paid));
        Assert.Equal(new DateOnly(2026, 11, 1), d.Period.From);
        // Período anterior (mesma duração, 02/10–31/10): 700 → +100%.
        Assert.Equal(700m, d.PreviousPeriod.Expenses);
        Assert.Equal(100m, d.PreviousPeriod.ExpensesChangePercent);
    }

    [Fact]
    public async Task Dashboard_RequiresMembership_ValidatesPeriod_AndKeepsFormerMembersInParticipation()
    {
        var owner = await h.NewUserAsync("Dona");
        var bia = await h.NewUserAsync("Bia");
        var stranger = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        await h.JoinAsync(owner, group.Id, bia);
        var tx = await h.CreateGroupTxAsync(bia, group.Id, "Da Bia", 100m, Nov(3));
        await h.PayGroupTxAsync(bia, group.Id, tx.Id, 100m, Nov(4));
        await bia.Post($"/api/v1/groups/{group.Id}/leave");

        await ReadProblemAsync(await stranger.Get($"/api/v1/groups/{group.Id}/dashboard?from=2026-11-01&to=2026-11-30"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await bia.Get($"/api/v1/groups/{group.Id}/dashboard?from=2026-11-01&to=2026-11-30"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await owner.Get($"/api/v1/groups/{group.Id}/dashboard?from=2026-11-30&to=2026-11-01"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await owner.Get($"/api/v1/groups/{group.Id}/dashboard?from=2024-01-01&to=2026-11-01"), HttpStatusCode.BadRequest, "validation_error");

        var d = await owner.GetOk<GroupDashboardDto>($"/api/v1/groups/{group.Id}/dashboard?from=2026-11-01&to=2026-11-30");
        var former = d.PaidBy.Single(p => p.Name == "Bia");
        Assert.Equal((GroupMemberStatus.Left, 100m, 100m), (former.Status, former.PaidAmount, former.Percent));
    }
}
