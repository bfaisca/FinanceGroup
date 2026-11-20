using System.Net;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Groups;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>
/// Dashboard do grupo (D-034) com números conferidos à mão. "Hoje" fixado em 2026-11-18 e restaurado ao final.
/// Despesas de novembro: Mercado 300 (criada por Ana; pagos Bia 100 + Ana 200), Aluguel 1000 (criada pela Dona; paga Dona 400),
/// Cinema 100 (criada por Bia; sem pagamento), Cancelada 999 (ignorada). Receita: Reembolso 200 (criada por Ana; recebida por Ana).
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
    public async Task Dashboard_SeparatesCreatedByFromPaidBy_WithHandCalculatedNumbers()
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
        var refund = await h.CategoryIdAsync(ana, "Reembolsos", CategoryType.Income);

        var market = await h.CreateGroupTxAsync(ana, gid, "Mercado", 300m, Nov(5), categoryId: food);
        await h.PayGroupTxAsync(bia, gid, market.Id, 100m, Nov(6));
        await h.PayGroupTxAsync(ana, gid, market.Id, 200m, Nov(7));
        var rent = await h.CreateGroupTxAsync(owner, gid, "Aluguel", 1000m, Nov(10), categoryId: housing);
        await h.PayGroupTxAsync(owner, gid, rent.Id, 400m, Nov(11));
        await h.CreateGroupTxAsync(bia, gid, "Cinema", 100m, Nov(12), categoryId: leisure);
        var canceled = await h.CreateGroupTxAsync(bia, gid, "Cancelada", 999m, Nov(13), categoryId: leisure);
        await bia.Post($"/api/v1/groups/{gid}/transactions/{canceled.Id}/cancel");
        var income = await h.CreateGroupTxAsync(ana, gid, "Reembolso", 200m, Nov(14), "Income", refund);
        await h.PayGroupTxAsync(ana, gid, income.Id, 200m, Nov(15));
        // Outubro (período anterior): 700 de despesa.
        var october = await h.CreateGroupTxAsync(owner, gid, "Outubro", 700m, new DateOnly(2026, 10, 15), categoryId: housing);

        var raw = await GroupSupport.RawAsync(await ana.Get($"/api/v1/groups/{gid}/dashboard?from=2026-11-01&to=2026-11-30"));
        GroupSupport.AssertNoPrivateData(raw, owner.Session.Email, ana.Session.Email, bia.Session.Email);
        var d = System.Text.Json.JsonSerializer.Deserialize<GroupDashboardDto>(raw, ApiUser.Json)!;

        Assert.Equal((1400m, 700m, 700m, 200m, 200m, 0m, -1200m),
            (d.Totals.Expenses, d.Totals.ExpensesPaid, d.Totals.ExpensesPending, d.Totals.Income, d.Totals.IncomeReceived, d.Totals.IncomePending, d.Totals.Balance));

        // Critério 1: quem cadastrou (despesa 1400 → Dona 1000, Ana 300, Bia 100).
        var created = d.CreatedBy.ToDictionary(p => p.Name);
        Assert.Equal((1000m, 71.43m), (created["Dona"].ExpenseAmount, created["Dona"].ExpensePercent));
        Assert.Equal((300m, 21.43m, 200m, 100m), (created["Ana"].ExpenseAmount, created["Ana"].ExpensePercent, created["Ana"].IncomeAmount, created["Ana"].IncomePercent));
        Assert.Equal((100m, 7.14m, 0m), (created["Bia"].ExpenseAmount, created["Bia"].ExpensePercent, created["Bia"].IncomeAmount));

        // Critério 2: quem efetivamente pagou (700 → Dona 400, Ana 200, Bia 100); o pendente não conta.
        var paid = d.PaidBy.ToDictionary(p => p.Name);
        Assert.Equal((400m, 57.14m), (paid["Dona"].ExpenseAmount, paid["Dona"].ExpensePercent));
        Assert.Equal((200m, 28.57m, 200m, 100m), (paid["Ana"].ExpenseAmount, paid["Ana"].ExpensePercent, paid["Ana"].IncomeAmount, paid["Ana"].IncomePercent));
        Assert.Equal((100m, 14.29m), (paid["Bia"].ExpenseAmount, paid["Bia"].ExpensePercent));
        Assert.NotEqual(created["Dona"].ExpenseAmount, paid["Dona"].ExpenseAmount);

        // Categorias (despesas lançadas, sem canceladas).
        Assert.Equal(new[] { ("Moradia", 1000m, 71.43m), ("Alimentação", 300m, 21.43m), ("Lazer", 100m, 7.14m) },
            d.ExpensesByCategory.Select(c => (c.Name, c.Amount, c.Percent)).ToArray());

        var month = d.Monthly.Single(m => m.Month == "2026-11");
        Assert.Equal((200m, 1400m), (month.Income, month.Expenses));
        Assert.Equal(new DateOnly(2026, 11, 1), d.Period.From);
        // Período anterior (mesma duração): outubro tem 700 → +100%.
        Assert.Equal(700m, d.PreviousPeriod.Expenses);
        Assert.Equal(100m, d.PreviousPeriod.ExpensesChangePercent);
        Assert.Null(d.PreviousPeriod.IncomeChangePercent);
        Assert.NotEqual(Guid.Empty, october.Id);
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
        var former = d.CreatedBy.Single(p => p.Name == "Bia");
        Assert.Equal((GroupMemberStatus.Left, 100m, 100m), (former.Status, former.ExpenseAmount, former.ExpensePercent));
        Assert.Equal(100m, d.PaidBy.Single(p => p.Name == "Bia").ExpenseAmount);
    }
}
