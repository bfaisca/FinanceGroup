using System.Net;
using FinanceApp.Application.Accounts;
using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Common;
using FinanceApp.Application.Reports;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>D-033: movimentações de grupo não vazam para o contexto individual; só pagamentos feitos com a conta do usuário entram no saldo projetado.</summary>
[Collection(ApiCollection.Name)]
public sealed class GroupIsolationAndProjectionTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 11, 18);
    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public GroupIsolationAndProjectionTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly Nov(int day) => new(2026, 11, day);

    [Fact]
    public async Task IndividualContext_NeverSeesGroupTransactions()
    {
        var owner = await h.NewUserAsync("Dona");
        var group = await h.CreateGroupAsync(owner);
        var food = await h.CategoryIdAsync(owner, "Alimentação");
        var groupTx = await h.CreateGroupTxAsync(owner, group.Id, "Do grupo", 500m, Nov(10), categoryId: food);
        await h.PayGroupTxAsync(owner, group.Id, groupTx.Id, 200m, Nov(11));
        await h.CreateTxAsync(owner, "Individual", "Expense", 40m, food, Nov(12));

        // D-060/D-061: o pagamento do próprio usuário no grupo aparece (somente leitura) e conta como pago no dashboard.
        var list = await h.ListAsync(owner);
        Assert.Equal(new[] { "Individual", "Do grupo" }, list.Items.Select(t => t.Description).OrderBy(d => d == "Do grupo").ToArray());
        await ReadProblemAsync(await owner.Get($"/api/v1/transactions/{groupTx.Id}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await owner.Get($"/api/v1/transactions/{groupTx.Id}/history"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await owner.Post($"/api/v1/transactions/{groupTx.Id}/payments", new { amount = 10m, paymentDate = Nov(12) }), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await owner.Post($"/api/v1/transactions/{groupTx.Id}/cancel"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await owner.Delete($"/api/v1/transactions/{groupTx.Id}"), HttpStatusCode.NotFound, "not_found");

        var dashboard = await owner.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-11-01&to=2026-11-30");
        Assert.Equal((240m, 200m), (dashboard.Totals.Expense, dashboard.Totals.PaidExpense));
        Assert.Equal(240m, Assert.Single(dashboard.ExpensesByCategory).Total);
        Assert.Empty(dashboard.UpcomingDues.Where(t => t.Description == "Do grupo"));
        var summary = await owner.GetOk<SummaryReportDto>("/api/v1/reports/summary?from=2026-11-01&to=2026-11-30");
        Assert.Equal((40m, 0m), (summary.Totals.Expense, summary.Totals.PaidExpense)); // relatórios: só individual
        var byCategory = await owner.GetOk<System.Text.Json.JsonElement>("/api/v1/reports/by-category?from=2026-11-01&to=2026-11-30");
        Assert.DoesNotContain("Do grupo", byCategory.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(40m, (await owner.GetOk<PagedResult<TransactionDto>>("/api/v1/transactions?status=Pending")).Items.Sum(t => t.Amount));
    }

    /// <summary>
    /// Conta da Ana: snapshot 1000 em 01/11. Individual: despesa paga com a conta em 05/11 (200).
    /// Pagamentos de grupo da Ana: Despesa G1 (500): 01/11 com conta 100 (mesmo dia do snapshot: fora), 10/11 com conta 50 (-50),
    /// 11/11 sem conta 30 (fora); despesa G2 (200): 12/11 com conta 80 de principal + 5 de juros (-85, D-048).
    /// Pagamento da Bia na G1 com a conta dela (40) não afeta a Ana. Projetado em 18/11 = 1000 - 200 - 50 - 85 = 665.
    /// </summary>
    [Fact]
    public async Task ProjectedBalance_CountsOnlyGroupPaymentsMadeWithTheUsersOwnAccount()
    {
        var owner = await h.NewUserAsync("Dona");
        var ana = await h.NewUserAsync("Ana");
        var bia = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner);
        await h.JoinAsync(owner, group.Id, ana);
        await h.JoinAsync(owner, group.Id, bia);
        var account = await h.CreateAccountAsync(ana, "Corrente", balance: 1000m, referenceDate: Nov(1));
        var biaAccount = await h.CreateAccountAsync(bia, "Da Bia", balance: 500m, referenceDate: Nov(1));
        var food = await h.CategoryIdAsync(ana, "Alimentação");

        var individual = await h.CreateTxAsync(ana, "Individual", "Expense", 200m, food, Nov(5), accountId: account.Id);
        await h.PayAsync(ana, individual.Id, 200m, Nov(5), account.Id);
        var g1 = await h.CreateGroupTxAsync(owner, group.Id, "G1", 500m, Nov(2));
        await h.PayGroupTxAsync(ana, group.Id, g1.Id, 100m, Nov(1), account.Id);
        await h.PayGroupTxAsync(ana, group.Id, g1.Id, 50m, Nov(10), account.Id);
        await h.PayGroupTxAsync(ana, group.Id, g1.Id, 30m, Nov(11));
        await h.PayGroupTxAsync(bia, group.Id, g1.Id, 40m, Nov(12), biaAccount.Id);
        var g2 = await h.CreateGroupTxAsync(bia, group.Id, "G2", 200m, Nov(3));
        await h.PayGroupTxAsync(ana, group.Id, g2.Id, 80m, Nov(12), account.Id, interest: 5m);

        var anaAccount = (await ana.GetOk<List<AccountDto>>("/api/v1/accounts")).Single();
        Assert.Equal(665m, anaAccount.ProjectedBalance);
        Assert.Equal(FixedToday, anaAccount.ProjectedAsOf);
        var biaProjected = (await bia.GetOk<List<AccountDto>>("/api/v1/accounts")).Single();
        Assert.Equal(460m, biaProjected.ProjectedBalance);
        // A Dona não tem conta: nada a projetar; o pagamento da Ana não aparece em lugar nenhum dela.
        Assert.Empty(await owner.GetOk<List<AccountDto>>("/api/v1/accounts"));
    }

    /// <summary>D-060: pagamentos de grupo feitos com conta própria aparecem na listagem individual (somente leitura), identificando o grupo.</summary>
    [Fact]
    public async Task IndividualList_ShowsOwnGroupPaymentsWithAccount_IdentifyingTheGroup()
    {
        var owner = await h.NewUserAsync("Dona");
        var ana = await h.NewUserAsync("Ana");
        var bia = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner, "Casa da praia");
        await h.JoinAsync(owner, group.Id, ana);
        await h.JoinAsync(owner, group.Id, bia);
        var account = await h.CreateAccountAsync(ana, "Corrente", balance: 1000m, referenceDate: Nov(1));
        var biaAccount = await h.CreateAccountAsync(bia, "Da Bia", balance: 500m, referenceDate: Nov(1));
        var food = await h.CategoryIdAsync(ana, "Alimentação");

        await h.CreateTxAsync(ana, "Individual", "Expense", 40m, food, Nov(12), accountId: account.Id);
        var g1 = await h.CreateGroupTxAsync(owner, group.Id, "Aluguel", 500m, Nov(2));
        await h.PayGroupTxAsync(ana, group.Id, g1.Id, 80m, Nov(10), account.Id, interest: 5m);
        await h.PayGroupTxAsync(ana, group.Id, g1.Id, 30m, Nov(11)); // sem conta: também aparece (sem conta), mas não afeta o saldo
        await h.PayGroupTxAsync(bia, group.Id, g1.Id, 40m, Nov(12), biaAccount.Id); // de outra pessoa: não aparece para a Ana

        var list = await ana.GetOk<PagedResult<TransactionDto>>("/api/v1/transactions?sort=transactionDate");

        Assert.Equal(3, list.TotalCount);
        // Ordem por data: Individual (12/11), Aluguel sem conta (11/11) e Aluguel com conta (10/11) — crescente.
        Assert.Equal(new[] { "Aluguel", "Aluguel", "Individual" }, list.Items.Select(t => t.Description).ToArray());
        var entry = list.Items[0];
        Assert.Equal(85m, entry.Amount);
        Assert.Equal(TransactionStatus.Paid, entry.Status);
        Assert.Equal(Nov(10), entry.TransactionDate);
        Assert.Equal(account.Id, entry.AccountId);
        Assert.NotNull(entry.GroupPayment);
        Assert.Equal((group.Id, "Casa da praia", g1.Id), (entry.GroupPayment!.GroupId, entry.GroupPayment.GroupName, entry.GroupPayment.GroupTransactionId));
        Assert.NotNull(list.Items[1].GroupPayment);
        Assert.Null(list.Items[1].AccountId);
        Assert.Null(list.Items[2].GroupPayment);

        // Paginação com intercalação: página 2 de tamanho 1 devolve o segundo item da ordem.
        var page2 = await ana.GetOk<PagedResult<TransactionDto>>("/api/v1/transactions?sort=-transactionDate&pageSize=1&page=2");
        Assert.Equal("Aluguel", Assert.Single(page2.Items).Description);
        Assert.Equal(3, page2.TotalCount);

        // Filtros sem equivalente em pagamentos de grupo os excluem; a conta filtra normalmente.
        var pending = await ana.GetOk<PagedResult<TransactionDto>>("/api/v1/transactions?status=Pending");
        Assert.Equal("Individual", Assert.Single(pending.Items).Description);
        Assert.Equal(2, (await ana.GetOk<PagedResult<TransactionDto>>($"/api/v1/transactions?accountId={account.Id}")).TotalCount);
        Assert.Equal(1, (await ana.GetOk<PagedResult<TransactionDto>>("/api/v1/transactions?search=aluguel&from=2026-11-11")).TotalCount);
        Assert.Equal(2, (await ana.GetOk<PagedResult<TransactionDto>>("/api/v1/transactions?search=aluguel")).TotalCount);

        // D-061: o que a Ana pagou no grupo (85 + 30) soma às despesas pagas do dashboard; relatórios seguem só individuais.
        var dashboard = await ana.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-11-01&to=2026-11-30");
        Assert.Equal((155m, 115m), (dashboard.Totals.Expense, dashboard.Totals.PaidExpense));
        Assert.Equal(-155m, dashboard.Totals.Result);
        Assert.Equal(155m, dashboard.MonthlyHistory.Single(m => m.Month == "2026-11").Expense);
        await ReadProblemAsync(await ana.Get($"/api/v1/transactions/{entry.Id}"), HttpStatusCode.NotFound, "not_found");
    }
}
