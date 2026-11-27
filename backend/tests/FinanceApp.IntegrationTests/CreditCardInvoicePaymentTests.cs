using System.Net;
using FinanceApp.Application.CreditCards;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;
using static FinanceApp.IntegrationTests.Infrastructure.Etapa3Support;

namespace FinanceApp.IntegrationTests;

/// <summary>Pagamento de fatura do cartão (D-057): dá baixa em todas as compras do vencimento. "Hoje" fixado em 2026-06-15.</summary>
[Collection(ApiCollection.Name)]
public sealed class CreditCardInvoicePaymentTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 6, 15);

    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public CreditCardInvoicePaymentTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private async Task<(ApiUser User, CreditCardDto Card, Guid Category)> SetupAsync()
    {
        var user = await h.NewUserAsync();
        var card = await h.CreateCardAsync(user, "Cartão", closingDay: 10, dueDay: 20);
        return (user, card, await h.CategoryIdAsync(user, "Compras"));
    }

    [Fact]
    public async Task ListInvoices_GroupsByDueDate_AndHidesSettledOnes()
    {
        var (user, card, category) = await SetupAsync();
        await h.CreateCardPurchaseAsync(user, card.Id, "A", 100m, category, D(6, 2));   // fatura 20/06
        await h.CreateCardPurchaseAsync(user, card.Id, "B", 50.5m, category, D(6, 5));  // fatura 20/06
        await h.CreateCardPurchaseAsync(user, card.Id, "C", 70m, category, D(6, 12));   // fatura 20/07

        var invoices = await ApiUser.Read<List<CardInvoiceDto>>(await user.Get($"/api/v1/credit-cards/{card.Id}/invoices"), HttpStatusCode.OK);

        Assert.Equal(2, invoices.Count);
        Assert.Equal(D(6, 20), invoices[0].DueDate);
        Assert.Equal(150.5m, invoices[0].RemainingAmount);
        Assert.Equal(2, invoices[0].PurchaseCount);
        Assert.False(invoices[0].IsOverdue);
        Assert.Equal(D(7, 20), invoices[1].DueDate);
        Assert.Equal(70m, invoices[1].RemainingAmount);
    }

    [Fact]
    public async Task Pay_SettlesEveryPurchaseOfTheInvoice_AndLeavesOthersUntouched()
    {
        var (user, card, category) = await SetupAsync();
        var a = await h.CreateCardPurchaseAsync(user, card.Id, "A", 100m, category, D(6, 2));
        var b = await h.CreateCardPurchaseAsync(user, card.Id, "B", 50.5m, category, D(6, 5));
        var c = await h.CreateCardPurchaseAsync(user, card.Id, "C", 70m, category, D(6, 12));

        var result = await ApiUser.Read<PayInvoiceResultDto>(
            await user.Post($"/api/v1/credit-cards/{card.Id}/invoices/pay", new { dueDate = D(6, 20) }), HttpStatusCode.OK);

        Assert.Equal(150.5m, result.PaidAmount);
        Assert.Equal(2, result.PurchasesPaid);
        Assert.Equal(FixedToday, result.PaymentDate);

        foreach (var id in new[] { a.Id, b.Id })
        {
            var tx = await ApiUser.Read<TransactionDetailDto>(await user.Get($"/api/v1/transactions/{id}"), HttpStatusCode.OK);
            Assert.Equal(TransactionStatus.Paid, tx.Status);
            Assert.Equal(tx.Amount, tx.PaidAmount);
            Assert.Single(tx.Payments);
        }

        var untouched = await ApiUser.Read<TransactionDetailDto>(await user.Get($"/api/v1/transactions/{c.Id}"), HttpStatusCode.OK);
        Assert.Equal(0m, untouched.PaidAmount);

        // O limite utilizado cai exatamente o valor pago e nenhuma despesa nova foi criada.
        var fresh = await ApiUser.Read<CreditCardDto>(await user.Get($"/api/v1/credit-cards/{card.Id}"), HttpStatusCode.OK);
        Assert.Equal(70m, fresh.UsedLimit);
        var invoices = await ApiUser.Read<List<CardInvoiceDto>>(await user.Get($"/api/v1/credit-cards/{card.Id}/invoices"), HttpStatusCode.OK);
        Assert.Single(invoices);
    }

    [Fact]
    public async Task Pay_CompletesPartiallyPaidPurchases_WithTheirRemainder()
    {
        var (user, card, category) = await SetupAsync();
        var a = await h.CreateCardPurchaseAsync(user, card.Id, "A", 100m, category, D(6, 2));
        await user.Post($"/api/v1/transactions/{a.Id}/payments", new { amount = 40m, paymentDate = D(6, 10) });

        var result = await ApiUser.Read<PayInvoiceResultDto>(
            await user.Post($"/api/v1/credit-cards/{card.Id}/invoices/pay", new { dueDate = D(6, 20), paymentDate = D(6, 14) }), HttpStatusCode.OK);

        Assert.Equal(60m, result.PaidAmount);
        var tx = await ApiUser.Read<TransactionDetailDto>(await user.Get($"/api/v1/transactions/{a.Id}"), HttpStatusCode.OK);
        Assert.Equal(TransactionStatus.Paid, tx.Status);
        Assert.Equal(2, tx.Payments.Count);
    }

    [Fact]
    public async Task Pay_IgnoresCanceledPurchases()
    {
        var (user, card, category) = await SetupAsync();
        await h.CreateCardPurchaseAsync(user, card.Id, "A", 100m, category, D(6, 2));
        var canceled = await h.CreateCardPurchaseAsync(user, card.Id, "B", 30m, category, D(6, 3));
        await user.Post($"/api/v1/transactions/{canceled.Id}/cancel");

        var result = await ApiUser.Read<PayInvoiceResultDto>(
            await user.Post($"/api/v1/credit-cards/{card.Id}/invoices/pay", new { dueDate = D(6, 20) }), HttpStatusCode.OK);

        Assert.Equal(100m, result.PaidAmount);
        Assert.Equal(1, result.PurchasesPaid);
    }

    [Fact]
    public async Task Pay_NothingPending_Returns422()
    {
        var (user, card, _) = await SetupAsync();
        var response = await user.Post($"/api/v1/credit-cards/{card.Id}/invoices/pay", new { dueDate = D(6, 20) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Pay_Validates_DueDateRequired_AndNoFuturePaymentDate()
    {
        var (user, card, category) = await SetupAsync();
        await h.CreateCardPurchaseAsync(user, card.Id, "A", 100m, category, D(6, 2));

        var noDue = await ReadProblemAsync(await user.Post($"/api/v1/credit-cards/{card.Id}/invoices/pay", new { }), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(noDue.HasError("dueDate"));

        var future = await ReadProblemAsync(
            await user.Post($"/api/v1/credit-cards/{card.Id}/invoices/pay", new { dueDate = D(6, 20), paymentDate = D(6, 16) }),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(future.HasError("paymentDate"));
    }

    [Fact]
    public async Task OtherUsers_CannotListOrPayTheCard()
    {
        var (owner, card, category) = await SetupAsync();
        await h.CreateCardPurchaseAsync(owner, card.Id, "A", 100m, category, D(6, 2));
        var stranger = await h.NewUserAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Get($"/api/v1/credit-cards/{card.Id}/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Post($"/api/v1/credit-cards/{card.Id}/invoices/pay", new { dueDate = D(6, 20) })).StatusCode);
    }
}
