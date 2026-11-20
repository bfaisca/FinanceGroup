using System.Net;
using System.Net.Http.Json;
using FinanceApp.Application.Common;
using FinanceApp.Application.Installments;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;
using static FinanceApp.IntegrationTests.Infrastructure.Etapa3Support;

namespace FinanceApp.IntegrationTests;

/// <summary>Parcelamentos (contrato §12, D-026). "Hoje" fixado em 2026-06-15.</summary>
[Collection(ApiCollection.Name)]
public sealed class InstallmentPlansApiTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 6, 15);

    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public InstallmentPlansApiTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    [Fact]
    public async Task Create_DistributesCents_FirstInstallmentAbsorbsDifference_AndGeneratesDates()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta");
        var category = await h.CategoryIdAsync(user, "Compras");

        var plan = await h.CreatePlanAsync(user, "Sofá", 100m, 3, D(6, 10), category, accountId: account.Id, paymentMethod: "BankSlip");

        Assert.Equal(["33.34", "33.33", "33.33"], plan.Installments.Select(i => i.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(100m, plan.Installments.Sum(i => i.Amount));
        Assert.Equal([D(6, 10), D(7, 10), D(8, 10)], plan.Installments.Select(i => i.TransactionDate));
        Assert.Equal([D(6, 10), D(7, 10), D(8, 10)], plan.Installments.Select(i => i.DueDate));
        // Hoje = 06-15: a 1ª já passou (Pending e, por ter vencimento anterior a hoje, apresentada como Overdue); as demais são Planned.
        Assert.Equal([TransactionStatus.Overdue, TransactionStatus.Planned, TransactionStatus.Planned], plan.Installments.Select(i => i.Status));
        Assert.All(plan.Installments, i =>
        {
            Assert.Equal("Sofá", i.Description);
            Assert.Equal(TransactionType.Expense, i.Type);
            Assert.Equal(account.Id, i.AccountId);
            Assert.Equal(PaymentMethod.BankSlip, i.PaymentMethod);
            Assert.Equal(plan.Id, i.Installment!.PlanId);
            Assert.Equal(3, i.Installment.Count);
            Assert.Null(i.CreditCardId);
        });
        Assert.Equal([1, 2, 3], plan.Installments.Select(i => i.Installment!.Number));

        Assert.Equal(InstallmentPlanStatus.Open, plan.Status);
        Assert.Equal(100m, plan.TotalAmount);
        Assert.Equal(3, plan.InstallmentCount);
        Assert.Equal(D(6, 10), plan.FirstDate);
        Assert.Equal((0, 3, 0), (plan.PaidCount, plan.OpenCount, plan.CanceledCount));
        Assert.Equal(0m, plan.PaidAmount);
        Assert.Equal(100m, plan.RemainingAmount);
        Assert.Equal(D(6, 10), plan.NextDueDate);
        Assert.Equal("Conta", plan.AccountName);
        Assert.Equal("Compras", plan.CategoryName);
    }

    [Fact]
    public async Task Create_PersistedStatuses_ArePlannedOrPending_BasedOnTransactionDate()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");

        var plan = await h.CreatePlanAsync(user, "Status", 30m, 3, D(6, 15), category); // 06-15 (hoje) → Pending; futuras → Planned

        var persisted = await h.WithDbAsync(db => db.Transactions.AsNoTracking()
            .Where(t => t.InstallmentPlanId == plan.Id).OrderBy(t => t.InstallmentNumber).Select(t => t.Status).ToListAsync());
        Assert.Equal([TransactionStatus.Pending, TransactionStatus.Planned, TransactionStatus.Planned], persisted);
    }

    [Fact]
    public async Task Create_MonthEndDates_AreClamped_WithoutDrift()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");

        var plan = await h.CreatePlanAsync(user, "Fim de mês", 400m, 5, D(1, 31), category);

        Assert.Equal([D(1, 31), D(2, 28), D(3, 31), D(4, 30), D(5, 31)], plan.Installments.Select(i => i.TransactionDate));
    }

    [Fact]
    public async Task Create_AcrossYearBoundary_AndTwoAndOneTwentyInstallments()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");

        var two = await h.CreatePlanAsync(user, "Dois", 0.03m, 2, D(12, 15), category);
        Assert.Equal([0.02m, 0.01m], two.Installments.Select(i => i.Amount));
        Assert.Equal([D(12, 15), D(1, 15, 2027)], two.Installments.Select(i => i.TransactionDate));

        var many = await h.CreatePlanAsync(user, "Cento e vinte", 1000m, 120, D(6, 20), category);
        Assert.Equal(120, many.Installments.Count);
        Assert.Equal(1000m, many.Installments.Sum(i => i.Amount));
        // 1000/120 = 8,333… → base 8,33; 120 × 8,33 = 999,60; a diferença de 0,40 vai para a 1ª parcela: 8,73.
        Assert.Equal(8.73m, many.Installments[0].Amount);
        Assert.All(many.Installments.Skip(1), i => Assert.Equal(8.33m, i.Amount));
    }

    [Fact]
    public async Task Create_WithCard_CalculatesInvoiceDueDate_PerInstallment_AndHasNoAccount()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta");
        var card = await h.CreateCardAsync(user, "Cartão", closingDay: 10, dueDay: 5, accountId: account.Id);
        var category = await h.CategoryIdAsync(user, "Compras");

        var plan = await h.CreatePlanAsync(user, "Notebook", 300m, 3, D(6, 12), category, cardId: card.Id);

        // dueDay 5 ≤ closingDay 10: vence no mês seguinte ao do fechamento. 06-12 → fecha 07-10 → vence 08-05.
        Assert.Equal([D(6, 12), D(7, 12), D(8, 12)], plan.Installments.Select(i => i.TransactionDate));
        Assert.Equal([D(8, 5), D(9, 5), D(10, 5)], plan.Installments.Select(i => i.DueDate));
        Assert.All(plan.Installments, i =>
        {
            Assert.Equal(card.Id, i.CreditCardId);
            Assert.Null(i.AccountId);
            Assert.Equal(PaymentMethod.CreditCard, i.PaymentMethod);
        });
        Assert.Equal(card.Id, plan.CreditCardId);
        Assert.Equal("Cartão", plan.CreditCardName);
        Assert.Null(plan.AccountId);
        Assert.Equal(PaymentMethod.CreditCard, plan.PaymentMethod);
        Assert.Equal(D(8, 5), plan.NextDueDate);
    }

    [Fact]
    public async Task Create_Validation_ReturnsFieldErrors()
    {
        var user = await h.NewUserAsync();
        var other = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var salary = await h.CategoryIdAsync(user, "Salário", FinanceApp.Domain.Categories.CategoryType.Income);
        var account = await h.CreateAccountAsync(user, "Conta");
        var card = await h.CreateCardAsync(user, "Cartão");
        var foreignCard = await h.CreateCardAsync(other, "Alheio");
        var foreignAccount = await h.CreateAccountAsync(other, "Alheia");

        async Task<System.Text.Json.JsonElement> Bad(object body, HttpStatusCode status = HttpStatusCode.BadRequest, string code = "validation_error") =>
            await ReadProblemAsync(await user.Post("/api/v1/installment-plans", body), status, code);

        Assert.True((await Bad(PlanBody("X", 100m, 1, D(6, 1), category))).HasError("installmentCount"));
        Assert.True((await Bad(PlanBody("X", 100m, 121, D(6, 1), category))).HasError("installmentCount"));
        Assert.True((await Bad(PlanBody("X", 0m, 3, D(6, 1), category))).HasError("totalAmount"));
        Assert.True((await Bad(PlanBody("X", 10.005m, 3, D(6, 1), category))).HasError("totalAmount"));
        Assert.True((await Bad(PlanBody("X", 0.05m, 10, D(6, 1), category))).HasError("totalAmount")); // < R$ 0,01 por parcela
        Assert.True((await Bad(PlanBody(" ", 100m, 3, D(6, 1), category))).HasError("description"));
        Assert.True((await Bad(new { description = "X", totalAmount = 100m, installmentCount = 3, categoryId = category })).HasError("firstDate"));
        Assert.True((await Bad(PlanBody("X", 100m, 3, D(6, 1), Guid.Empty))).HasError("categoryId"));
        Assert.True((await Bad(PlanBody("X", 100m, 3, D(6, 1), Guid.NewGuid()))).HasError("categoryId"));
        Assert.True((await Bad(PlanBody("X", 100m, 3, D(6, 1), salary))).HasError("categoryId")); // parcelas são despesas
        Assert.True((await Bad(PlanBody("X", 100m, 3, D(6, 1), category, accountId: account.Id, cardId: card.Id))).HasError("accountId"));
        Assert.True((await Bad(PlanBody("X", 100m, 3, D(6, 1), category, cardId: card.Id, paymentMethod: "Pix"))).HasError("paymentMethod"));
        Assert.True((await Bad(PlanBody("X", 100m, 3, D(6, 1), category, cardId: foreignCard.Id))).HasError("creditCardId"));
        Assert.True((await Bad(PlanBody("X", 100m, 3, D(6, 1), category, accountId: foreignAccount.Id))).HasError("accountId"));
        Assert.True((await Bad(PlanBody("X", 100m, 3, D(6, 1), category, notes: new string('n', 1001)))).HasError("notes"));
    }

    [Fact]
    public async Task Create_WithInactiveCardAccountOrCategory_Returns422()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var card = await h.CreateCardAsync(user, "Cartão");
        var account = await h.CreateAccountAsync(user, "Conta");
        var custom = await h.CreateCategoryAsync(user, "Custom");
        await user.Post($"/api/v1/credit-cards/{card.Id}/deactivate");
        await user.Post($"/api/v1/accounts/{account.Id}/deactivate");
        await user.Post($"/api/v1/categories/{custom.Id}/deactivate");

        await ReadProblemAsync(await user.Post("/api/v1/installment-plans", PlanBody("X", 100m, 3, D(6, 1), category, cardId: card.Id)), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await ReadProblemAsync(await user.Post("/api/v1/installment-plans", PlanBody("X", 100m, 3, D(6, 1), category, accountId: account.Id)), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await ReadProblemAsync(await user.Post("/api/v1/installment-plans", PlanBody("X", 100m, 3, D(6, 1), custom.Id)), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        // Nada foi persistido (transação única).
        Assert.Equal(0, (await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans")).TotalCount);
    }

    [Fact]
    public async Task Pay_Installments_UpdatesPlanAggregates_AndNextDueDate()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var plan = await h.CreatePlanAsync(user, "Geladeira", 100m, 3, D(6, 10), category);

        await h.PayAsync(user, plan.Installments[0].Id, 33.34m, D(6, 14));
        await h.PayAsync(user, plan.Installments[1].Id, 10m, D(6, 14)); // parcial: continua em aberto

        var current = await user.GetOk<InstallmentPlanDetailDto>($"/api/v1/installment-plans/{plan.Id}");
        Assert.Equal((1, 2, 0), (current.PaidCount, current.OpenCount, current.CanceledCount));
        Assert.Equal(43.34m, current.PaidAmount);
        Assert.Equal(56.66m, current.RemainingAmount);
        Assert.Equal(D(7, 10), current.NextDueDate);
        Assert.Equal(InstallmentPlanStatus.Open, current.Status);

        await h.PayAsync(user, plan.Installments[1].Id, 23.33m, D(6, 14));
        await h.PayAsync(user, plan.Installments[2].Id, 33.33m, D(6, 14));
        var done = await user.GetOk<InstallmentPlanDetailDto>($"/api/v1/installment-plans/{plan.Id}");
        Assert.Equal(InstallmentPlanStatus.Completed, done.Status);
        Assert.Equal((3, 0, 0), (done.PaidCount, done.OpenCount, done.CanceledCount));
        Assert.Equal(100m, done.PaidAmount);
        Assert.Equal(0m, done.RemainingAmount);
        Assert.Null(done.NextDueDate);
    }

    [Fact]
    public async Task Installment_AmountAndDatesAreImmutable_OtherFieldsEditable()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var other = await h.CategoryIdAsync(user, "Lazer");
        var plan = await h.CreatePlanAsync(user, "Mesa", 90m, 3, D(7, 10), category);
        var installment = plan.Installments[1];

        object Body(decimal? amount = null, DateOnly? date = null, DateOnly? due = null, Guid? categoryId = null, string description = "Mesa", string? notes = null) =>
            new
            {
                description,
                amount = amount ?? installment.Amount,
                categoryId = categoryId ?? category,
                transactionDate = date ?? installment.TransactionDate,
                dueDate = due ?? installment.DueDate,
                notes,
            };

        await ReadProblemAsync(await user.Put($"/api/v1/transactions/{installment.Id}", Body(amount: 31m)), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await ReadProblemAsync(await user.Put($"/api/v1/transactions/{installment.Id}", Body(date: D(7, 11), due: D(7, 11))), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        // Sem cartão, categoria, observações, descrição e vencimento podem ser editados.
        var edited = await ApiUser.Read<TransactionDetailDto>(
            await user.Put($"/api/v1/transactions/{installment.Id}", Body(due: D(8, 25), categoryId: other, description: "Mesa de jantar", notes: "obs")));
        Assert.Equal(D(8, 25), edited.DueDate);
        Assert.Equal(other, edited.CategoryId);
        Assert.Equal("obs", edited.Notes);
        Assert.Equal(installment.Amount, edited.Amount);
        Assert.Equal(2, edited.Installment!.Number);

        // Uma edição vazia (mesmos valores) é aceita.
        await ApiUser.Read<TransactionDetailDto>(await user.Put($"/api/v1/transactions/{installment.Id}", Body(due: D(8, 25), categoryId: other, description: "Mesa de jantar", notes: "obs")));
    }

    [Fact]
    public async Task Installment_OnCard_DueDateIsImmutable_422()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var card = await h.CreateCardAsync(user, "Cartão", closingDay: 10, dueDay: 20);
        var plan = await h.CreatePlanAsync(user, "TV", 60m, 2, D(7, 5), category, cardId: card.Id);
        var installment = plan.Installments[0];

        object Body(DateOnly? due) =>
            new { description = "TV", amount = installment.Amount, categoryId = category, transactionDate = installment.TransactionDate, dueDate = due };

        await ReadProblemAsync(await user.Put($"/api/v1/transactions/{installment.Id}", Body(D(7, 28))), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        // Reenviar o vencimento atual ou omiti-lo é aceito.
        await ApiUser.Read<TransactionDetailDto>(await user.Put($"/api/v1/transactions/{installment.Id}", Body(installment.DueDate)));
        await ApiUser.Read<TransactionDetailDto>(await user.Put($"/api/v1/transactions/{installment.Id}", Body(null)));
    }

    [Fact]
    public async Task Installment_CannotBeDeletedIndividually_422_ButCanBeCanceledAndReopened()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var plan = await h.CreatePlanAsync(user, "Fogão", 60m, 2, D(7, 1), category);
        var first = plan.Installments[0];

        var problem = await ReadProblemAsync(await user.Delete($"/api/v1/transactions/{first.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("detail").GetString()));
        Assert.NotNull(await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{first.Id}"));

        var canceled = await ApiUser.Read<TransactionDetailDto>(await user.Post($"/api/v1/transactions/{first.Id}/cancel"));
        Assert.Equal(TransactionStatus.Canceled, canceled.Status);
        var current = await user.GetOk<InstallmentPlanDetailDto>($"/api/v1/installment-plans/{plan.Id}");
        Assert.Equal((0, 1, 1), (current.PaidCount, current.OpenCount, current.CanceledCount));
        Assert.Equal(30m, current.RemainingAmount);
        Assert.Equal(InstallmentPlanStatus.Open, current.Status);
        await user.Post($"/api/v1/transactions/{first.Id}/reopen");
        Assert.Equal(2, (await user.GetOk<InstallmentPlanDetailDto>($"/api/v1/installment-plans/{plan.Id}")).OpenCount);
    }

    [Fact]
    public async Task CancelPlan_CancelsUnpaidInstallments_PreservesPaid()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var plan = await h.CreatePlanAsync(user, "Cama", 90m, 3, D(6, 10), category);
        await h.PayAsync(user, plan.Installments[0].Id, 30m, D(6, 12));
        await h.PayAsync(user, plan.Installments[1].Id, 5m, D(6, 12)); // pagamento parcial também preserva a parcela

        var canceled = await ApiUser.Read<InstallmentPlanDetailDto>(await user.Post($"/api/v1/installment-plans/{plan.Id}/cancel"));

        Assert.Equal(
            [TransactionStatus.Paid, TransactionStatus.Pending, TransactionStatus.Canceled],
            canceled.Installments.Select(i => i.Status == TransactionStatus.Overdue ? TransactionStatus.Pending : i.Status));
        Assert.Equal((1, 1, 1), (canceled.PaidCount, canceled.OpenCount, canceled.CanceledCount));
        Assert.Equal(35m, canceled.PaidAmount);
        Assert.Equal(25m, canceled.RemainingAmount);
        Assert.Equal(InstallmentPlanStatus.Open, canceled.Status);

        // Cancelar de novo é idempotente.
        var again = await ApiUser.Read<InstallmentPlanDetailDto>(await user.Post($"/api/v1/installment-plans/{plan.Id}/cancel"));
        Assert.Equal((1, 1, 1), (again.PaidCount, again.OpenCount, again.CanceledCount));

        var history = await user.GetOk<List<TransactionHistoryEntryDto>>($"/api/v1/transactions/{canceled.Installments[2].Id}/history");
        Assert.Contains(history, e => e.Action == TransactionAuditAction.Canceled);
        Assert.Contains(history, e => e.Action == TransactionAuditAction.Created);
    }

    [Fact]
    public async Task CancelPlan_WithoutPayments_MarksPlanCanceled_AndLeavesTotalsAndProjectionClean()
    {
        var user = await h.NewUserAsync();
        var account = await h.CreateAccountAsync(user, "Conta", balance: 1000m, referenceDate: D(6, 1));
        var category = await h.CategoryIdAsync(user, "Compras");
        var plan = await h.CreatePlanAsync(user, "Cancelável", 90m, 3, D(6, 10), category, accountId: account.Id);
        Assert.Equal(970m, (await user.GetOk<FinanceApp.Application.Accounts.AccountDto>($"/api/v1/accounts/{account.Id}?asOf=2026-06-10")).ProjectedBalance);
        Assert.Equal(940m, (await user.GetOk<FinanceApp.Application.Accounts.AccountDto>($"/api/v1/accounts/{account.Id}?asOf=2026-07-10")).ProjectedBalance);

        var canceled = await ApiUser.Read<InstallmentPlanDetailDto>(await user.Post($"/api/v1/installment-plans/{plan.Id}/cancel"));

        Assert.Equal(InstallmentPlanStatus.Canceled, canceled.Status);
        Assert.Equal((0, 0, 3), (canceled.PaidCount, canceled.OpenCount, canceled.CanceledCount));
        Assert.Equal(0m, canceled.RemainingAmount);
        Assert.Null(canceled.NextDueDate);
        Assert.All(canceled.Installments, i => Assert.Equal(TransactionStatus.Canceled, i.Status));
        Assert.Equal(1000m, (await user.GetOk<FinanceApp.Application.Accounts.AccountDto>($"/api/v1/accounts/{account.Id}?asOf=2026-09-30")).ProjectedBalance);
    }

    [Fact]
    public async Task DeletePlan_WithoutPayments_RemovesPlanAndInstallments()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var plan = await h.CreatePlanAsync(user, "Descartável", 90m, 3, D(6, 10), category);

        var response = await user.Delete($"/api/v1/installment-plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await ReadProblemAsync(await user.Get($"/api/v1/installment-plans/{plan.Id}"), HttpStatusCode.NotFound, "not_found");
        Assert.Empty((await h.ListAsync(user, $"installmentPlanId={plan.Id}")).Items);
        Assert.Equal(0, await h.WithDbAsync(db => db.Transactions.CountAsync(t => t.InstallmentPlanId == plan.Id)));
    }

    [Fact]
    public async Task DeletePlan_WithAnyPayment_Returns422_AndKeepsEverything()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var plan = await h.CreatePlanAsync(user, "Com pagamento", 90m, 3, D(6, 10), category);
        await h.PayAsync(user, plan.Installments[2].Id, 1m, D(6, 12));

        await ReadProblemAsync(await user.Delete($"/api/v1/installment-plans/{plan.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        Assert.Equal(3, (await user.GetOk<InstallmentPlanDetailDto>($"/api/v1/installment-plans/{plan.Id}")).Installments.Count);

        // Cancelar o plano libera nada: o pagamento continua impedindo a exclusão.
        await user.Post($"/api/v1/installment-plans/{plan.Id}/cancel");
        await ReadProblemAsync(await user.Delete($"/api/v1/installment-plans/{plan.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task List_FiltersByStatus_Sorts_AndPaginates()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");
        var open = await h.CreatePlanAsync(user, "B aberto", 50m, 2, D(8, 1), category);
        var done = await h.CreatePlanAsync(user, "A concluído", 20m, 2, D(6, 1), category);
        foreach (var i in done.Installments)
        {
            await h.PayAsync(user, i.Id, i.Amount, D(6, 10));
        }

        var canceled = await h.CreatePlanAsync(user, "C cancelado", 40m, 2, D(9, 1), category);
        await user.Post($"/api/v1/installment-plans/{canceled.Id}/cancel");

        var all = await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans?sort=description");
        Assert.Equal(["A concluído", "B aberto", "C cancelado"], all.Items.Select(p => p.Description));
        Assert.Equal(
            [InstallmentPlanStatus.Completed, InstallmentPlanStatus.Open, InstallmentPlanStatus.Canceled],
            all.Items.Select(p => p.Status));

        Assert.Equal([open.Id], (await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans?status=Open")).Items.Select(p => p.Id));
        Assert.Equal([done.Id], (await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans?status=Completed")).Items.Select(p => p.Id));
        Assert.Equal([canceled.Id], (await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans?status=Canceled")).Items.Select(p => p.Id));

        var byDate = await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans?sort=-firstDate");
        Assert.Equal(["C cancelado", "B aberto", "A concluído"], byDate.Items.Select(p => p.Description));
        var byTotal = await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans?sort=totalAmount");
        Assert.Equal([20m, 40m, 50m], byTotal.Items.Select(p => p.TotalAmount));

        var page = await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans?pageSize=2&page=2&sort=description");
        Assert.Equal((3, 2, 2), (page.TotalCount, page.TotalPages, page.Page));
        Assert.Equal(["C cancelado"], page.Items.Select(p => p.Description));

        // Padrão: -createdAt (o mais recente primeiro).
        var byDefault = await user.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans");
        Assert.Equal(["C cancelado", "A concluído", "B aberto"], byDefault.Items.Select(p => p.Description));
    }

    [Fact]
    public async Task List_InvalidParameters_Return400()
    {
        var user = await h.NewUserAsync();
        await ReadProblemAsync(await user.Get("/api/v1/installment-plans?pageSize=0"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await user.Get("/api/v1/installment-plans?pageSize=101"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await user.Get("/api/v1/installment-plans?page=0"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await user.Get("/api/v1/installment-plans?sort=bogus"), HttpStatusCode.BadRequest, "validation_error");
        await ReadProblemAsync(await user.Get("/api/v1/installment-plans?status=Bogus"), HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task OtherUsersPlan_Returns404_OnEveryRoute_AndIsInvisibleInLists()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(owner, "Compras");
        var plan = await h.CreatePlanAsync(owner, "Privado", 60m, 2, D(7, 1), category);

        await ReadProblemAsync(await intruder.Get($"/api/v1/installment-plans/{plan.Id}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await intruder.Post($"/api/v1/installment-plans/{plan.Id}/cancel"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await intruder.Delete($"/api/v1/installment-plans/{plan.Id}"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(0, (await intruder.GetOk<PagedResult<InstallmentPlanDto>>("/api/v1/installment-plans")).TotalCount);
        Assert.Empty((await h.ListAsync(intruder, $"installmentPlanId={plan.Id}")).Items);
        await ReadProblemAsync(await intruder.Get($"/api/v1/transactions/{plan.Installments[0].Id}"), HttpStatusCode.NotFound, "not_found");

        // O plano do dono permanece intacto.
        Assert.Equal(2, (await owner.GetOk<InstallmentPlanDetailDto>($"/api/v1/installment-plans/{plan.Id}")).OpenCount);
    }

    [Fact]
    public async Task Create_UsesDefaultCategory_OfAnotherUser_Returns400()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var custom = await h.CreateCategoryAsync(owner, "Minha");

        var problem = await ReadProblemAsync(
            await intruder.Post("/api/v1/installment-plans", PlanBody("X", 10m, 2, D(6, 1), custom.Id)),
            HttpStatusCode.BadRequest,
            "validation_error");
        Assert.True(problem.HasError("categoryId"));
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await h.Client.PostAsJsonAsync("/api/v1/installment-plans", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsLocationAndContractShape()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Compras");

        var response = await user.Post("/api/v1/installment-plans", PlanBody("Forma", 60m, 2, D(7, 1), category));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ApiUser.Json);
        Assert.Equal(
            [
                "accountId", "accountName", "canceledCount", "categoryId", "categoryName", "createdAt", "creditCardId", "creditCardName",
                "description", "firstDate", "id", "installmentCount", "installments", "nextDueDate", "openCount", "paidAmount", "paidCount",
                "paymentMethod", "remainingAmount", "status", "totalAmount",
            ],
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("Open", json.GetProperty("status").GetString());
        var first = json.GetProperty("installments")[0];
        Assert.Equal(["count", "number", "planId"], first.GetProperty("installment").EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }
}
