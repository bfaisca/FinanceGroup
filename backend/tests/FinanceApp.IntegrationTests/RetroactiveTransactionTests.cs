using System.Net;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>Lançamento retroativo já quitado (D-059). "Hoje" fixado em 2026-06-15.</summary>
[Collection(ApiCollection.Name)]
public sealed class RetroactiveTransactionTests : IDisposable
{
    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public RetroactiveTransactionTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = new DateOnly(2026, 6, 15);
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static object Body(Guid category, string type, DateOnly date, DateOnly? paymentDate, string? status = null) =>
        new { description = "Salário", type, amount = 5000m, categoryId = category, transactionDate = date, paymentDate, status };

    [Fact]
    public async Task Income_WithPaymentDate_IsCreatedAsReceived_WithOnePayment()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Salário", FinanceApp.Domain.Categories.CategoryType.Income);

        var response = await user.Post("/api/v1/transactions", Body(category, "Income", new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 7)));

        var tx = await ApiUser.Read<TransactionDetailDto>(response, HttpStatusCode.Created);
        Assert.Equal(TransactionStatus.Received, tx.Status);
        Assert.Equal(5000m, tx.PaidAmount);
        Assert.Equal(0m, tx.RemainingAmount);
        var payment = Assert.Single(tx.Payments);
        Assert.Equal(new DateOnly(2026, 5, 7), payment.PaymentDate);
    }

    [Fact]
    public async Task Expense_WithPaymentDate_IsCreatedAsPaid()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");

        var tx = await ApiUser.Read<TransactionDetailDto>(
            await user.Post("/api/v1/transactions", Body(category, "Expense", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1))),
            HttpStatusCode.Created);

        Assert.Equal(TransactionStatus.Paid, tx.Status);
    }

    [Fact]
    public async Task Without_PaymentDate_StaysPending()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Salário", FinanceApp.Domain.Categories.CategoryType.Income);
        var tx = await ApiUser.Read<TransactionDetailDto>(
            await user.Post("/api/v1/transactions", Body(category, "Income", new DateOnly(2026, 6, 1), null)), HttpStatusCode.Created);
        Assert.Equal(TransactionStatus.Pending, tx.Status);
        Assert.Empty(tx.Payments);
    }

    [Fact]
    public async Task PaymentDate_Validations_Return400()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Salário", FinanceApp.Domain.Categories.CategoryType.Income);

        var future = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", Body(category, "Income", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 16))),
            HttpStatusCode.BadRequest, "validation_error");
        Assert.True(future.HasError("paymentDate"));

        var beforeTx = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", Body(category, "Income", new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 9))),
            HttpStatusCode.BadRequest, "validation_error");
        Assert.True(beforeTx.HasError("paymentDate"));

        var planned = await ReadProblemAsync(
            await user.Post("/api/v1/transactions", Body(category, "Income", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1), "Planned")),
            HttpStatusCode.BadRequest, "validation_error");
        Assert.True(planned.HasError("paymentDate"));
    }
}
