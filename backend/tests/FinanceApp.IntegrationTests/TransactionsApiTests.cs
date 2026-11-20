using System.Net;
using System.Net.Http.Json;
using FinanceApp.Application.Accounts;
using FinanceApp.Application.Common;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TransactionsApiTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    private DateOnly Today => h.Today;

    // ------------------------------------------------------------ criação

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        await ReadProblemAsync(await h.Client.GetAsync("/api/v1/transactions"), HttpStatusCode.Unauthorized, "unauthorized");
        await ReadProblemAsync(await h.Client.GetAsync($"/api/v1/transactions/{Guid.NewGuid()}"), HttpStatusCode.Unauthorized, "unauthorized");
        await ReadProblemAsync(await h.Client.PostAsJsonAsync("/api/v1/transactions", new { }), HttpStatusCode.Unauthorized, "unauthorized");
        await ReadProblemAsync(await h.Client.GetAsync("/api/v1/dashboard?from=2026-01-01&to=2026-01-31"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Create_Expense_ReturnsDetailWithContractShape()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta");
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var sub = await h.CreateCategoryAsync(user, "Restaurantes", parent: food, color: "#123456", icon: "pi-star");

        var response = await user.Post("/api/v1/transactions", FinancialHarness.TxBody(
            "  Jantar ", "Expense", 89.90m, sub.Id, Today, Today.AddDays(5), account.Id, paymentMethod: "Pix", notes: " obs "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ApiUser.Json);
        Assert.Equal(
            [
                "accountId", "accountName", "amount", "categoryColor", "categoryIcon", "categoryId", "categoryName", "createdAt", "creditCardId",
                "creditCardName", "description", "dueDate", "id", "installment", "notes", "paidAmount", "paidAt", "parentCategoryName",
                "paymentMethod", "payments", "recurrenceRuleId", "remainingAmount", "status", "transactionDate", "type", "updatedAt",
            ],
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("Jantar", json.GetProperty("description").GetString());
        Assert.Equal("Expense", json.GetProperty("type").GetString());
        Assert.Equal("Pending", json.GetProperty("status").GetString());
        Assert.Equal(89.90m, json.GetProperty("amount").GetDecimal());
        Assert.Equal(0m, json.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(89.90m, json.GetProperty("remainingAmount").GetDecimal());
        Assert.Equal("Restaurantes", json.GetProperty("categoryName").GetString());
        Assert.Equal("Alimentação", json.GetProperty("parentCategoryName").GetString());
        Assert.Equal("#123456", json.GetProperty("categoryColor").GetString());
        Assert.Equal("pi-star", json.GetProperty("categoryIcon").GetString());
        Assert.Equal("Conta", json.GetProperty("accountName").GetString());
        Assert.Equal("Pix", json.GetProperty("paymentMethod").GetString());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), json.GetProperty("transactionDate").GetString());
        Assert.Equal(Today.AddDays(5).ToString("yyyy-MM-dd"), json.GetProperty("dueDate").GetString());
        Assert.Equal("obs", json.GetProperty("notes").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("paidAt").ValueKind);
        Assert.Empty(json.GetProperty("payments").EnumerateArray());
        Assert.EndsWith("Z", json.GetProperty("createdAt").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_Income_Planned_AndDefaultStatusIsPending()
    {
        var user = await h.NewUserAsync();
        var salary = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);

        var planned = await h.CreateTxAsync(user, "Salário futuro", "Income", 5000m, salary, Today, status: "Planned");
        var pending = await h.CreateTxAsync(user, "Salário", "Income", 5000m, salary, Today);

        Assert.Equal(TransactionStatus.Planned, planned.Status);
        Assert.Equal(TransactionStatus.Pending, pending.Status);
        Assert.Equal(TransactionType.Income, pending.Type);
    }

    [Fact]
    public async Task Create_RejectsStatusPaidAndOthers()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        foreach (var status in new[] { "Paid", "Received", "Overdue", "Canceled" })
        {
            var response = await user.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 1m, food, Today, status: status));
            var errors = (await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors");
            Assert.True(errors.TryGetProperty("status", out _), status);
        }
    }

    [Fact]
    public async Task Create_ValidationErrors_ListEveryField()
    {
        var user = await h.NewUserAsync();

        var response = await user.Post("/api/v1/transactions", new
        {
            description = "",
            type = "Expense",
            amount = 0m,
            categoryId = Guid.Empty,
            transactionDate = (DateOnly?)null,
            notes = new string('n', 1001),
        });

        var errors = (await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors");
        foreach (var field in new[] { "description", "amount", "categoryId", "transactionDate", "notes" })
        {
            Assert.True(errors.TryGetProperty(field, out var messages), field);
            Assert.False(string.IsNullOrWhiteSpace(messages[0].GetString()));
        }
    }

    [Fact]
    public async Task Create_AmountPrecisionAndLimits()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");

        var decimals = await user.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 10.123m, food, Today));
        Assert.True((await ReadProblemAsync(decimals, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors").TryGetProperty("amount", out _));

        var huge = await user.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 10_000_000_000_000m, food, Today));
        Assert.True((await ReadProblemAsync(huge, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors").TryGetProperty("amount", out _));

        var max = await h.CreateTxAsync(user, "Máximo", "Expense", 9_999_999_999_999.99m, food, Today);
        Assert.Equal(9_999_999_999_999.99m, max.Amount);
    }

    [Fact]
    public async Task Create_DueDateBeforeTransactionDate_IsValidationError()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var response = await user.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 1m, food, Today, Today.AddDays(-1)));
        Assert.True((await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors").TryGetProperty("dueDate", out _));
    }

    [Fact]
    public async Task Create_CategoryOfOtherType_IsValidationError()
    {
        var user = await h.NewUserAsync();
        var salary = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);
        var response = await user.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 1m, salary, Today));
        var errors = (await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors");
        Assert.Equal("A categoria deve ser do mesmo tipo da movimentação.", errors.GetProperty("categoryId")[0].GetString());
    }

    [Fact]
    public async Task Create_UnknownOrForeignAccount_IsValidationError()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(owner, "Privada");
        var food = await h.CategoryIdAsync(intruder, "Alimentação");

        var foreign = await ReadProblemAsync(
            await intruder.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 1m, food, Today, accountId: account.Id)),
            HttpStatusCode.BadRequest, "validation_error");
        var unknown = await ReadProblemAsync(
            await intruder.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 1m, food, Today, accountId: Guid.NewGuid())),
            HttpStatusCode.BadRequest, "validation_error");
        Assert.Equal(
            foreign.GetProperty("errors").GetProperty("accountId")[0].GetString(),
            unknown.GetProperty("errors").GetProperty("accountId")[0].GetString());
    }

    [Fact]
    public async Task Create_MalformedBody_Returns400()
    {
        var user = await h.NewUserAsync();
        var response = await user.Post("/api/v1/transactions", new { description = "x", type = "Banana", amount = 1m, categoryId = Guid.NewGuid(), transactionDate = Today });
        await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    // ------------------------------------------------------------ leitura e edição

    [Fact]
    public async Task Get_ReturnsDetail_And404ForUnknown()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 10m, food, Today);

        var got = await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{created.Id}");
        Assert.Equal("Mercado", got.Description);
        await ReadProblemAsync(await user.Get($"/api/v1/transactions/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Update_ChangesFields_TypeIsImmutable_AndWritesHistoryPerField()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta");
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var health = await h.CategoryIdAsync(user, "Saúde");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today, Today.AddDays(5));

        var response = await user.Put($"/api/v1/transactions/{created.Id}", new
        {
            type = "Income", // ignorado: o tipo é imutável
            description = "Farmácia",
            amount = 120.50m,
            categoryId = health,
            accountId = account.Id,
            paymentMethod = "DebitCard",
            transactionDate = Today.AddDays(-1),
            dueDate = Today.AddDays(9),
            notes = "receita",
        });

        var updated = await ApiUser.Read<TransactionDetailDto>(response);
        Assert.Equal(TransactionType.Expense, updated.Type);
        Assert.Equal("Farmácia", updated.Description);
        Assert.Equal(120.50m, updated.Amount);
        Assert.Equal(120.50m, updated.RemainingAmount);
        Assert.Equal(health, updated.CategoryId);
        Assert.Equal("Conta", updated.AccountName);
        Assert.Equal(PaymentMethod.DebitCard, updated.PaymentMethod);
        Assert.Equal(Today.AddDays(9), updated.DueDate);
        Assert.Equal("receita", updated.Notes);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);

        var history = await user.GetOk<List<TransactionHistoryEntryDto>>($"/api/v1/transactions/{created.Id}/history");
        Assert.Equal(TransactionAuditAction.Created, history[^1].Action);
        var updates = history.Where(e => e.Action == TransactionAuditAction.Updated).ToList();
        Assert.Equal(
            ["account", "amount", "category", "description", "dueDate", "notes", "paymentMethod", "transactionDate"],
            updates.Select(e => e.Field!).Order(StringComparer.Ordinal).ToList());
        var amount = updates.Single(e => e.Field == "amount");
        Assert.Equal("100.00", amount.OldValue);
        Assert.Equal("120.50", amount.NewValue);
        var category = updates.Single(e => e.Field == "category");
        Assert.Equal("Alimentação", category.OldValue);
        Assert.Equal("Saúde", category.NewValue);
    }

    [Fact]
    public async Task Update_CategoryOfOtherType_IsRejected_BecauseTypeIsImmutable()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var salary = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);

        var response = await user.Put($"/api/v1/transactions/{created.Id}", new { description = "x", amount = 100m, categoryId = salary, transactionDate = Today });

        await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Update_AmountBelowPaid_Returns422_AndKeepsData()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        await h.PayAsync(user, created.Id, 60m, Today);

        var response = await user.Put($"/api/v1/transactions/{created.Id}", new { description = "Mercado", amount = 59.99m, categoryId = food, transactionDate = Today });

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Contains("menor que o total já pago", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(100m, (await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{created.Id}")).Amount);
    }

    [Fact]
    public async Task Update_AmountToExactlyPaid_SettlesTheTransaction()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        await h.PayAsync(user, created.Id, 60m, Today);

        var updated = await ApiUser.Read<TransactionDetailDto>(
            await user.Put($"/api/v1/transactions/{created.Id}", new { description = "Mercado", amount = 60m, categoryId = food, transactionDate = Today }));

        Assert.Equal(TransactionStatus.Paid, updated.Status);
        Assert.Equal(Today, updated.PaidAt);
        Assert.Equal(0m, updated.RemainingAmount);
    }

    [Fact]
    public async Task Update_PlannedAndPendingSwitch_OnlyWithoutPayments()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);

        var planned = await ApiUser.Read<TransactionDetailDto>(
            await user.Put($"/api/v1/transactions/{created.Id}", new { description = "Mercado", amount = 100m, categoryId = food, transactionDate = Today, status = "Planned" }));
        Assert.Equal(TransactionStatus.Planned, planned.Status);
        var pending = await ApiUser.Read<TransactionDetailDto>(
            await user.Put($"/api/v1/transactions/{created.Id}", new { description = "Mercado", amount = 100m, categoryId = food, transactionDate = Today, status = "Pending" }));
        Assert.Equal(TransactionStatus.Pending, pending.Status);

        await h.PayAsync(user, created.Id, 10m, Today);
        await ReadProblemAsync(
            await user.Put($"/api/v1/transactions/{created.Id}", new { description = "Mercado", amount = 100m, categoryId = food, transactionDate = Today, status = "Planned" }),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await ReadProblemAsync(
            await user.Put($"/api/v1/transactions/{created.Id}", new { description = "Mercado", amount = 100m, categoryId = food, transactionDate = Today, status = "Paid" }),
            HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Update_CanceledTransaction_Returns422()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        await user.Post($"/api/v1/transactions/{created.Id}/cancel");

        await ReadProblemAsync(
            await user.Put($"/api/v1/transactions/{created.Id}", new { description = "x", amount = 1m, categoryId = food, transactionDate = Today }),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    // ------------------------------------------------------------ exclusão

    [Fact]
    public async Task Delete_WithoutPayments_Returns204_ThenNotFound()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Delete($"/api/v1/transactions/{created.Id}")).StatusCode);

        await ReadProblemAsync(await user.Get($"/api/v1/transactions/{created.Id}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await user.Delete($"/api/v1/transactions/{created.Id}"), HttpStatusCode.NotFound, "not_found");
        // Histórico some junto (cascata).
        Assert.Equal(0, await h.WithDbAsync(db => db.TransactionAuditEntries.CountAsync(e => e.TransactionId == created.Id)));
    }

    [Fact]
    public async Task Delete_WithPayments_Returns422_AndItCanBeCanceledInstead()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        var paid = await h.PayAsync(user, created.Id, 10m, Today);

        await ReadProblemAsync(await user.Delete($"/api/v1/transactions/{created.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Equal(HttpStatusCode.OK, (await user.Get($"/api/v1/transactions/{created.Id}")).StatusCode);

        // Cancelada também não pode ser excluída enquanto houver pagamentos.
        await user.Post($"/api/v1/transactions/{created.Id}/cancel");
        await ReadProblemAsync(await user.Delete($"/api/v1/transactions/{created.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        // Reabrir, remover o pagamento e então excluir.
        await user.Post($"/api/v1/transactions/{created.Id}/reopen");
        await user.Delete($"/api/v1/transactions/{created.Id}/payments/{paid.Payments.Single().Id}");
        Assert.Equal(HttpStatusCode.NoContent, (await user.Delete($"/api/v1/transactions/{created.Id}")).StatusCode);
    }

    // ------------------------------------------------------------ pagamentos e máquina de estados

    [Fact]
    public async Task Payments_PartialThenFull_ThenRemove_FollowsTheStateMachine()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta");
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today.AddDays(-3), Today.AddDays(10), status: "Planned");
        Assert.Equal(TransactionStatus.Planned, created.Status);

        var partial = await h.PayAsync(user, created.Id, 30m, Today.AddDays(-1), account.Id);
        Assert.Equal(TransactionStatus.Pending, partial.Status);
        Assert.Equal(30m, partial.PaidAmount);
        Assert.Equal(70m, partial.RemainingAmount);
        Assert.Null(partial.PaidAt);
        var firstPayment = Assert.Single(partial.Payments);
        Assert.Equal("Conta", firstPayment.AccountName);
        Assert.Equal(30m, firstPayment.Amount);

        var full = await h.PayAsync(user, created.Id, 70m, Today);
        Assert.Equal(TransactionStatus.Paid, full.Status);
        Assert.Equal(100m, full.PaidAmount);
        Assert.Equal(0m, full.RemainingAmount);
        Assert.Equal(Today, full.PaidAt);
        Assert.Equal(2, full.Payments.Count);
        Assert.Equal([30m, 70m], full.Payments.Select(p => p.Amount));

        var afterRemove = await ApiUser.Read<TransactionDetailDto>(await user.Delete($"/api/v1/transactions/{created.Id}/payments/{full.Payments[1].Id}"));
        Assert.Equal(TransactionStatus.Pending, afterRemove.Status);
        Assert.Equal(30m, afterRemove.PaidAmount);
        Assert.Null(afterRemove.PaidAt);

        var afterRemoveAll = await ApiUser.Read<TransactionDetailDto>(await user.Delete($"/api/v1/transactions/{created.Id}/payments/{firstPayment.Id}"));
        Assert.Equal(TransactionStatus.Pending, afterRemoveAll.Status);
        Assert.Equal(0m, afterRemoveAll.PaidAmount);
        Assert.Empty(afterRemoveAll.Payments);

        // Persistência: o estado salvo bate com o apresentado e os pagamentos foram apagados.
        Assert.Equal(0, await h.WithDbAsync(db => db.TransactionPayments.CountAsync(p => p.TransactionId == created.Id)));
    }

    [Fact]
    public async Task Payments_Income_BecomesReceived_AndPaidByIsTheUserWhoCompleted()
    {
        var user = await h.NewUserAsync();
        var salary = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);
        var created = await h.CreateTxAsync(user, "Salário", "Income", 3000m, salary, Today);

        var received = await h.PayAsync(user, created.Id, 3000m, Today);

        Assert.Equal(TransactionStatus.Received, received.Status);
        var row = await h.WithDbAsync(db => db.Transactions.AsNoTracking().SingleAsync(t => t.Id == created.Id));
        Assert.Equal(user.Id, row.CreatedByUserId);
        Assert.Equal(user.Id, row.PaidByUserId);
        Assert.Equal(TransactionStatus.Received, row.Status);
        Assert.Equal(3000m, row.PaidAmount);
    }

    [Fact]
    public async Task Payments_Validation_Excess_Future_Zero_Canceled()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        await h.PayAsync(user, created.Id, 60m, Today);

        var excess = await ReadProblemAsync(
            await user.Post($"/api/v1/transactions/{created.Id}/payments", new { amount = 40.01m, paymentDate = Today }),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Contains("excede", excess.GetProperty("detail").GetString(), StringComparison.Ordinal);

        var future = await ReadProblemAsync(
            await user.Post($"/api/v1/transactions/{created.Id}/payments", new { amount = 10m, paymentDate = Today.AddDays(1) }),
            HttpStatusCode.BadRequest, "validation_error");
        Assert.True(future.GetProperty("errors").TryGetProperty("paymentDate", out _));

        var zero = await ReadProblemAsync(
            await user.Post($"/api/v1/transactions/{created.Id}/payments", new { amount = 0m, paymentDate = Today }),
            HttpStatusCode.BadRequest, "validation_error");
        Assert.True(zero.GetProperty("errors").TryGetProperty("amount", out _));

        var noDate = await ReadProblemAsync(
            await user.Post($"/api/v1/transactions/{created.Id}/payments", new { amount = 10m }), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(noDate.GetProperty("errors").TryGetProperty("paymentDate", out _));

        Assert.Equal(60m, (await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{created.Id}")).PaidAmount);

        await user.Post($"/api/v1/transactions/{created.Id}/cancel");
        await ReadProblemAsync(
            await user.Post($"/api/v1/transactions/{created.Id}/payments", new { amount = 10m, paymentDate = Today }),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task Payments_ExactRemaining_IsAllowed_AndPayingASettledOneIsNot()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        await h.PayAsync(user, created.Id, 99.99m, Today);

        var done = await h.PayAsync(user, created.Id, 0.01m, Today);
        Assert.Equal(TransactionStatus.Paid, done.Status);

        await ReadProblemAsync(
            await user.Post($"/api/v1/transactions/{created.Id}/payments", new { amount = 0.01m, paymentDate = Today }),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task Payments_RemoveUnknownPayment_Returns404()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        await ReadProblemAsync(await user.Delete($"/api/v1/transactions/{created.Id}/payments/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Payments_OnAnotherTransactionOfTheSameUser_CannotBeRemovedThroughTheWrongRoute()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var a = await h.CreateTxAsync(user, "A", "Expense", 100m, food, Today);
        var b = await h.CreateTxAsync(user, "B", "Expense", 100m, food, Today);
        var paidB = await h.PayAsync(user, b.Id, 10m, Today);

        await ReadProblemAsync(await user.Delete($"/api/v1/transactions/{a.Id}/payments/{paidB.Payments.Single().Id}"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(10m, (await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{b.Id}")).PaidAmount);
    }

    [Fact]
    public async Task CancelAndReopen_PreservePaymentsAndRestoreState()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        await h.PayAsync(user, created.Id, 100m, Today);

        var canceled = await ApiUser.Read<TransactionDetailDto>(await user.Post($"/api/v1/transactions/{created.Id}/cancel"));
        Assert.Equal(TransactionStatus.Canceled, canceled.Status);
        Assert.Equal(100m, canceled.PaidAmount);
        Assert.Single(canceled.Payments);
        Assert.Null(canceled.PaidAt);
        await ReadProblemAsync(await user.Post($"/api/v1/transactions/{created.Id}/cancel"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        // Remover pagamento de cancelada é proibido.
        await ReadProblemAsync(
            await user.Delete($"/api/v1/transactions/{created.Id}/payments/{canceled.Payments.Single().Id}"),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        var reopened = await ApiUser.Read<TransactionDetailDto>(await user.Post($"/api/v1/transactions/{created.Id}/reopen"));
        Assert.Equal(TransactionStatus.Paid, reopened.Status);
        Assert.Equal(Today, reopened.PaidAt);
        await ReadProblemAsync(await user.Post($"/api/v1/transactions/{created.Id}/reopen"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task Reopen_WithoutPayments_BecomesPending_AndPartialStaysPending()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var plain = await h.CreateTxAsync(user, "Sem pagamento", "Expense", 100m, food, Today, status: "Planned");
        await user.Post($"/api/v1/transactions/{plain.Id}/cancel");
        Assert.Equal(TransactionStatus.Pending, (await ApiUser.Read<TransactionDetailDto>(await user.Post($"/api/v1/transactions/{plain.Id}/reopen"))).Status);

        var partial = await h.CreateTxAsync(user, "Parcial", "Expense", 100m, food, Today);
        await h.PayAsync(user, partial.Id, 40m, Today);
        await user.Post($"/api/v1/transactions/{partial.Id}/cancel");
        var reopened = await ApiUser.Read<TransactionDetailDto>(await user.Post($"/api/v1/transactions/{partial.Id}/reopen"));
        Assert.Equal(TransactionStatus.Pending, reopened.Status);
        Assert.Equal(60m, reopened.RemainingAmount);
    }

    [Fact]
    public async Task Overdue_IsComputedOnRead_AndFilterable_ButNeverPersisted()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var overdue = await h.CreateTxAsync(user, "Vencida", "Expense", 100m, food, Today.AddDays(-30), Today.AddDays(-1));
        var dueToday = await h.CreateTxAsync(user, "Vence hoje", "Expense", 100m, food, Today.AddDays(-30), Today);
        var future = await h.CreateTxAsync(user, "Futura", "Expense", 100m, food, Today.AddDays(-30), Today.AddDays(3));
        var noDue = await h.CreateTxAsync(user, "Sem vencimento", "Expense", 100m, food, Today.AddDays(-30));
        var plannedOverdue = await h.CreateTxAsync(user, "Prevista vencida", "Expense", 100m, food, Today.AddDays(-30), Today.AddDays(-2), status: "Planned");
        var paidLate = await h.CreateTxAsync(user, "Paga", "Expense", 100m, food, Today.AddDays(-30), Today.AddDays(-5));
        await h.PayAsync(user, paidLate.Id, 100m, Today);
        var canceledOverdue = await h.CreateTxAsync(user, "Cancelada", "Expense", 100m, food, Today.AddDays(-30), Today.AddDays(-5));
        await user.Post($"/api/v1/transactions/{canceledOverdue.Id}/cancel");

        Assert.Equal(TransactionStatus.Overdue, overdue.Status);
        Assert.Equal(TransactionStatus.Pending, dueToday.Status);
        Assert.Equal(TransactionStatus.Pending, future.Status);
        Assert.Equal(TransactionStatus.Pending, noDue.Status);
        Assert.Equal(TransactionStatus.Overdue, plannedOverdue.Status);

        var overdueList = await h.ListAsync(user, "status=Overdue");
        Assert.Equal(["Prevista vencida", "Vencida"], overdueList.Items.Select(t => t.Description).Order(StringComparer.Ordinal).ToList());
        Assert.All(overdueList.Items, t => Assert.Equal(TransactionStatus.Overdue, t.Status));

        var pendingList = await h.ListAsync(user, "status=Pending");
        Assert.Equal(["Futura", "Sem vencimento", "Vence hoje"], pendingList.Items.Select(t => t.Description).Order(StringComparer.Ordinal).ToList());
        // Uma "Planned" vencida é apresentada como Overdue, portanto não aparece no filtro Planned.
        Assert.Empty((await h.ListAsync(user, "status=Planned")).Items);
        Assert.Equal(["Paga"], (await h.ListAsync(user, "status=Paid")).Items.Select(t => t.Description));
        Assert.Equal(["Cancelada"], (await h.ListAsync(user, "status=Canceled")).Items.Select(t => t.Description));

        // Persistido nunca é Overdue.
        var persisted = await h.WithDbAsync(db => db.Transactions.AsNoTracking().Where(t => t.CreatedByUserId == user.Id).Select(t => t.Status).Distinct().ToListAsync());
        Assert.DoesNotContain(TransactionStatus.Overdue, persisted);
    }

    [Fact]
    public async Task Overdue_DependsOnTheInjectedToday()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Vence amanhã", "Expense", 10m, food, Today, Today.AddDays(1));
        Assert.Equal(TransactionStatus.Pending, created.Status);

        factory.Clock.TodayOverride = Today.AddDays(2);
        try
        {
            Assert.Equal(TransactionStatus.Overdue, (await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{created.Id}")).Status);
            Assert.Single((await h.ListAsync(user, "status=Overdue")).Items);
        }
        finally
        {
            factory.Clock.TodayOverride = null;
        }
    }

    // ------------------------------------------------------------ histórico

    [Fact]
    public async Task History_ListsMostRecentFirst_WithAllActions()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);
        var withPayment = await h.PayAsync(user, created.Id, 25m, Today);
        await user.Put($"/api/v1/transactions/{created.Id}", new { description = "Mercado", amount = 120m, categoryId = food, transactionDate = Today });
        await user.Delete($"/api/v1/transactions/{created.Id}/payments/{withPayment.Payments.Single().Id}");
        await user.Post($"/api/v1/transactions/{created.Id}/cancel");
        await user.Post($"/api/v1/transactions/{created.Id}/reopen");

        var history = await user.GetOk<List<TransactionHistoryEntryDto>>($"/api/v1/transactions/{created.Id}/history");

        Assert.Equal(
            new[] { TransactionAuditAction.Reopened, TransactionAuditAction.Canceled, TransactionAuditAction.PaymentRemoved, TransactionAuditAction.Updated, TransactionAuditAction.PaymentAdded, TransactionAuditAction.Created },
            history.Select(e => e.Action));
        Assert.Equal("amount", history[3].Field);
        Assert.Equal("100.00", history[3].OldValue);
        Assert.Equal("120.00", history[3].NewValue);
        Assert.Equal("status", history[1].Field);
        Assert.Equal("Canceled", history[1].NewValue);
        Assert.Equal("Canceled", history[0].OldValue);
        Assert.Equal("Pending", history[0].NewValue);
        Assert.Contains("25.00", history[4].NewValue, StringComparison.Ordinal);
        Assert.True(history.Zip(history.Skip(1), (a, b) => a.CreatedAt >= b.CreatedAt).All(x => x));
    }

    // ------------------------------------------------------------ listagem

    private async Task<(ApiUser User, Guid Food, Guid Health, Guid Restaurants, Guid Salary, AccountDto Account)> SeedListAsync()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta");
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var health = await h.CategoryIdAsync(user, "Saúde");
        var salary = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);
        var restaurants = (await h.CreateCategoryAsync(user, "Restaurantes", parent: food)).Id;

        var d = new DateOnly(2026, 3, 1);
        await h.CreateTxAsync(user, "Mercado Central", "Expense", 100m, food, d.AddDays(0), d.AddDays(10), account.Id);
        await h.CreateTxAsync(user, "Pizza", "Expense", 50m, restaurants, d.AddDays(1), d.AddDays(2));
        await h.CreateTxAsync(user, "Remédio", "Expense", 30m, health, d.AddDays(2));
        await h.CreateTxAsync(user, "Salário março", "Income", 4000m, salary, d.AddDays(3), d.AddDays(4), account.Id);
        await h.CreateTxAsync(user, "mercado de rua", "Expense", 20m, food, d.AddDays(4), status: "Planned");
        return (user, food, health, restaurants, salary, account);
    }

    [Fact]
    public async Task List_DefaultSort_IsTransactionDateDescending_WithPagingEnvelope()
    {
        var (user, _, _, _, _, _) = await SeedListAsync();

        var page = await h.ListAsync(user);

        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(["mercado de rua", "Salário março", "Remédio", "Pizza", "Mercado Central"], page.Items.Select(t => t.Description));

        var raw = await user.GetJson("/api/v1/transactions");
        Assert.Equal(
            ["items", "page", "pageSize", "totalCount", "totalPages"],
            raw.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task List_Paging_PageSizeAndTotalPages()
    {
        var (user, _, _, _, _, _) = await SeedListAsync();

        var first = await h.ListAsync(user, "page=1&pageSize=2&sort=transactionDate");
        var second = await h.ListAsync(user, "page=2&pageSize=2&sort=transactionDate");
        var third = await h.ListAsync(user, "page=3&pageSize=2&sort=transactionDate");
        var beyond = await h.ListAsync(user, "page=4&pageSize=2&sort=transactionDate");

        Assert.Equal(5, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(["Mercado Central", "Pizza"], first.Items.Select(t => t.Description));
        Assert.Equal(["Remédio", "Salário março"], second.Items.Select(t => t.Description));
        Assert.Equal(["mercado de rua"], third.Items.Select(t => t.Description));
        Assert.Empty(beyond.Items);
        Assert.Equal(2, beyond.PageSize);
    }

    [Fact]
    public async Task List_Sorting_ByEachFieldAndDirection()
    {
        var (user, _, _, _, _, _) = await SeedListAsync();

        // amount: 20, 30, 50, 100, 4000
        Assert.Equal(
            ["mercado de rua", "Remédio", "Pizza", "Mercado Central", "Salário março"],
            (await h.ListAsync(user, "sort=amount")).Items.Select(t => t.Description));
        Assert.Equal(
            ["Salário março", "Mercado Central", "Pizza", "Remédio", "mercado de rua"],
            (await h.ListAsync(user, "sort=-amount")).Items.Select(t => t.Description));

        // transactionDate: Mercado Central (+0), Pizza (+1), Remédio (+2), Salário (+3), mercado de rua (+4)
        Assert.Equal(
            ["Mercado Central", "Pizza", "Remédio", "Salário março", "mercado de rua"],
            (await h.ListAsync(user, "sort=transactionDate")).Items.Select(t => t.Description));

        var byDescription = (await h.ListAsync(user, "sort=description")).Items.Select(t => t.Description).ToList();
        var byDescriptionDesc = (await h.ListAsync(user, "sort=-description")).Items.Select(t => t.Description).ToList();
        Assert.Equal(5, byDescription.Count);
        Assert.Equal(byDescription.AsEnumerable().Reverse(), byDescriptionDesc);

        // dueDate: Pizza (+2), Salário (+4), Mercado Central (+10); sem vencimento ficam ao final (asc) / início (desc).
        var byDue = (await h.ListAsync(user, "sort=dueDate")).Items.Select(t => t.DueDate).ToList();
        Assert.Equal(new DateOnly?[] { new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 11), null, null }, byDue);
        var byDueDesc = (await h.ListAsync(user, "sort=-dueDate")).Items.Select(t => t.DueDate).ToList();
        Assert.Equal(byDue.AsEnumerable().Reverse(), byDueDesc);

        var createdAsc = (await h.ListAsync(user, "sort=createdAt")).Items.Select(t => t.Description).ToList();
        Assert.Equal(["Mercado Central", "Pizza", "Remédio", "Salário março", "mercado de rua"], createdAsc);
        Assert.Equal(createdAsc.AsEnumerable().Reverse(), (await h.ListAsync(user, "sort=-createdAt")).Items.Select(t => t.Description));

        // Ordenação é insensível à caixa do nome do campo e o padrão é -transactionDate.
        Assert.Equal(
            (await h.ListAsync(user, "sort=-transactionDate")).Items.Select(t => t.Id),
            (await h.ListAsync(user)).Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_Filters_Date_Due_Type_Category_Account_Search()
    {
        var (user, food, health, restaurants, _, account) = await SeedListAsync();
        var d = new DateOnly(2026, 3, 1);

        Assert.Equal(["Pizza", "Remédio"], (await h.ListAsync(user, $"from={d.AddDays(1):yyyy-MM-dd}&to={d.AddDays(2):yyyy-MM-dd}&sort=transactionDate")).Items.Select(t => t.Description));
        Assert.Equal(["Salário março", "mercado de rua"], (await h.ListAsync(user, $"from={d.AddDays(3):yyyy-MM-dd}&sort=transactionDate&pageSize=10")).Items.Select(t => t.Description));
        Assert.Equal(["Pizza", "Salário março"], (await h.ListAsync(user, $"dueFrom={d.AddDays(2):yyyy-MM-dd}&dueTo={d.AddDays(4):yyyy-MM-dd}&sort=dueDate")).Items.Select(t => t.Description));
        Assert.Equal(["Salário março"], (await h.ListAsync(user, "type=Income")).Items.Select(t => t.Description));
        Assert.Equal(4, (await h.ListAsync(user, "type=Expense")).TotalCount);
        Assert.Equal(["Mercado Central", "Salário março"], (await h.ListAsync(user, $"accountId={account.Id}&sort=transactionDate")).Items.Select(t => t.Description));
        Assert.Equal(["Remédio"], (await h.ListAsync(user, $"categoryId={health}")).Items.Select(t => t.Description));

        // categoryId inclui subcategorias.
        Assert.Equal(["Mercado Central", "Pizza", "mercado de rua"], (await h.ListAsync(user, $"categoryId={food}")).Items.Select(t => t.Description).Order(StringComparer.Ordinal).ToList());
        Assert.Equal(["Pizza"], (await h.ListAsync(user, $"categoryId={restaurants}")).Items.Select(t => t.Description));

        // Busca sem diferenciar maiúsculas.
        Assert.Equal(["Mercado Central", "mercado de rua"], (await h.ListAsync(user, "search=MERCADO&sort=transactionDate")).Items.Select(t => t.Description));
        Assert.Equal(["Pizza"], (await h.ListAsync(user, "search=izz")).Items.Select(t => t.Description));
        Assert.Empty((await h.ListAsync(user, "search=inexistente")).Items);
        // Caracteres especiais de LIKE são tratados literalmente.
        Assert.Empty((await h.ListAsync(user, "search=%25")).Items);
        Assert.Empty((await h.ListAsync(user, "search=_")).Items);

        // Combinação de filtros.
        Assert.Equal(["Mercado Central"], (await h.ListAsync(user, $"type=Expense&accountId={account.Id}&search=central")).Items.Select(t => t.Description));
        Assert.Equal(["mercado de rua"], (await h.ListAsync(user, "status=Planned")).Items.Select(t => t.Description));
    }

    [Fact]
    public async Task List_InvalidParameters_Return400()
    {
        var user = await h.NewUserAsync();
        foreach (var query in new[]
                 {
                     "page=0", "page=-1", "pageSize=0", "pageSize=101", "sort=status", "sort=-id", "type=Banana", "status=Banana",
                     "from=2026-03-10&to=2026-03-01", "from=ontem", "categoryId=nao-guid",
                 })
        {
            var response = await user.Get("/api/v1/transactions?" + query);
            await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        }
    }

    [Fact]
    public async Task List_PageSize100_IsAllowed()
    {
        var user = await h.NewUserAsync();
        Assert.Equal(100, (await h.ListAsync(user, "pageSize=100")).PageSize);
    }

    // ------------------------------------------------------------ isolamento entre usuários

    [Fact]
    public async Task OtherUsersTransactions_AreInvisible_AndEveryWriteIsNotFound()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(owner, "Alimentação");
        var tx = await h.CreateTxAsync(owner, "Privada", "Expense", 100m, food, Today);
        var paid = await h.PayAsync(owner, tx.Id, 10m, Today);
        var paymentId = paid.Payments.Single().Id;

        var attempts = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Get, $"/api/v1/transactions/{tx.Id}", null),
            (HttpMethod.Put, $"/api/v1/transactions/{tx.Id}", new { description = "hack", amount = 1m, categoryId = food, transactionDate = Today }),
            (HttpMethod.Delete, $"/api/v1/transactions/{tx.Id}", null),
            (HttpMethod.Post, $"/api/v1/transactions/{tx.Id}/cancel", null),
            (HttpMethod.Post, $"/api/v1/transactions/{tx.Id}/reopen", null),
            (HttpMethod.Get, $"/api/v1/transactions/{tx.Id}/history", null),
            (HttpMethod.Post, $"/api/v1/transactions/{tx.Id}/payments", new { amount = 1m, paymentDate = Today }),
            (HttpMethod.Delete, $"/api/v1/transactions/{tx.Id}/payments/{paymentId}", null),
        };
        foreach (var (method, url, body) in attempts)
        {
            await ReadProblemAsync(await intruder.SendAsync(method, url, body), HttpStatusCode.NotFound, "not_found");
        }

        // Nada mudou para o dono.
        var untouched = await owner.GetOk<TransactionDetailDto>($"/api/v1/transactions/{tx.Id}");
        Assert.Equal("Privada", untouched.Description);
        Assert.Equal(10m, untouched.PaidAmount);
        Assert.Single(untouched.Payments);

        // Listagens e filtros do intruso nunca mostram dados do dono.
        Assert.Empty((await h.ListAsync(intruder)).Items);
        Assert.Equal(0, (await h.ListAsync(intruder, $"categoryId={food}")).TotalCount);
        Assert.Empty((await h.ListAsync(intruder, "search=Privada")).Items);
        Assert.Single((await h.ListAsync(owner)).Items);

        // A resposta 404 é idêntica à de um recurso inexistente (não revela existência).
        var foreign = await ReadProblemAsync(await intruder.Get($"/api/v1/transactions/{tx.Id}"), HttpStatusCode.NotFound, "not_found");
        var unknown = await ReadProblemAsync(await intruder.Get($"/api/v1/transactions/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(foreign.GetProperty("title").GetString(), unknown.GetProperty("title").GetString());
        Assert.Equal(foreign.TryGetProperty("detail", out _), unknown.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task UserCannotAttachOtherUsersAccountToPayment()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(owner, "Privada");
        var food = await h.CategoryIdAsync(intruder, "Alimentação");
        var tx = await h.CreateTxAsync(intruder, "Meu gasto", "Expense", 10m, food, Today);

        var response = await intruder.Post($"/api/v1/transactions/{tx.Id}/payments", new { amount = 5m, paymentDate = Today, accountId = account.Id });

        var errors = (await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("accountId", out _));
    }

    // ------------------------------------------------------------ concorrência

    [Fact]
    public async Task ConcurrentPayments_NeverExceedTheTransactionAmount()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");

        for (var round = 0; round < 3; round++)
        {
            var created = await h.CreateTxAsync(user, $"Concorrente {round}", "Expense", 100m, food, Today);

            // 8 pagamentos simultâneos de 40: no máximo dois podem caber (80 ≤ 100 < 120).
            var tasks = Enumerable.Range(0, 8)
                .Select(_ => user.Post($"/api/v1/transactions/{created.Id}/payments", new { amount = 40m, paymentDate = Today }))
                .ToList();
            var responses = await Task.WhenAll(tasks);

            var statuses = responses.Select(r => r.StatusCode).ToList();
            var created201 = statuses.Count(s => s == HttpStatusCode.Created);
            Assert.InRange(created201, 1, 2);
            Assert.All(statuses, s => Assert.Contains(s, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity }));
            foreach (var failure in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
            {
                var problem = await failure.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
                Assert.Contains(problem.GetProperty("code").GetString(), new[] { "conflict", "business_rule_violation" });
            }

            var final = await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{created.Id}");
            Assert.True(final.PaidAmount <= final.Amount);
            Assert.Equal(created201 * 40m, final.PaidAmount);
            Assert.Equal(created201, final.Payments.Count);
            Assert.Equal(final.Payments.Sum(p => p.Amount), final.PaidAmount);
        }
    }

    [Fact]
    public async Task ConcurrentPayments_ExactlyCompletingOnes_OnlyOneWins()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Disputa", "Expense", 100m, food, Today);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => user.Post($"/api/v1/transactions/{created.Id}/payments", new { amount = 100m, paymentDate = Today })));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var final = await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{created.Id}");
        Assert.Equal(TransactionStatus.Paid, final.Status);
        Assert.Equal(100m, final.PaidAmount);
        Assert.Single(final.Payments);
    }

    [Fact]
    public async Task Database_ConstraintsProtectTheInvariantsEvenBypassingTheDomain()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var created = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, Today);

        foreach (var sql in new[]
                 {
                     $"UPDATE transactions SET paid_amount = 150 WHERE id = '{created.Id}'",
                     $"UPDATE transactions SET status = 'Paid' WHERE id = '{created.Id}'",
                     $"UPDATE transactions SET amount = 0 WHERE id = '{created.Id}'",
                     $"UPDATE transactions SET due_date = '{Today.AddDays(-1):yyyy-MM-dd}' WHERE id = '{created.Id}'",
                     $"UPDATE transactions SET status = 'Overdue' WHERE id = '{created.Id}'",
                     $"INSERT INTO transaction_payments (id, transaction_id, amount, payment_date, created_by_user_id, created_at) VALUES (gen_random_uuid(), '{created.Id}', 0, '{Today:yyyy-MM-dd}', '{user.Id}', now())",
                 })
        {
            await Assert.ThrowsAnyAsync<Exception>(() => h.WithDbAsync(async db => await db.Database.ExecuteSqlRawAsync(sql)));
        }
    }
}
