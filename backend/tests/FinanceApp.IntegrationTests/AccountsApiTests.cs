using System.Net;
using System.Net.Http.Json;
using FinanceApp.Application.Accounts;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AccountsApiTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        foreach (var url in new[] { "/api/v1/accounts", $"/api/v1/accounts/{Guid.NewGuid()}", $"/api/v1/accounts/{Guid.NewGuid()}/balance-snapshots" })
        {
            await ReadProblemAsync(await h.Client.GetAsync(url), HttpStatusCode.Unauthorized, "unauthorized");
        }

        await ReadProblemAsync(
            await h.Client.PostAsJsonAsync("/api/v1/accounts", new { name = "x", type = "Checking" }), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Create_ReturnsContractShape_AndLocation()
    {
        var user = await h.NewUserAsync();

        var response = await user.Post("/api/v1/accounts", new { name = "  Nubank ", institution = "Nu Pagamentos", type = "Checking", initialBalance = 1250.75m, balanceReferenceDate = h.Today.AddDays(-3) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ApiUser.Json);
        Assert.Equal(
            ["createdAt", "currency", "id", "institution", "isActive", "latestSnapshot", "name", "projectedAsOf", "projectedBalance", "type", "updatedAt"],
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("Nubank", json.GetProperty("name").GetString());
        Assert.Equal("Checking", json.GetProperty("type").GetString());
        Assert.Equal("BRL", json.GetProperty("currency").GetString());
        Assert.True(json.GetProperty("isActive").GetBoolean());
        Assert.Equal(1250.75m, json.GetProperty("latestSnapshot").GetProperty("balance").GetDecimal());
        Assert.Equal(h.Today.AddDays(-3).ToString("yyyy-MM-dd"), json.GetProperty("latestSnapshot").GetProperty("referenceDate").GetString());
        Assert.Equal(1250.75m, json.GetProperty("projectedBalance").GetDecimal());
        Assert.Equal(h.Today.ToString("yyyy-MM-dd"), json.GetProperty("projectedAsOf").GetString());
        Assert.EndsWith("Z", json.GetProperty("createdAt").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WithoutBalance_HasNullSnapshotAndProjection()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Carteira", type: "Wallet");

        Assert.Null(account.LatestSnapshot);
        Assert.Null(account.ProjectedBalance);
        Assert.Empty(await user.GetOk<List<BalanceSnapshotDto>>($"/api/v1/accounts/{account.Id}/balance-snapshots"));
    }

    [Fact]
    public async Task Create_InitialBalanceWithoutDate_DefaultsToToday()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Poupança", balance: 300m);
        Assert.Equal(h.Today, account.LatestSnapshot!.ReferenceDate);
    }

    [Fact]
    public async Task Create_ValidationErrors_ReturnErrorsPerField()
    {
        var user = await h.NewUserAsync();

        var response = await user.Post("/api/v1/accounts", new { name = "", institution = new string('x', 101), type = "Nope", initialBalance = 1.234m, balanceReferenceDate = h.Today.AddDays(1) });

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(problem.GetProperty("errors").TryGetProperty("type", out _) || problem.GetProperty("errors").TryGetProperty("body", out _));

        var response2 = await user.Post("/api/v1/accounts", new { name = "", institution = new string('x', 101), type = "Checking", initialBalance = 1.234m, balanceReferenceDate = h.Today.AddDays(1) });
        var errors = (await ReadProblemAsync(response2, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors");
        Assert.Equal("O nome da conta é obrigatório.", errors.GetProperty("name")[0].GetString());
        Assert.True(errors.TryGetProperty("institution", out _));
        Assert.True(errors.TryGetProperty("initialBalance", out _));
        Assert.True(errors.TryGetProperty("balanceReferenceDate", out _));
    }

    [Fact]
    public async Task Create_MissingType_IsValidationError()
    {
        var user = await h.NewUserAsync();
        var problem = await ReadProblemAsync(await user.Post("/api/v1/accounts", new { name = "Sem tipo" }), HttpStatusCode.BadRequest, "validation_error");
        Assert.Equal("O tipo da conta é obrigatório.", problem.GetProperty("errors").GetProperty("type")[0].GetString());
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409Conflict_IgnoringCase_AndPerUser()
    {
        var user = await h.NewUserAsync();
        await h.CreateAccountAsync(user, "Itaú");

        var duplicate = await user.Post("/api/v1/accounts", new { name = "itaÚ", type = "Savings" });
        await ReadProblemAsync(duplicate, HttpStatusCode.Conflict, "conflict");

        var other = await h.NewUserAsync();
        await h.CreateAccountAsync(other, "Itaú");
    }

    [Fact]
    public async Task List_IsSortedByName_FiltersInactive_AndSupportsAsOf()
    {
        var user = await h.NewUserAsync();
        var b = await h.CreateAccountAsync(user, "Banco B", balance: 10m, referenceDate: h.Today.AddDays(-10));
        await h.CreateAccountAsync(user, "Banco A");
        var inactive = await h.CreateAccountAsync(user, "Banco C");
        Assert.Equal(HttpStatusCode.OK, (await user.Post($"/api/v1/accounts/{inactive.Id}/deactivate")).StatusCode);

        var active = await user.GetOk<List<AccountDto>>("/api/v1/accounts");
        Assert.Equal(["Banco A", "Banco B"], active.Select(a => a.Name));

        var all = await user.GetOk<List<AccountDto>>("/api/v1/accounts?includeInactive=true");
        Assert.Equal(["Banco A", "Banco B", "Banco C"], all.Select(a => a.Name));
        Assert.False(all.Single(a => a.Name == "Banco C").IsActive);

        var asOf = h.Today.AddDays(-20);
        var past = await user.GetOk<List<AccountDto>>($"/api/v1/accounts?asOf={asOf:yyyy-MM-dd}");
        Assert.All(past, a => Assert.Equal(asOf, a.ProjectedAsOf));
        Assert.Equal(10m, past.Single(a => a.Id == b.Id).ProjectedBalance);
    }

    [Fact]
    public async Task Get_Update_Deactivate_Activate()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta", balance: 100m);

        var got = await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}");
        Assert.Equal(account.Id, got.Id);

        var updated = await ApiUser.Read<AccountDto>(await user.Put($"/api/v1/accounts/{account.Id}", new { name = "Renomeada", institution = "Banco", type = "Savings" }));
        Assert.Equal("Renomeada", updated.Name);
        Assert.Equal("Savings", updated.Type.ToString());
        Assert.Equal(100m, updated.LatestSnapshot!.Balance);
        Assert.True(updated.UpdatedAt >= account.UpdatedAt);

        var off = await ApiUser.Read<AccountDto>(await user.Post($"/api/v1/accounts/{account.Id}/deactivate"));
        Assert.False(off.IsActive);
        var on = await ApiUser.Read<AccountDto>(await user.Post($"/api/v1/accounts/{account.Id}/activate"));
        Assert.True(on.IsActive);
    }

    [Fact]
    public async Task Update_ToExistingName_Returns409()
    {
        var user = await h.NewUserAsync();
        await h.CreateAccountAsync(user, "Alfa");
        var beta = await h.CreateAccountAsync(user, "Beta");
        await ReadProblemAsync(
            await user.Put($"/api/v1/accounts/{beta.Id}", new { name = "ALFA", type = "Checking" }), HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task Snapshots_NewOneBecomesReference_HistoryKept_MostRecentFirst()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta", balance: 1000m, referenceDate: h.Today.AddDays(-20));

        var created = await user.Post($"/api/v1/accounts/{account.Id}/balance-snapshots", new { balance = 2500.40m, referenceDate = h.Today.AddDays(-5) });
        var snapshot = await ApiUser.Read<BalanceSnapshotDto>(created, HttpStatusCode.Created);
        Assert.Equal(2500.40m, snapshot.Balance);

        // Mesmo dia de referência: o criado por último prevalece.
        await ApiUser.Read<BalanceSnapshotDto>(
            await user.Post($"/api/v1/accounts/{account.Id}/balance-snapshots", new { balance = 2600m, referenceDate = h.Today.AddDays(-5) }), HttpStatusCode.Created);

        var list = await user.GetOk<List<BalanceSnapshotDto>>($"/api/v1/accounts/{account.Id}/balance-snapshots");
        Assert.Equal([2600m, 2500.40m, 1000m], list.Select(s => s.Balance));

        var current = await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}");
        Assert.Equal(2600m, current.LatestSnapshot!.Balance);
        Assert.Equal(2600m, current.ProjectedBalance);
    }

    [Fact]
    public async Task Snapshots_Validation_AndInactiveAccount()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta");

        var future = await user.Post($"/api/v1/accounts/{account.Id}/balance-snapshots", new { balance = 1m, referenceDate = h.Today.AddDays(1) });
        var errors = (await ReadProblemAsync(future, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("referenceDate", out _));

        var negative = await ApiUser.Read<BalanceSnapshotDto>(
            await user.Post($"/api/v1/accounts/{account.Id}/balance-snapshots", new { balance = -75.5m, referenceDate = h.Today }), HttpStatusCode.Created);
        Assert.Equal(-75.5m, negative.Balance);

        await user.Post($"/api/v1/accounts/{account.Id}/deactivate");
        var inactive = await user.Post($"/api/v1/accounts/{account.Id}/balance-snapshots", new { balance = 1m, referenceDate = h.Today });
        await ReadProblemAsync(inactive, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task ProjectedBalance_FollowsEffectiveDate_AsOf_AndIgnoresCanceled()
    {
        var user = await h.NewUserAsync();
        var today = h.Today;
        var reference = today.AddDays(-10);
        var account = await h.CreateAccountAsync(user, "Conta", balance: 1000m, referenceDate: reference);
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var salary = await h.CategoryIdAsync(user, "Salário", FinanceApp.Domain.Categories.CategoryType.Income);

        // Receita 500 com vencimento em +3 dias (data efetiva = vencimento).
        await h.CreateTxAsync(user, "Salário", "Income", 500m, salary, today.AddDays(-9), due: today.AddDays(3), accountId: account.Id);
        // Despesa 120 sem vencimento (data efetiva = data da movimentação).
        await h.CreateTxAsync(user, "Mercado", "Expense", 120m, food, today.AddDays(-2), accountId: account.Id);
        // Despesa antes da referência: ignorada.
        await h.CreateTxAsync(user, "Antiga", "Expense", 999m, food, reference.AddDays(-5), accountId: account.Id);
        // Despesa NA data de referência: ignorada (já refletida no saldo informado).
        await h.CreateTxAsync(user, "No dia", "Expense", 77m, food, reference, accountId: account.Id);
        // Cancelada: ignorada.
        var canceled = await h.CreateTxAsync(user, "Cancelada", "Expense", 300m, food, today.AddDays(-1), accountId: account.Id);
        await user.Post($"/api/v1/transactions/{canceled.Id}/cancel");
        // Paga parcialmente: conta pelo valor total.
        var partial = await h.CreateTxAsync(user, "Parcial", "Expense", 80m, food, today.AddDays(-1), accountId: account.Id);
        await h.PayAsync(user, partial.Id, 10m, today);
        // Sem conta: não afeta.
        await h.CreateTxAsync(user, "Sem conta", "Expense", 5000m, food, today.AddDays(-1));

        // Hoje: 1000 - 120 - 80 = 800 (a receita vence só em +3).
        var now = await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}");
        Assert.Equal(800m, now.ProjectedBalance);
        Assert.Equal(today, now.ProjectedAsOf);

        var future = await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}?asOf={today.AddDays(3):yyyy-MM-dd}");
        Assert.Equal(1300m, future.ProjectedBalance);
        Assert.Equal(today.AddDays(3), future.ProjectedAsOf);

        var atReference = await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}?asOf={reference:yyyy-MM-dd}");
        Assert.Equal(1000m, atReference.ProjectedBalance);
        var beforeReference = await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}?asOf={reference.AddDays(-30):yyyy-MM-dd}");
        Assert.Equal(1000m, beforeReference.ProjectedBalance);

        // Novo snapshot muda a referência: a partir de hoje, saldo 2000 e só o que vem depois conta.
        await user.Post($"/api/v1/accounts/{account.Id}/balance-snapshots", new { balance = 2000m, referenceDate = today });
        var rebased = await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}?asOf={today.AddDays(3):yyyy-MM-dd}");
        Assert.Equal(2500m, rebased.ProjectedBalance);
    }

    [Fact]
    public async Task Account_WithoutSnapshot_IgnoresMovementsForProjection()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta");
        var food = await h.CategoryIdAsync(user, "Alimentação");
        await h.CreateTxAsync(user, "Mercado", "Expense", 10m, food, h.Today, accountId: account.Id);

        Assert.Null((await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}")).ProjectedBalance);
    }

    [Fact]
    public async Task InactiveAccount_RejectsNewTransactionsAndPayments_ButKeepsHistory()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta", balance: 100m);
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var existing = await h.CreateTxAsync(user, "Existente", "Expense", 50m, food, h.Today, accountId: account.Id);
        await user.Post($"/api/v1/accounts/{account.Id}/deactivate");

        var create = await user.Post("/api/v1/transactions", FinancialHarness.TxBody("Nova", "Expense", 10m, food, h.Today, accountId: account.Id));
        await ReadProblemAsync(create, HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        var pay = await user.Post($"/api/v1/transactions/{existing.Id}/payments", new { amount = 10m, paymentDate = h.Today, accountId = account.Id });
        await ReadProblemAsync(pay, HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        var history = await h.ListAsync(user, $"accountId={account.Id}");
        Assert.Single(history.Items);
    }

    [Fact]
    public async Task OtherUsersAccounts_AreNotFound_AndNeverListed()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(owner, "Privada", balance: 10m);

        foreach (var (method, url, body) in new (HttpMethod, string, object?)[]
                 {
                     (HttpMethod.Get, $"/api/v1/accounts/{account.Id}", null),
                     (HttpMethod.Put, $"/api/v1/accounts/{account.Id}", new { name = "x", type = "Other" }),
                     (HttpMethod.Post, $"/api/v1/accounts/{account.Id}/deactivate", null),
                     (HttpMethod.Post, $"/api/v1/accounts/{account.Id}/activate", null),
                     (HttpMethod.Get, $"/api/v1/accounts/{account.Id}/balance-snapshots", null),
                     (HttpMethod.Post, $"/api/v1/accounts/{account.Id}/balance-snapshots", new { balance = 1m, referenceDate = h.Today }),
                 })
        {
            await ReadProblemAsync(await intruder.SendAsync(method, url, body), HttpStatusCode.NotFound, "not_found");
        }

        Assert.Empty(await intruder.GetOk<List<AccountDto>>("/api/v1/accounts?includeInactive=true"));
        Assert.Single(await owner.GetOk<List<AccountDto>>("/api/v1/accounts"));
        // Nada foi alterado.
        var untouched = await owner.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}");
        Assert.Equal("Privada", untouched.Name);
        Assert.True(untouched.IsActive);
    }

    [Fact]
    public async Task NonExistentAccount_Returns404()
    {
        var user = await h.NewUserAsync();
        await ReadProblemAsync(await user.Get($"/api/v1/accounts/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task InvalidAsOf_IsValidationError()
    {
        var user = await h.NewUserAsync();
        await ReadProblemAsync(await user.Get("/api/v1/accounts?asOf=ontem"), HttpStatusCode.BadRequest, "validation_error");
    }
}
