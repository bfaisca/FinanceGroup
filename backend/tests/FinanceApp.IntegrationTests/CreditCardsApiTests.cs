using System.Net;
using System.Net.Http.Json;
using FinanceApp.Application.Transactions;
using FinanceApp.Application.CreditCards;
using FinanceApp.Application.Dashboard;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>Cartões (contrato §11, D-025). "Hoje" fixado em 2026-06-15 (TestClock), restaurado ao final.</summary>
[Collection(ApiCollection.Name)]
public sealed class CreditCardsApiTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 6, 15);

    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public CreditCardsApiTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    [Fact]
    public async Task Create_ReturnsContractShape_AndTrimsFields()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Corrente");

        var response = await user.Post("/api/v1/credit-cards", new
        {
            name = "  Nubank  ", institution = " Nu ", creditLimit = 5000m, closingDay = 10, dueDay = 17, accountId = account.Id,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ApiUser.Json);
        Assert.Equal(
            [
                "accountId", "accountName", "availableLimit", "closingDay", "createdAt", "creditLimit", "dueDay", "id", "institution",
                "isActive", "name", "openInvoice", "updatedAt", "usedLimit",
            ],
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(["amount", "closingDate", "dueDate"], json.GetProperty("openInvoice").EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("Nubank", json.GetProperty("name").GetString());
        Assert.Equal("Nu", json.GetProperty("institution").GetString());
        Assert.Equal("Corrente", json.GetProperty("accountName").GetString());
        Assert.True(json.GetProperty("isActive").GetBoolean());
        Assert.Equal(5000m, json.GetProperty("availableLimit").GetDecimal());
        Assert.Equal(0m, json.GetProperty("usedLimit").GetDecimal());
        // Hoje = 2026-06-15; fechamento dia 10 → fatura fecha em 2026-07-10 e vence (dia 17 > 10) em 2026-07-17.
        Assert.Equal("2026-07-10", json.GetProperty("openInvoice").GetProperty("closingDate").GetString());
        Assert.Equal("2026-07-17", json.GetProperty("openInvoice").GetProperty("dueDate").GetString());
        Assert.Equal(0m, json.GetProperty("openInvoice").GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Crud_List_Update_Deactivate_Activate()
    {
        var user = await h.NewUserAsync();
        var a = await h.CreateCardAsync(user, "Beta");
        var b = await h.CreateCardAsync(user, "Alfa", limit: 100m);

        var list = await user.GetOk<List<CreditCardDto>>("/api/v1/credit-cards");
        Assert.Equal(["Alfa", "Beta"], list.Select(c => c.Name));

        var updated = await ApiUser.Read<CreditCardDto>(await user.Put($"/api/v1/credit-cards/{a.Id}", new
        {
            name = "Beta Gold", institution = "Banco", creditLimit = 7000.50m, closingDay = 31, dueDay = 5,
        }));
        Assert.Equal("Beta Gold", updated.Name);
        Assert.Equal(7000.50m, updated.CreditLimit);
        Assert.Equal(31, updated.ClosingDay);
        Assert.Null(updated.AccountId);

        var deactivated = await ApiUser.Read<CreditCardDto>(await user.Post($"/api/v1/credit-cards/{b.Id}/deactivate"));
        Assert.False(deactivated.IsActive);
        Assert.Equal(["Beta Gold"], (await user.GetOk<List<CreditCardDto>>("/api/v1/credit-cards")).Select(c => c.Name));
        Assert.Equal(2, (await user.GetOk<List<CreditCardDto>>("/api/v1/credit-cards?includeInactive=true")).Count);

        var reactivated = await ApiUser.Read<CreditCardDto>(await user.Post($"/api/v1/credit-cards/{b.Id}/activate"));
        Assert.True(reactivated.IsActive);
        Assert.Equal((await user.GetOk<CreditCardDto>($"/api/v1/credit-cards/{b.Id}")).Id, b.Id);
    }

    [Fact]
    public async Task Create_Validation_ReturnsFieldErrors()
    {
        var user = await h.NewUserAsync();

        var response = await user.Post("/api/v1/credit-cards", new { name = " ", creditLimit = -1m, closingDay = 0, dueDay = 32 });
        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(problem.HasError("name"));
        Assert.True(problem.HasError("creditLimit"));
        Assert.True(problem.HasError("closingDay"));
        Assert.True(problem.HasError("dueDay"));

        var missing = await ReadProblemAsync(await user.Post("/api/v1/credit-cards", new { name = "X" }), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(missing.HasError("creditLimit"));
        Assert.True(missing.HasError("closingDay"));
        Assert.True(missing.HasError("dueDay"));

        var scale = await ReadProblemAsync(
            await user.Post("/api/v1/credit-cards", new { name = "Y", creditLimit = 10.123m, closingDay = 1, dueDay = 1 }),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(scale.HasError("creditLimit"));

        var longName = await ReadProblemAsync(
            await user.Post("/api/v1/credit-cards", new { name = new string('a', 101), creditLimit = 1m, closingDay = 1, dueDay = 1 }),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(longName.HasError("name"));
    }

    [Fact]
    public async Task Create_ZeroLimit_AndDays1And31_AreAccepted()
    {
        var user = await h.NewUserAsync();
        var card = await h.CreateCardAsync(user, "Zero", limit: 0m, closingDay: 1, dueDay: 31);
        Assert.Equal(0m, card.CreditLimit);
        Assert.Equal(0m, card.AvailableLimit);
    }

    [Fact]
    public async Task DuplicateName_IsCaseInsensitive_PerUser()
    {
        var user = await h.NewUserAsync();
        var other = await h.NewUserAsync();
        var card = await h.CreateCardAsync(user, "Meu Cartão");

        await ReadProblemAsync(
            await user.Post("/api/v1/credit-cards", new { name = "meu cartão", creditLimit = 1m, closingDay = 1, dueDay = 2 }),
            HttpStatusCode.Conflict,
            "conflict");

        var second = await h.CreateCardAsync(user, "Outro");
        await ReadProblemAsync(
            await user.Put($"/api/v1/credit-cards/{second.Id}", new { name = "MEU CARTÃO", creditLimit = 1m, closingDay = 1, dueDay = 2 }),
            HttpStatusCode.Conflict,
            "conflict");

        // Outro usuário pode usar o mesmo nome; renomear para o próprio nome (alterando só a caixa) é permitido.
        await h.CreateCardAsync(other, "Meu Cartão");
        var self = await ApiUser.Read<CreditCardDto>(
            await user.Put($"/api/v1/credit-cards/{card.Id}", new { name = "MEU CARTÃO", creditLimit = 1m, closingDay = 1, dueDay = 2 }));
        Assert.Equal("MEU CARTÃO", self.Name);
    }

    [Fact]
    public async Task AccountReference_IsValidated()
    {
        var user = await h.NewUserAsync();
        var other = await h.NewUserAsync();
        var foreign = await h.CreateAccountAsync(other, "Alheia");
        var inactive = await h.CreateAccountAsync(user, "Velha");
        await user.Post($"/api/v1/accounts/{inactive.Id}/deactivate");

        var body = (Guid id) => new { name = "C", creditLimit = 10m, closingDay = 1, dueDay = 2, accountId = id };
        var foreignProblem = await ReadProblemAsync(await user.Post("/api/v1/credit-cards", body(foreign.Id)), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(foreignProblem.HasError("accountId"));
        var missing = await ReadProblemAsync(await user.Post("/api/v1/credit-cards", body(Guid.NewGuid())), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(missing.HasError("accountId"));
        await ReadProblemAsync(await user.Post("/api/v1/credit-cards", body(inactive.Id)), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task OtherUsersCard_Returns404_OnEveryRoute()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var card = await h.CreateCardAsync(owner, "Privado");
        var update = new { name = "Hack", creditLimit = 1m, closingDay = 1, dueDay = 2 };

        await ReadProblemAsync(await intruder.Get($"/api/v1/credit-cards/{card.Id}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await intruder.Put($"/api/v1/credit-cards/{card.Id}", update), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await intruder.Post($"/api/v1/credit-cards/{card.Id}/deactivate"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await intruder.Post($"/api/v1/credit-cards/{card.Id}/activate"), HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await intruder.GetOk<List<CreditCardDto>>("/api/v1/credit-cards?includeInactive=true"));
        Assert.Single(await owner.GetOk<List<CreditCardDto>>("/api/v1/credit-cards"));
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await h.Client.GetAsync("/api/v1/credit-cards");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UsedLimit_AvailableLimit_AndOpenInvoice_MatchHandComputedNumbers()
    {
        // Cartão: limite 5000, fecha dia 10, vence dia 20. Hoje = 2026-06-15 (fatura aberta: fecha 07-10, vence 07-20).
        var user = await h.NewUserAsync();
        var card = await h.CreateCardAsync(user, "Principal", limit: 5000m, closingDay: 10, dueDay: 20);
        var shopping = await h.CategoryIdAsync(user, "Compras");

        var p1 = await h.CreateCardPurchaseAsync(user, card.Id, "P1 antes do fechamento", 300m, shopping, D(6, 5));   // vence 06-20
        var p2 = await h.CreateCardPurchaseAsync(user, card.Id, "P2 após o fechamento", 200m, shopping, D(6, 12));   // vence 07-20
        var p3 = await h.CreateCardPurchaseAsync(user, card.Id, "P3 hoje", 100m, shopping, D(6, 15));                // vence 07-20
        var p4 = await h.CreateCardPurchaseAsync(user, card.Id, "P4 maio", 150m, shopping, D(5, 20));                // vence 06-20
        await h.PayAsync(user, p4.Id, 50m, D(6, 15));                                                                // restante 100
        var p5 = await h.CreateCardPurchaseAsync(user, card.Id, "P5 cancelada", 999m, shopping, D(6, 1));
        await user.Post($"/api/v1/transactions/{p5.Id}/cancel");
        // Parcelamento 3x de 90,00 no cartão a partir de 06-01: parcelas de 30,00 vencendo 06-20, 07-20 e 08-20.
        await h.CreatePlanAsync(user, "TV", 90m, 3, D(6, 1), shopping, cardId: card.Id);

        Assert.Equal(D(6, 20), p1.DueDate);
        Assert.Equal(D(7, 20), p2.DueDate);
        Assert.Equal(D(7, 20), p3.DueDate);
        Assert.Equal(D(6, 20), p4.DueDate);

        var dto = await user.GetOk<CreditCardDto>($"/api/v1/credit-cards/{card.Id}");

        // Utilizado: 300 + 200 + 100 + (150−50) + 3×30 = 790 (canceladas ficam de fora; parcelas futuras entram).
        Assert.Equal(790m, dto.UsedLimit);
        Assert.Equal(4210m, dto.AvailableLimit);
        Assert.Equal(D(7, 10), dto.OpenInvoice.ClosingDate);
        Assert.Equal(D(7, 20), dto.OpenInvoice.DueDate);
        // Fatura aberta (vence 07-20): P2 200 + P3 100 + 2ª parcela 30 = 330.
        Assert.Equal(330m, dto.OpenInvoice.Amount);

        var list = await user.GetOk<List<CreditCardDto>>("/api/v1/credit-cards");
        Assert.Equal(790m, list.Single().UsedLimit);
    }

    [Fact]
    public async Task AvailableLimit_CanBeNegative_WhenExceeded()
    {
        var user = await h.NewUserAsync();
        var card = await h.CreateCardAsync(user, "Pequeno", limit: 100m);
        var shopping = await h.CategoryIdAsync(user, "Compras");
        await h.CreateCardPurchaseAsync(user, card.Id, "Grande", 250m, shopping, D(6, 1));

        var dto = await user.GetOk<CreditCardDto>($"/api/v1/credit-cards/{card.Id}");
        Assert.Equal(250m, dto.UsedLimit);
        Assert.Equal(-150m, dto.AvailableLimit);
    }

    [Fact]
    public async Task FullyPaidPurchase_ReleasesLimit_AndLeavesInvoice()
    {
        var user = await h.NewUserAsync();
        var card = await h.CreateCardAsync(user, "Pago", limit: 1000m);
        var shopping = await h.CategoryIdAsync(user, "Compras");
        var p = await h.CreateCardPurchaseAsync(user, card.Id, "Compra", 400m, shopping, D(6, 12));
        Assert.Equal(400m, (await user.GetOk<CreditCardDto>($"/api/v1/credit-cards/{card.Id}")).UsedLimit);

        await h.PayAsync(user, p.Id, 400m, D(6, 15));

        var dto = await user.GetOk<CreditCardDto>($"/api/v1/credit-cards/{card.Id}");
        Assert.Equal(0m, dto.UsedLimit);
        Assert.Equal(1000m, dto.AvailableLimit);
        Assert.Equal(0m, dto.OpenInvoice.Amount);
    }

    [Fact]
    public async Task Dashboard_ListsOnlyActiveCards_ByName_WithSummary()
    {
        var user = await h.NewUserAsync();
        var zeta = await h.CreateCardAsync(user, "Zeta", limit: 1000m, closingDay: 10, dueDay: 20);
        var alfa = await h.CreateCardAsync(user, "Alfa", limit: 2000m, closingDay: 25, dueDay: 5);
        var old = await h.CreateCardAsync(user, "Antigo");
        await user.Post($"/api/v1/credit-cards/{old.Id}/deactivate");
        var shopping = await h.CategoryIdAsync(user, "Compras");
        await h.CreateCardPurchaseAsync(user, zeta.Id, "Z1", 100m, shopping, D(6, 12));   // vence 07-20 = fatura aberta
        await h.CreateCardPurchaseAsync(user, alfa.Id, "A1", 50m, shopping, D(6, 15));    // fecha dia 25 → vence 07-05 (5 ≤ 25 → mês seguinte)

        var dashboard = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-06-01&to=2026-06-30");

        Assert.Equal(["Alfa", "Zeta"], dashboard.CreditCards.Select(c => c.Name));
        var a = dashboard.CreditCards[0];
        Assert.Equal(alfa.Id, a.Id);
        Assert.Equal(2000m, a.CreditLimit);
        Assert.Equal(50m, a.UsedLimit);
        Assert.Equal(1950m, a.AvailableLimit);
        Assert.Equal(D(6, 25), a.OpenInvoice.ClosingDate);
        Assert.Equal(D(7, 5), a.OpenInvoice.DueDate);
        Assert.Equal(50m, a.OpenInvoice.Amount);
        var z = dashboard.CreditCards[1];
        Assert.Equal(100m, z.UsedLimit);
        Assert.Equal(900m, z.AvailableLimit);
        Assert.Equal(D(7, 20), z.OpenInvoice.DueDate);
        Assert.Equal(100m, z.OpenInvoice.Amount);
    }

    [Fact]
    public async Task Dashboard_WithoutCards_ReturnsEmptyList_AndIsolatesUsers()
    {
        var user = await h.NewUserAsync();
        var other = await h.NewUserAsync();
        await h.CreateCardAsync(other, "Alheio");

        var dashboard = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-06-01&to=2026-06-30");
        Assert.Empty(dashboard.CreditCards);
    }

    [Fact]
    public async Task ChangingCardClosing_DoesNotRecalculateExistingPurchases()
    {
        var user = await h.NewUserAsync();
        var card = await h.CreateCardAsync(user, "Mutável", closingDay: 10, dueDay: 20);
        var shopping = await h.CategoryIdAsync(user, "Compras");
        var before = await h.CreateCardPurchaseAsync(user, card.Id, "Antes", 100m, shopping, D(6, 12));
        Assert.Equal(D(7, 20), before.DueDate);

        await user.Put($"/api/v1/credit-cards/{card.Id}", new { name = "Mutável", creditLimit = 5000m, closingDay = 20, dueDay = 28 });

        var unchanged = await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{before.Id}");
        Assert.Equal(D(7, 20), unchanged.DueDate);
        var after = await h.CreateCardPurchaseAsync(user, card.Id, "Depois", 100m, shopping, D(6, 12));
        Assert.Equal(D(6, 28), after.DueDate);
        Assert.Equal(PaymentMethod.CreditCard, after.PaymentMethod);
    }
}
