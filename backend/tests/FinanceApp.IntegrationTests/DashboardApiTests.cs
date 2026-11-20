using System.Net;
using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>
/// Dashboard com números conferidos à mão. "Hoje" é fixado em 2026-06-15 (TestClock) para que cada valor seja
/// determinístico. O relógio é restaurado ao final de cada teste.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardApiTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 6, 15);

    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public DashboardApiTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    /// <summary>
    /// Cenário (usuário novo). Contas: Corrente 1000 e Poupança 500 (ref. 2026-05-01), Carteira (sem saldo),
    /// Antiga 9999 (inativa).
    /// </summary>
    private async Task<ApiUser> SeedScenarioAsync()
    {
        var user = await h.NewUserAsync();

        var checking = await h.CreateAccountAsync(user, "Corrente", balance: 1000m, referenceDate: D(5, 1));
        var savings = await h.CreateAccountAsync(user, "Poupança", balance: 500m, referenceDate: D(5, 1), type: "Savings");
        await h.CreateAccountAsync(user, "Carteira", type: "Wallet");
        var old = await h.CreateAccountAsync(user, "Antiga", balance: 9999m, referenceDate: D(5, 1));
        await user.Post($"/api/v1/accounts/{old.Id}/deactivate");

        var food = await h.CategoryIdAsync(user, "Alimentação");
        var restaurants = (await h.CreateCategoryAsync(user, "Restaurantes", parent: food)).Id;
        var housing = await h.CategoryIdAsync(user, "Moradia");
        var shopping = await h.CategoryIdAsync(user, "Compras");
        var others = await h.CategoryIdAsync(user, "Outros");
        var salary = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);
        var freelance = await h.CategoryIdAsync(user, "Freelance", CategoryType.Income);

        var t1 = await h.CreateTxAsync(user, "T1 Mercado", "Expense", 100m, food, D(5, 5), D(5, 5), checking.Id);
        await h.PayAsync(user, t1.Id, 100m, D(5, 5));
        var t2 = await h.CreateTxAsync(user, "T2 Jantar", "Expense", 50m, restaurants, D(5, 10), accountId: checking.Id);
        await h.PayAsync(user, t2.Id, 20m, D(5, 10));
        await h.CreateTxAsync(user, "T3 Aluguel", "Expense", 1000m, housing, D(6, 1), D(6, 20), checking.Id);
        var t4 = await h.CreateTxAsync(user, "T4 Salário", "Income", 3000m, salary, D(5, 2), accountId: checking.Id);
        await h.PayAsync(user, t4.Id, 3000m, D(5, 5));
        var t5 = await h.CreateTxAsync(user, "T5 Bônus", "Income", 500m, salary, D(5, 3), D(6, 13), savings.Id);
        await h.PayAsync(user, t5.Id, 100m, D(6, 1));
        var t6 = await h.CreateTxAsync(user, "T6 Cancelada", "Expense", 777m, food, D(5, 4), accountId: checking.Id);
        await user.Post($"/api/v1/transactions/{t6.Id}/cancel");
        await h.CreateTxAsync(user, "T7 Março", "Expense", 200m, food, D(3, 4));
        await h.CreateTxAsync(user, "T8 Antiga", "Expense", 999m, food, D(10, 1, 2025));
        await h.CreateTxAsync(user, "T9 Longe", "Expense", 10m, others, D(6, 1), D(7, 25));
        await h.CreateTxAsync(user, "T10 Compra", "Expense", 60m, shopping, D(6, 1), D(7, 15), checking.Id, status: "Planned");
        await h.CreateTxAsync(user, "T11 Freela", "Income", 700m, freelance, D(4, 10));
        return user;
    }

    [Fact]
    public async Task Dashboard_MatchesHandComputedNumbers()
    {
        var user = await SeedScenarioAsync();

        var dashboard = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-05-01&to=2026-06-30");

        Assert.Equal(new DashboardPeriodDto(D(5, 1), D(6, 30)), dashboard.Period);
        Assert.Equal(FixedToday, dashboard.Today);

        // Saldos: Corrente 1000 + 3000 (T4) - 100 (T1) - 50 (T2) - 1000 (T3) = 2850 (T10 vence em 15/07, fora; T6 cancelada);
        // Poupança 500 + 500 (T5, vencimento 13/06) = 1000. Carteira sem saldo; Antiga inativa fora.
        Assert.Equal(1500m, dashboard.Balance.ReportedTotal);
        Assert.Equal(3850m, dashboard.Balance.ProjectedTotal);
        Assert.Equal(D(6, 30), dashboard.Balance.ProjectedAsOf);
        Assert.Equal(2, dashboard.Balance.AccountsWithBalance);
        Assert.Equal(1, dashboard.Balance.AccountsWithoutBalance);

        // Totais por transactionDate no período, sem canceladas.
        Assert.Equal(3500m, dashboard.Totals.Income);                 // T4 3000 + T5 500
        Assert.Equal(1220m, dashboard.Totals.Expense);                // T1 100 + T2 50 + T3 1000 + T9 10 + T10 60
        Assert.Equal(2280m, dashboard.Totals.Result);
        Assert.Equal(3100m, dashboard.Totals.ReceivedIncome);         // 3000 + 100 (parcial)
        Assert.Equal(400m, dashboard.Totals.PendingIncome);
        Assert.Equal(120m, dashboard.Totals.PaidExpense);             // 100 + 20 (parcial)
        Assert.Equal(1100m, dashboard.Totals.PendingExpense);

        // Despesas por categoria RAIZ (Restaurantes soma em Alimentação), ordem decrescente.
        Assert.Equal(["Moradia", "Alimentação", "Compras", "Outros"], dashboard.ExpensesByCategory.Select(c => c.CategoryName));
        Assert.Equal([1000m, 150m, 60m, 10m], dashboard.ExpensesByCategory.Select(c => c.Total));
        Assert.Equal([81.97m, 12.30m, 4.92m, 0.82m], dashboard.ExpensesByCategory.Select(c => c.Percentage));
        var foodId = await h.CategoryIdAsync(user, "Alimentação");
        Assert.Equal(foodId, dashboard.ExpensesByCategory.Single(c => c.CategoryName == "Alimentação").CategoryId);
        Assert.All(dashboard.ExpensesByCategory, c => Assert.NotNull(c.Color));

        // Próximos vencimentos: restante > 0, não cancelados, vencimento até hoje+30 (15/07), atrasadas incluídas.
        Assert.Equal(["T5 Bônus", "T3 Aluguel", "T10 Compra"], dashboard.UpcomingDues.Select(t => t.Description));
        Assert.Equal([TransactionStatus.Overdue, TransactionStatus.Pending, TransactionStatus.Planned], dashboard.UpcomingDues.Select(t => t.Status));
        Assert.Equal(400m, dashboard.UpcomingDues[0].RemainingAmount);

        // Histórico: 6 meses terminando em 2026-06 (T8 de 2025-10 e a cancelada T6 ficam de fora).
        Assert.Equal(["2026-01", "2026-02", "2026-03", "2026-04", "2026-05", "2026-06"], dashboard.MonthlyHistory.Select(m => m.Month));
        Assert.Equal([0m, 0m, 0m, 700m, 3500m, 0m], dashboard.MonthlyHistory.Select(m => m.Income));
        Assert.Equal([0m, 0m, 200m, 0m, 150m, 1070m], dashboard.MonthlyHistory.Select(m => m.Expense));
        Assert.Equal([0m, 0m, -200m, 700m, 3350m, -1070m], dashboard.MonthlyHistory.Select(m => m.Result));
    }

    [Fact]
    public async Task Dashboard_ContractShape()
    {
        var user = await SeedScenarioAsync();

        var json = await user.GetJson("/api/v1/dashboard?from=2026-05-01&to=2026-06-30");

        Assert.Equal(
            ["balance", "creditCards", "expensesByCategory", "monthlyHistory", "period", "today", "totals", "upcomingDues"],
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            ["accountsWithBalance", "accountsWithoutBalance", "projectedAsOf", "projectedTotal", "reportedTotal"],
            json.GetProperty("balance").EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            ["expense", "income", "paidExpense", "pendingExpense", "pendingIncome", "receivedIncome", "result"],
            json.GetProperty("totals").EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            ["categoryId", "categoryName", "color", "percentage", "total"],
            json.GetProperty("expensesByCategory")[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            ["expense", "income", "month", "result"],
            json.GetProperty("monthlyHistory")[0].EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("2026-06-15", json.GetProperty("today").GetString());
        Assert.Equal("2026-05-01", json.GetProperty("period").GetProperty("from").GetString());
        Assert.Equal("Overdue", json.GetProperty("upcomingDues")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Dashboard_ProjectionFollowsThePeriodEnd_AndTotalsFollowThePeriod()
    {
        var user = await SeedScenarioAsync();

        // to = 10/06: T3 (vence 20/06) e T5 (vence 13/06) ainda não entram na projeção.
        var early = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-05-01&to=2026-06-10");
        Assert.Equal(1500m, early.Balance.ReportedTotal);
        Assert.Equal(1000m + 3000m - 100m - 50m + 500m, early.Balance.ProjectedTotal); // 3850 (Corrente) + 500 (Poupança) = 4350
        Assert.Equal(D(6, 10), early.Balance.ProjectedAsOf);
        Assert.Equal(3500m, early.Totals.Income);
        Assert.Equal(1220m, early.Totals.Expense);

        // Período mais antigo: histórico acompanha o mês de `to` (2026-03 → 2025-10 a 2026-03).
        var old = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-01-01&to=2026-03-31");
        Assert.Equal(["2025-10", "2025-11", "2025-12", "2026-01", "2026-02", "2026-03"], old.MonthlyHistory.Select(m => m.Month));
        Assert.Equal([999m, 0m, 0m, 0m, 0m, 200m], old.MonthlyHistory.Select(m => m.Expense));
        Assert.Equal(200m, old.Totals.Expense);
        Assert.Equal(0m, old.Totals.Income);
        Assert.Equal(["Alimentação"], old.ExpensesByCategory.Select(c => c.CategoryName));
        Assert.Equal(100m, old.ExpensesByCategory[0].Percentage);
        // Projeção antes do snapshot de referência é o próprio saldo informado.
        Assert.Equal(1500m, old.Balance.ProjectedTotal);
        // Os vencimentos próximos independem do período.
        Assert.Equal(3, old.UpcomingDues.Count);
    }

    [Fact]
    public async Task Dashboard_EmptyUser_ReturnsZeroedStructure()
    {
        var user = await h.NewUserAsync();

        var dashboard = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-06-01&to=2026-06-30");

        Assert.Null(dashboard.Balance.ReportedTotal);
        Assert.Null(dashboard.Balance.ProjectedTotal);
        Assert.Equal(0, dashboard.Balance.AccountsWithBalance);
        Assert.Equal(0, dashboard.Balance.AccountsWithoutBalance);
        Assert.Equal(new DashboardTotalsDto(0m, 0m, 0m, 0m, 0m, 0m, 0m), dashboard.Totals);
        Assert.Empty(dashboard.ExpensesByCategory);
        Assert.Empty(dashboard.UpcomingDues);
        Assert.Equal(6, dashboard.MonthlyHistory.Count);
        Assert.All(dashboard.MonthlyHistory, m => Assert.Equal((0m, 0m, 0m), (m.Income, m.Expense, m.Result)));
    }

    [Fact]
    public async Task Dashboard_AccountWithoutSnapshotOnly_CountsAsWithoutBalance()
    {
        var user = await h.NewUserAsync();
        await h.CreateAccountAsync(user, "Sem saldo");
        var dashboard = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-06-01&to=2026-06-30");
        Assert.Null(dashboard.Balance.ProjectedTotal);
        Assert.Equal(1, dashboard.Balance.AccountsWithoutBalance);
    }

    [Fact]
    public async Task Dashboard_UpcomingDues_AreLimitedToTen_Ordered_AndExcludeSettledAndCanceled()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        for (var i = 0; i < 12; i++)
        {
            await h.CreateTxAsync(user, $"Conta {i:00}", "Expense", 10m, food, D(6, 1), FixedToday.AddDays(i - 3));
        }

        var settled = await h.CreateTxAsync(user, "Quitada", "Expense", 10m, food, D(6, 1), FixedToday);
        await h.PayAsync(user, settled.Id, 10m, FixedToday);
        var canceled = await h.CreateTxAsync(user, "Cancelada", "Expense", 10m, food, D(6, 1), FixedToday);
        await user.Post($"/api/v1/transactions/{canceled.Id}/cancel");

        var dashboard = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-06-01&to=2026-06-30");

        Assert.Equal(10, dashboard.UpcomingDues.Count);
        Assert.Equal(
            Enumerable.Range(0, 10).Select(i => $"Conta {i:00}"),
            dashboard.UpcomingDues.Select(t => t.Description));
        Assert.Equal([true, true, true], dashboard.UpcomingDues.Take(3).Select(t => t.Status == TransactionStatus.Overdue));
        Assert.Equal(FixedToday.AddDays(-3), dashboard.UpcomingDues[0].DueDate);
    }

    [Fact]
    public async Task Dashboard_PartialPayments_AreSplitBetweenPaidAndPending()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var tx = await h.CreateTxAsync(user, "Parcial", "Expense", 200m, food, D(6, 2));
        await h.PayAsync(user, tx.Id, 75.25m, D(6, 3));

        var dashboard = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-06-01&to=2026-06-30");

        Assert.Equal(200m, dashboard.Totals.Expense);
        Assert.Equal(75.25m, dashboard.Totals.PaidExpense);
        Assert.Equal(124.75m, dashboard.Totals.PendingExpense);
    }

    [Fact]
    public async Task Dashboard_Validation()
    {
        var user = await h.NewUserAsync();

        var missing = await ReadProblemAsync(await user.Get("/api/v1/dashboard"), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(missing.GetProperty("errors").TryGetProperty("from", out _));
        Assert.True(missing.GetProperty("errors").TryGetProperty("to", out _));

        await ReadProblemAsync(await user.Get("/api/v1/dashboard?from=2026-06-30&to=2026-06-01"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await user.Get("/api/v1/dashboard?from=2026-01-01&to=2027-01-02"), HttpStatusCode.BadRequest, "validation_error"); // 367 dias
        await ReadProblemAsync(await user.Get("/api/v1/dashboard?from=amanha&to=2026-06-01"), HttpStatusCode.BadRequest, "validation_error");

        Assert.Equal(HttpStatusCode.OK, (await user.Get("/api/v1/dashboard?from=2026-01-01&to=2026-12-31")).StatusCode); // 365 dias
        Assert.Equal(HttpStatusCode.OK, (await user.Get("/api/v1/dashboard?from=2024-01-01&to=2024-12-31")).StatusCode); // 366 dias
        Assert.Equal(HttpStatusCode.OK, (await user.Get("/api/v1/dashboard?from=2026-06-01&to=2026-06-01")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_IsolatesUsers()
    {
        var owner = await SeedScenarioAsync();
        var intruder = await h.NewUserAsync();

        var theirs = await intruder.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-05-01&to=2026-06-30");
        var mine = await owner.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-05-01&to=2026-06-30");

        Assert.Equal(new DashboardTotalsDto(0m, 0m, 0m, 0m, 0m, 0m, 0m), theirs.Totals);
        Assert.Null(theirs.Balance.ProjectedTotal);
        Assert.Empty(theirs.UpcomingDues);
        Assert.Empty(theirs.ExpensesByCategory);
        Assert.Equal(3500m, mine.Totals.Income);
    }
}
