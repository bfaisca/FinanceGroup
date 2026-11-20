using System.Net;
using FinanceApp.Application.Accounts;
using FinanceApp.Application.CreditCards;
using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;
using static FinanceApp.IntegrationTests.Infrastructure.Etapa3Support;

namespace FinanceApp.IntegrationTests;

/// <summary>Compras no cartão (contrato §10, D-025) e projeção de saldo. "Hoje" fixado em 2026-06-15.</summary>
[Collection(ApiCollection.Name)]
public sealed class CardPurchasesApiTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 6, 15);

    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public CardPurchasesApiTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    private async Task<(ApiUser User, CreditCardDto Card, Guid Category)> SetupAsync(int closing = 10, int due = 20, Guid? accountId = null)
    {
        var user = await h.NewUserAsync();
        var card = await h.CreateCardAsync(user, "Cartão", closingDay: closing, dueDay: due, accountId: accountId);
        return (user, card, await h.CategoryIdAsync(user, "Compras"));
    }

    [Fact]
    public async Task Purchase_StoresCalculatedDueDate_ForcesCreditCard_AndHasNoAccount()
    {
        var (user, card, category) = await SetupAsync();

        var response = await user.Post("/api/v1/transactions", CardTxBody("Notebook", 1500m, category, card.Id, D(6, 12)));

        var tx = await ApiUser.Read<TransactionDetailDto>(response, HttpStatusCode.Created);
        Assert.Equal(card.Id, tx.CreditCardId);
        Assert.Equal("Cartão", tx.CreditCardName);
        Assert.Null(tx.AccountId);
        Assert.Null(tx.AccountName);
        Assert.Equal(PaymentMethod.CreditCard, tx.PaymentMethod);
        // 12 > fechamento (10): fatura de julho; vencimento dia 20.
        Assert.Equal(D(7, 20), tx.DueDate);
        Assert.Equal(D(6, 12), tx.TransactionDate);
        Assert.Null(tx.Installment);
        Assert.Null(tx.RecurrenceRuleId);
        Assert.Equal(TransactionStatus.Pending, tx.Status);
    }

    [Fact]
    public async Task Purchase_ExplicitCreditCardPaymentMethod_IsAccepted()
    {
        var (user, card, category) = await SetupAsync();
        var response = await user.Post("/api/v1/transactions", new
        {
            description = "Ok", type = "Expense", amount = 10m, categoryId = category, creditCardId = card.Id, paymentMethod = "CreditCard",
            transactionDate = D(6, 5),
        });
        var tx = await ApiUser.Read<TransactionDetailDto>(response, HttpStatusCode.Created);
        Assert.Equal(D(6, 20), tx.DueDate);
    }

    private static object WithCard(Guid card, Guid category, object? extra = null)
    {
        var bag = new Dictionary<string, object?>
        {
            ["description"] = "Compra", ["type"] = "Expense", ["amount"] = 10m, ["categoryId"] = category, ["creditCardId"] = card,
            ["transactionDate"] = D(6, 5),
        };
        if (extra is not null)
        {
            foreach (var p in extra.GetType().GetProperties())
            {
                bag[p.Name] = p.GetValue(extra);
            }
        }

        return bag;
    }

    [Fact]
    public async Task Purchase_Rejects_AccountId_DueDate_AndOtherPaymentMethod_With400()
    {
        var (user, card, category) = await SetupAsync();
        var account = await h.CreateAccountAsync(user, "Conta");

        var withAccount = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", WithCard(card.Id, category, new { accountId = account.Id })),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(withAccount.HasError("accountId"));

        var withDue = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", WithCard(card.Id, category, new { dueDate = D(6, 30) })),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(withDue.HasError("dueDate"));

        var withMethod = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", WithCard(card.Id, category, new { paymentMethod = "Pix" })),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(withMethod.HasError("paymentMethod"));
    }

    [Fact]
    public async Task Purchase_OnIncome_Returns400_OnCreditCardId()
    {
        var (user, card, _) = await SetupAsync();
        var salary = await h.CategoryIdAsync(user, "Salário", FinanceApp.Domain.Categories.CategoryType.Income);

        var problem = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", new
            {
                description = "Receita", type = "Income", amount = 10m, categoryId = salary, creditCardId = card.Id, transactionDate = D(6, 5),
            }),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(problem.HasError("creditCardId"));
    }

    [Fact]
    public async Task Purchase_WithOtherUsersOrMissingCard_Returns400()
    {
        var (user, _, category) = await SetupAsync();
        var other = await h.NewUserAsync();
        var foreign = await h.CreateCardAsync(other, "Alheio");

        var foreignProblem = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", CardTxBody("X", 10m, category, foreign.Id, D(6, 5))),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(foreignProblem.HasError("creditCardId"));
        var missing = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", CardTxBody("X", 10m, category, Guid.NewGuid(), D(6, 5))),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(missing.HasError("creditCardId"));
        // Mesma mensagem nos dois casos: não vaza existência.
        Assert.Equal(
            foreignProblem.GetProperty("errors").GetProperty("creditCardId")[0].GetString(),
            missing.GetProperty("errors").GetProperty("creditCardId")[0].GetString());
    }

    [Fact]
    public async Task Purchase_OnInactiveCard_Returns422()
    {
        var (user, card, category) = await SetupAsync();
        await user.Post($"/api/v1/credit-cards/{card.Id}/deactivate");

        await ReadProblemAsync(
            await user.Post("/api/v1/transactions", CardTxBody("X", 10m, category, card.Id, D(6, 5))),
            HttpStatusCode.UnprocessableEntity,
            "business_rule_violation");

        // Reativado, volta a aceitar compras.
        await user.Post($"/api/v1/credit-cards/{card.Id}/activate");
        await h.CreateCardPurchaseAsync(user, card.Id, "Agora vai", 10m, category, D(6, 5));
    }

    [Fact]
    public async Task Update_CreditCardId_IsImmutable()
    {
        var (user, card, category) = await SetupAsync();
        var other = await h.CreateCardAsync(user, "Outro");
        var tx = await h.CreateCardPurchaseAsync(user, card.Id, "Compra", 100m, category, D(6, 5));
        var plain = await h.CreateTxAsync(user, "Comum", "Expense", 50m, category, D(6, 5));

        object Body(Guid? creditCardId, string description = "Compra") =>
            new { description, amount = 100m, categoryId = category, creditCardId, transactionDate = D(6, 5) };

        await ReadProblemAsync(await user.Put($"/api/v1/transactions/{tx.Id}", Body(other.Id)), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        // Associar cartão a uma movimentação comum também é "outro valor".
        await ReadProblemAsync(
            await user.Put($"/api/v1/transactions/{plain.Id}", new { description = "Comum", amount = 50m, categoryId = category, creditCardId = card.Id, transactionDate = D(6, 5) }),
            HttpStatusCode.UnprocessableEntity,
            "business_rule_violation");

        // Reenviar o mesmo cartão (ou omitir) é aceito.
        var same = await ApiUser.Read<TransactionDetailDto>(await user.Put($"/api/v1/transactions/{tx.Id}", Body(card.Id, "Renomeada")));
        Assert.Equal("Renomeada", same.Description);
        var omitted = await ApiUser.Read<TransactionDetailDto>(await user.Put($"/api/v1/transactions/{tx.Id}", Body(null, "Sem cartão no corpo")));
        Assert.Equal(card.Id, omitted.CreditCardId);
        Assert.Equal(PaymentMethod.CreditCard, omitted.PaymentMethod);
    }

    [Fact]
    public async Task Update_OnCardPurchase_RejectsAccountAndDifferentDueDate_ButToleratesEcho()
    {
        var (user, card, category) = await SetupAsync();
        var account = await h.CreateAccountAsync(user, "Conta");
        var tx = await h.CreateCardPurchaseAsync(user, card.Id, "Compra", 100m, category, D(6, 5));
        Assert.Equal(D(6, 20), tx.DueDate);

        var withAccount = await ReadProblemAsync(
            await user.Put($"/api/v1/transactions/{tx.Id}", new { description = "Compra", amount = 100m, categoryId = category, accountId = account.Id, transactionDate = D(6, 5) }),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(withAccount.HasError("accountId"));

        var otherDue = await ReadProblemAsync(
            await user.Put($"/api/v1/transactions/{tx.Id}", new { description = "Compra", amount = 100m, categoryId = category, transactionDate = D(6, 5), dueDate = D(6, 25) }),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(otherDue.HasError("dueDate"));

        var echo = await ApiUser.Read<TransactionDetailDto>(
            await user.Put($"/api/v1/transactions/{tx.Id}", new { description = "Compra", amount = 120m, categoryId = category, transactionDate = D(6, 5), dueDate = D(6, 20) }));
        Assert.Equal(D(6, 20), echo.DueDate);
        Assert.Equal(120m, echo.Amount);
    }

    [Fact]
    public async Task Update_TransactionDate_RecalculatesInvoiceDueDate()
    {
        var (user, card, category) = await SetupAsync();
        var tx = await h.CreateCardPurchaseAsync(user, card.Id, "Compra", 100m, category, D(6, 5));
        Assert.Equal(D(6, 20), tx.DueDate);

        var moved = await ApiUser.Read<TransactionDetailDto>(
            await user.Put($"/api/v1/transactions/{tx.Id}", new { description = "Compra", amount = 100m, categoryId = category, transactionDate = D(6, 11) }));

        Assert.Equal(D(7, 20), moved.DueDate);
        var history = await user.GetOk<List<TransactionHistoryEntryDto>>($"/api/v1/transactions/{tx.Id}/history");
        Assert.Contains(history, e => e.Field == "dueDate" && e.OldValue == "2026-06-20" && e.NewValue == "2026-07-20");
    }

    [Fact]
    public async Task Filters_ByCreditCard_AndByAccountIncludingCardPurchases()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Corrente");
        var cardWithAccount = await h.CreateCardAsync(user, "ComConta", accountId: account.Id);
        var cardWithout = await h.CreateCardAsync(user, "SemConta");
        var category = await h.CategoryIdAsync(user, "Compras");
        var onCard = await h.CreateCardPurchaseAsync(user, cardWithAccount.Id, "No cartão A", 100m, category, D(6, 5));
        await h.CreateCardPurchaseAsync(user, cardWithout.Id, "No cartão B", 40m, category, D(6, 5));
        var direct = await h.CreateTxAsync(user, "Direto", "Expense", 10m, category, D(6, 6), accountId: account.Id);

        var byCard = await h.ListAsync(user, $"creditCardId={cardWithAccount.Id}");
        Assert.Equal([onCard.Id], byCard.Items.Select(t => t.Id));
        Assert.Equal("ComConta", byCard.Items[0].CreditCardName);

        // accountId inclui compras de cartões associados à conta (conta efetiva).
        var byAccount = await h.ListAsync(user, $"accountId={account.Id}");
        Assert.Equal(new[] { onCard.Id, direct.Id }.Order(), byAccount.Items.Select(t => t.Id).Order());
    }

    [Fact]
    public async Task Filters_DoNotLeakOtherUsersCards()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var card = await h.CreateCardAsync(owner, "Privado");
        var category = await h.CategoryIdAsync(owner, "Compras");
        await h.CreateCardPurchaseAsync(owner, card.Id, "Segredo", 10m, category, D(6, 5));

        Assert.Empty((await h.ListAsync(intruder, $"creditCardId={card.Id}")).Items);
        Assert.Single((await h.ListAsync(owner, $"creditCardId={card.Id}")).Items);
    }

    [Fact]
    public async Task ProjectedBalance_UsesInvoiceDueDate_ForAssociatedAccount_WithoutDoubleCounting()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Corrente", balance: 1000m, referenceDate: D(6, 1));
        var card = await h.CreateCardAsync(user, "Associado", closingDay: 10, dueDay: 20, accountId: account.Id);
        var orphan = await h.CreateCardAsync(user, "Sem conta", closingDay: 10, dueDay: 20);
        var category = await h.CategoryIdAsync(user, "Compras");

        var p1 = await h.CreateCardPurchaseAsync(user, card.Id, "Antes do fechamento", 200m, category, D(6, 5));   // vence 06-20
        await h.CreateCardPurchaseAsync(user, card.Id, "Após o fechamento", 100m, category, D(6, 12));              // vence 07-20
        await h.CreateCardPurchaseAsync(user, orphan.Id, "Cartão sem conta", 500m, category, D(6, 5));              // não impacta
        await h.CreateTxAsync(user, "Despesa da conta", "Expense", 50m, category, D(6, 10), accountId: account.Id);  // 06-10

        async Task<decimal?> ProjectedAsync(string asOf) =>
            (await user.GetOk<AccountDto>($"/api/v1/accounts/{account.Id}?asOf={asOf}")).ProjectedBalance;

        Assert.Equal(1000m, await ProjectedAsync("2026-06-09"));   // nada antes de 06-10
        Assert.Equal(950m, await ProjectedAsync("2026-06-19"));    // só a despesa direta (a compra ainda não venceu)
        Assert.Equal(750m, await ProjectedAsync("2026-06-20"));    // + compra 200 no vencimento da fatura
        Assert.Equal(750m, await ProjectedAsync("2026-07-19"));
        Assert.Equal(650m, await ProjectedAsync("2026-07-20"));    // + compra 100 (fatura de julho)

        // Pagar a compra (inclusive pela conta) não gera nova despesa: sem dupla contagem.
        await h.PayAsync(user, p1.Id, 200m, D(6, 15), account.Id);
        Assert.Equal(650m, await ProjectedAsync("2026-07-20"));

        // Cancelada sai da projeção.
        await user.Post($"/api/v1/transactions/{p1.Id}/cancel");
        Assert.Equal(850m, await ProjectedAsync("2026-07-20"));
    }

    [Fact]
    public async Task ProjectedBalance_ReflectsInDashboardTotal_AndAccountList()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Corrente", balance: 1000m, referenceDate: D(6, 1));
        var card = await h.CreateCardAsync(user, "Associado", accountId: account.Id);
        var category = await h.CategoryIdAsync(user, "Compras");
        await h.CreateCardPurchaseAsync(user, card.Id, "Compra", 300m, category, D(6, 5)); // vence 06-20

        var dashboard = await user.GetOk<DashboardDto>("/api/v1/dashboard?from=2026-06-01&to=2026-06-30");
        Assert.Equal(1000m, dashboard.Balance.ReportedTotal);
        Assert.Equal(700m, dashboard.Balance.ProjectedTotal);

        var accounts = await user.GetOk<List<AccountDto>>("/api/v1/accounts?asOf=2026-06-30");
        Assert.Equal(700m, accounts.Single().ProjectedBalance);
    }

    [Fact]
    public async Task Cancel_Reopen_Pay_WorkOnCardPurchases_AndOverdueUsesInvoiceDueDate()
    {
        var (user, card, category) = await SetupAsync();
        var tx = await h.CreateCardPurchaseAsync(user, card.Id, "Compra", 100m, category, D(5, 5)); // vence 05-20 (< hoje)

        var current = await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{tx.Id}");
        Assert.Equal(D(5, 20), current.DueDate);
        Assert.Equal(TransactionStatus.Overdue, current.Status);

        var paid = await h.PayAsync(user, tx.Id, 100m, D(6, 15));
        Assert.Equal(TransactionStatus.Paid, paid.Status);
        Assert.Null(paid.AccountId);
        var removed = await ApiUser.Read<TransactionDetailDto>(await user.Delete($"/api/v1/transactions/{tx.Id}/payments/{paid.Payments.Single().Id}"));
        Assert.Equal(TransactionStatus.Overdue, removed.Status);

        var canceled = await ApiUser.Read<TransactionDetailDto>(await user.Post($"/api/v1/transactions/{tx.Id}/cancel"));
        Assert.Equal(TransactionStatus.Canceled, canceled.Status);
        var reopened = await ApiUser.Read<TransactionDetailDto>(await user.Post($"/api/v1/transactions/{tx.Id}/reopen"));
        Assert.Equal(card.Id, reopened.CreditCardId);
    }

    [Fact]
    public async Task Delete_PurchaseWithoutPayments_IsAllowed()
    {
        var (user, card, category) = await SetupAsync();
        var tx = await h.CreateCardPurchaseAsync(user, card.Id, "Compra", 100m, category, D(6, 5));

        var response = await user.Delete($"/api/v1/transactions/{tx.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0m, (await user.GetOk<CreditCardDto>($"/api/v1/credit-cards/{card.Id}")).UsedLimit);
    }
}
