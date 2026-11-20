using System.Net;
using System.Text.Json;
using FinanceApp.Application.Installments;
using FinanceApp.Application.Reports;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;
using static FinanceApp.IntegrationTests.Infrastructure.Etapa3Support;

namespace FinanceApp.IntegrationTests;

/// <summary>Relatórios (contrato §14, D-028) com valores conferidos à mão. "Hoje" fixado em 2026-06-15.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReportsApiTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 6, 15);
    private const string June = "from=2026-06-01&to=2026-06-30";

    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public ReportsApiTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    /// <summary>
    /// Cenário de junho/2026 — despesas: Aluguel 1000 (Moradia, Conta B, BankSlip, 06-05); Mercado 300 (Alimentação, Conta A, Pix, 06-08);
    /// Restaurante 50 (Restaurantes ⊂ Alimentação, Conta A, Pix, 06-09); Lâmpadas 40 (Moradia, cartão da Conta A, 06-12);
    /// Feira 60 (Alimentação, sem conta, 06-20). Total 1450. Receita: Salário 3500 (06-01). Excluídas: despesa cancelada de 999 e despesa de julho de 77.
    /// </summary>
    private async Task<ApiUser> SeedJuneAsync()
    {
        var user = await h.NewUserAsync();
        var accountA = await h.CreateAccountAsync(user, "Conta A");
        var accountB = await h.CreateAccountAsync(user, "Conta B");
        var card = await h.CreateCardAsync(user, "Visa", accountId: accountA.Id);
        var housing = await h.CategoryIdAsync(user, "Moradia");
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var salary = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);
        var restaurants = await h.CreateCategoryAsync(user, "Restaurantes", parent: food);

        async Task Tx(string description, string type, decimal amount, Guid category, DateOnly date, Guid? account = null, string? method = null)
        {
            var response = await user.Post("/api/v1/transactions", FinancialHarness.TxBody(description, type, amount, category, date, accountId: account, paymentMethod: method));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        await Tx("Aluguel", "Expense", 1000m, housing, D(6, 5), accountB.Id, "BankSlip");
        await Tx("Mercado", "Expense", 300m, food, D(6, 8), accountA.Id, "Pix");
        await Tx("Restaurante", "Expense", 50m, restaurants.Id, D(6, 9), accountA.Id, "Pix");
        await h.CreateCardPurchaseAsync(user, card.Id, "Lâmpadas", 40m, housing, D(6, 12));
        await Tx("Feira", "Expense", 60m, food, D(6, 20));
        await Tx("Salário", "Income", 3500m, salary, D(6, 1), accountA.Id);
        var canceled = await h.CreateTxAsync(user, "Cancelada", "Expense", 999m, food, D(6, 10));
        await user.Post($"/api/v1/transactions/{canceled.Id}/cancel");
        await Tx("Julho", "Expense", 77m, food, D(7, 3));
        return user;
    }

    [Fact]
    public async Task Summary_Month_And_Day_AreHandChecked()
    {
        var user = await SeedJuneAsync();

        var month = await user.GetOk<SummaryReportDto>($"/api/v1/reports/summary?{June}");
        var day = await user.GetOk<SummaryReportDto>($"/api/v1/reports/summary?{June}&groupBy=day");

        Assert.Equal((3500m, 1450m, 2050m), (month.Totals.Income, month.Totals.Expense, month.Totals.Result));
        var only = Assert.Single(month.Series);
        Assert.Equal(("2026-06", 3500m, 1450m, 2050m), (only.Key, only.Income, only.Expense, only.Result));
        Assert.Equal(30, day.Series.Count);
        Assert.Equal(("2026-06-01", 3500m, 0m, 3500m), (day.Series[0].Key, day.Series[0].Income, day.Series[0].Expense, day.Series[0].Result));
        Assert.Equal(1000m, day.Series.Single(s => s.Key == "2026-06-05").Expense);
        Assert.Equal(0m, day.Series.Single(s => s.Key == "2026-06-30").Expense);
        Assert.Equal(1450m, day.Series.Sum(s => s.Expense));
    }

    [Fact]
    public async Task ByCategory_GroupsSubcategoriesUnderRoot_WithPercentages()
    {
        var user = await SeedJuneAsync();

        var report = await user.GetOk<CategoryReportDto>($"/api/v1/reports/by-category?{June}");

        Assert.Equal(TransactionType.Expense, report.Type);
        Assert.Equal(1450m, report.Total);
        Assert.Equal(["Moradia", "Alimentação"], report.Items.Select(i => i.CategoryName));
        Assert.Equal((1040m, 71.72m), (report.Items[0].Total, report.Items[0].Percentage));
        Assert.Empty(report.Items[0].Subcategories);
        Assert.Equal((410m, 28.28m), (report.Items[1].Total, report.Items[1].Percentage));
        var sub = Assert.Single(report.Items[1].Subcategories);
        Assert.Equal(("Restaurantes", 50m, 12.20m), (sub.CategoryName, sub.Total, sub.Percentage));

        var income = await user.GetOk<CategoryReportDto>($"/api/v1/reports/by-category?{June}&type=Income");
        Assert.Equal(3500m, income.Total);
        Assert.Equal(100m, Assert.Single(income.Items).Percentage);
    }

    [Fact]
    public async Task ByAccount_UsesCardAccount_AndGroupsNoAccount()
    {
        var user = await SeedJuneAsync();

        var report = await user.GetOk<AccountReportDto>($"/api/v1/reports/by-account?{June}");

        Assert.Equal(1450m, report.Total);
        Assert.Equal(["Conta B", "Conta A", "Sem conta"], report.Items.Select(i => i.AccountName));
        Assert.Equal([1000m, 390m, 60m], report.Items.Select(i => i.Total));
        Assert.Equal([68.97m, 26.90m, 4.14m], report.Items.Select(i => i.Percentage));
        Assert.Null(report.Items[2].AccountId);
    }

    [Fact]
    public async Task ByPaymentMethod_GroupsIncludingNull()
    {
        var user = await SeedJuneAsync();

        var report = await user.GetOk<PaymentMethodReportDto>($"/api/v1/reports/by-payment-method?{June}");

        Assert.Equal(1450m, report.Total);
        Assert.Equal<PaymentMethod?>([PaymentMethod.BankSlip, PaymentMethod.Pix, null, PaymentMethod.CreditCard], report.Items.Select(i => i.PaymentMethod));
        Assert.Equal([1000m, 350m, 60m, 40m], report.Items.Select(i => i.Total));
        Assert.Equal([68.97m, 24.14m, 4.14m, 2.76m], report.Items.Select(i => i.Percentage));
    }

    [Fact]
    public async Task MonthlyEvolution_FillsEmptyMonths_AndAccumulatesResult()
    {
        var user = await SeedJuneAsync();

        var report = await user.GetOk<MonthlyEvolutionReportDto>("/api/v1/reports/monthly-evolution?months=3&endMonth=2026-07");

        Assert.Equal(["2026-05", "2026-06", "2026-07"], report.Items.Select(i => i.Month));
        Assert.Equal([0m, 3500m, 0m], report.Items.Select(i => i.Income));
        Assert.Equal([0m, 1450m, 77m], report.Items.Select(i => i.Expense));
        Assert.Equal([0m, 2050m, -77m], report.Items.Select(i => i.Result));
        Assert.Equal([0m, 2050m, 1973m], report.Items.Select(i => i.CumulativeResult));

        var defaults = await user.GetOk<MonthlyEvolutionReportDto>("/api/v1/reports/monthly-evolution");
        Assert.Equal(12, defaults.Items.Count);
        Assert.Equal("2026-06", defaults.Items[^1].Month);
    }

    [Fact]
    public async Task Installments_CommitmentRemainingAndOpenPlans_AreHandChecked()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var card = await h.CreateCardAsync(user, "Visa", closingDay: 10, dueDay: 20);
        var p = await h.CreatePlanAsync(user, "P", 90m, 3, D(5, 10), category);      // 05-10, 06-10, 07-10 (30 cada)
        var q = await h.CreatePlanAsync(user, "Q", 100m, 2, D(6, 20), category);     // 06-20, 07-20 (50 cada)
        var s = await h.CreatePlanAsync(user, "S", 60m, 2, D(6, 5), category, cardId: card.Id); // vencimentos 06-20 e 07-20 (30 cada)
        var r = await h.CreatePlanAsync(user, "R", 20m, 2, D(6, 25), category);
        await user.Post($"/api/v1/installment-plans/{r.Id}/cancel");
        await h.PayAsync(user, p.Installments[0].Id, 10m, D(5, 12));                  // P1: restam 20 (atrasada → entra em junho)

        var report = await user.GetOk<InstallmentsReportDto>("/api/v1/reports/installments?months=3");

        Assert.Equal(3, report.OpenPlans);
        Assert.Equal(80m + 100m + 60m, report.TotalRemaining);
        Assert.Equal(["2026-06", "2026-07", "2026-08"], report.CommitmentByMonth.Select(c => c.Month));
        Assert.Equal([20m + 30m + 50m + 30m, 30m + 50m + 30m, 0m], report.CommitmentByMonth.Select(c => c.Amount));
        Assert.Equal([4, 3, 0], report.CommitmentByMonth.Select(c => c.InstallmentsCount));
        Assert.Equal(["P", "Q", "S"], report.Plans.Select(x => x.Description));
        Assert.Equal(80m, report.Plans[0].RemainingAmount);
        Assert.Equal(D(5, 10), report.Plans[0].NextDueDate);
        Assert.DoesNotContain(report.Plans, x => x.Id == r.Id);
        Assert.Equal(q.Id, report.Plans[1].Id);
        Assert.Equal(s.Id, report.Plans[2].Id);
    }

    [Fact]
    public async Task Recurrences_ReportsOnlyActiveRules_AndGeneratedUpcomingOccurrences()
    {
        var user = await h.NewUserAsync();
        var expense = await h.CategoryIdAsync(user, "Assinaturas");
        var income = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);
        var internet = await h.CreateRuleAsync(user, "Internet", "Expense", 100m, expense, "Monthly", D(6, 10));
        var salary = await h.CreateRuleAsync(user, "Salário fixo", "Income", 3000m, income, "Monthly", D(6, 20));
        var off = await h.CreateRuleAsync(user, "Desativada", "Expense", 9m, expense, "Monthly", D(7, 1));
        await user.Post($"/api/v1/recurrences/{off.Id}/deactivate");

        var report = await user.GetOk<RecurrencesReportDto>("/api/v1/reports/recurrences");

        Assert.Equal(2, report.ActiveRules);
        Assert.Equal(13, report.UpcomingByMonth.Count); // internet chega a 2027-06-10 (≤ hoje + 12 meses = 2027-06-15)
        Assert.Equal("2026-06", report.UpcomingByMonth[0].Month);
        Assert.Equal((3000m, 0m), (report.UpcomingByMonth[0].Income, report.UpcomingByMonth[0].Expense)); // a de 06-10 é passada
        Assert.Equal(("2027-06", 0m, 100m), (report.UpcomingByMonth[^1].Month, report.UpcomingByMonth[^1].Income, report.UpcomingByMonth[^1].Expense)); // salário de 2027-06-20 está fora
        Assert.All(report.UpcomingByMonth.Skip(1).SkipLast(1), m => Assert.Equal((3000m, 100m), (m.Income, m.Expense)));
        Assert.Equal(1200m, report.Rules.Single(x => x.Id == internet.Id).UpcomingTotal12m);
        Assert.Equal(36000m, report.Rules.Single(x => x.Id == salary.Id).UpcomingTotal12m);
        Assert.DoesNotContain(report.Rules, x => x.Id == off.Id);
    }

    [Fact]
    public async Task Reports_Validation_ReturnsFieldErrors()
    {
        var user = await h.NewUserAsync();

        var missing = await ReadProblemAsync(await user.Get("/api/v1/reports/summary"), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(missing.HasError("from"));
        Assert.True(missing.HasError("to"));
        var inverted = await ReadProblemAsync(await user.Get("/api/v1/reports/by-category?from=2026-06-30&to=2026-06-01"), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(inverted.HasError("to"));
        await ReadProblemAsync(await user.Get("/api/v1/reports/by-account?from=2025-01-01&to=2026-06-30"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await user.Get("/api/v1/reports/monthly-evolution?months=0"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await user.Get("/api/v1/reports/monthly-evolution?endMonth=2026-13"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await user.Get("/api/v1/reports/installments?months=37"), HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Reports_AreIsolatedPerUser_AndRequireAuthentication()
    {
        await SeedJuneAsync();
        var other = await h.NewUserAsync();

        var summary = await other.GetOk<SummaryReportDto>($"/api/v1/reports/summary?{June}");
        var categories = await other.GetOk<CategoryReportDto>($"/api/v1/reports/by-category?{June}");

        Assert.Equal((0m, 0m), (summary.Totals.Income, summary.Totals.Expense));
        Assert.Empty(categories.Items);
        Assert.Equal(0m, categories.Total);
        Assert.Equal(0, (await other.GetOk<InstallmentsReportDto>("/api/v1/reports/installments")).OpenPlans);
        Assert.Equal(0, (await other.GetOk<RecurrencesReportDto>("/api/v1/reports/recurrences")).ActiveRules);
        var anonymous = await h.Client.GetAsync($"/api/v1/reports/summary?{June}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
