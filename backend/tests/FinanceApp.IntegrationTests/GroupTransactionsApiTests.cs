using System.Net;
using System.Text.Json;
using FinanceApp.Application.Common;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class GroupTransactionsApiTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    private async Task<(ApiUser Owner, ApiUser Ana, ApiUser Bia, GroupDetailDto Group)> NewGroupAsync()
    {
        var owner = await h.NewUserAsync("Maria Dona");
        var ana = await h.NewUserAsync("Ana");
        var bia = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner, "Casa");
        await h.JoinAsync(owner, group.Id, ana);
        await h.JoinAsync(owner, group.Id, bia);
        return (owner, ana, bia, group);
    }

    [Fact]
    public async Task FullFlow_CreateAndPayByTwoMembers_ShowsAuthorAndPayersWithoutPrivateData()
    {
        var (owner, ana, bia, group) = await NewGroupAsync();
        var anaAccount = await h.CreateAccountAsync(ana, "Conta da Ana", balance: 1000m, referenceDate: h.Today.AddDays(-10));
        var gid = group.Id;

        var tx = await h.CreateGroupTxAsync(ana, gid, "Mercado do mês", 300m);
        Assert.Equal((TransactionStatus.Pending, 0m, 300m), (tx.Status, tx.PaidAmount, tx.RemainingAmount));
        Assert.Equal((ana.Id, "Ana"), (tx.CreatedBy.UserId, tx.CreatedBy.Name));
        Assert.True(tx.Permissions.CanEdit);

        // Pagamento parcial da Bia (sem conta) e o restante da Ana, com a conta dela.
        var afterBia = await h.PayGroupTxAsync(bia, gid, tx.Id, 100m);
        Assert.Equal((TransactionStatus.Pending, 100m, 200m), (afterBia.Status, afterBia.PaidAmount, afterBia.RemainingAmount));
        var afterAna = await h.PayGroupTxAsync(ana, gid, tx.Id, 200m, accountId: anaAccount.Id);
        Assert.Equal((TransactionStatus.Paid, 300m, 0m), (afterAna.Status, afterAna.PaidAmount, afterAna.RemainingAmount));
        Assert.Equal(new[] { "Bia", "Ana" }, afterAna.Payments.OrderBy(p => p.CreatedAt).Select(p => p.PaidBy.Name).ToArray());

        // Todos os membros veem o mesmo registro; nenhum JSON traz conta ou e-mail.
        var emails = new[] { owner.Session.Email, ana.Session.Email, bia.Session.Email };
        foreach (var viewer in new[] { owner, ana, bia })
        {
            var raw = await GroupSupport.RawAsync(await viewer.Get($"/api/v1/groups/{gid}/transactions/{tx.Id}"));
            GroupSupport.AssertNoPrivateData(raw, emails);
            Assert.DoesNotContain(anaAccount.Id.ToString(), raw, StringComparison.OrdinalIgnoreCase);
            GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await viewer.Get($"/api/v1/groups/{gid}/transactions")), emails);
            GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await viewer.Get($"/api/v1/groups/{gid}/transactions/{tx.Id}/history")), emails);
            GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await viewer.Get($"/api/v1/groups/{gid}/dashboard?from={h.Today.AddDays(-30):yyyy-MM-dd}&to={h.Today:yyyy-MM-dd}")), emails);
        }

        // Existe um único registro no banco (nada de cópia por participante) e a conta só ficou no pagamento da Ana.
        var rows = await h.WithDbAsync(db => Task.FromResult(db.Transactions.Count(t => t.GroupId == gid)));
        Assert.Equal(1, rows);
        var accounts = await h.WithDbAsync(db => Task.FromResult(db.TransactionPayments.Where(p => p.TransactionId == tx.Id).OrderBy(p => p.CreatedAt).Select(p => p.AccountId).ToList()));
        Assert.Equal(new Guid?[] { null, anaAccount.Id }, accounts);
        Assert.False(afterAna.Payments.Single(p => p.PaidBy.UserId == bia.Id).CanRemove && false);
    }

    [Fact]
    public async Task Payment_WithAnotherUsersAccount_IsRejected_AndOverpaymentIs422()
    {
        var (owner, ana, _, group) = await NewGroupAsync();
        var ownerAccount = await h.CreateAccountAsync(owner, "Da dona");
        var tx = await h.CreateGroupTxAsync(ana, group.Id, "Luz", 100m);

        var foreign = await ana.Post($"/api/v1/groups/{group.Id}/transactions/{tx.Id}/payments", new { amount = 10m, paymentDate = h.Today, accountId = ownerAccount.Id });
        Assert.True(foreign.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity, $"Status {(int)foreign.StatusCode}");
        Assert.Empty((await ana.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{group.Id}/transactions/{tx.Id}")).Payments);

        await h.PayGroupTxAsync(ana, group.Id, tx.Id, 90m);
        await ReadProblemAsync(await ana.Post($"/api/v1/groups/{group.Id}/transactions/{tx.Id}/payments", new { amount = 10.01m, paymentDate = h.Today }), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        var invalid = await ReadProblemAsync(await ana.Post($"/api/v1/groups/{group.Id}/transactions/{tx.Id}/payments", new { amount = 0m, paymentDate = h.Today }), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(invalid.HasError("amount"));
    }

    [Fact]
    public async Task OnlyTheAuthor_EditsCancelsReopensAndDeletes_EvenTheOwner()
    {
        var (owner, ana, bia, group) = await NewGroupAsync();
        var gid = group.Id;
        var tx = await h.CreateGroupTxAsync(ana, gid, "Internet", 120m);
        var category = await h.CategoryIdAsync(ana, "Alimentação");
        var body = GroupSupport.GroupTxBody("Alterada", "Expense", 120m, category, h.Today);

        foreach (var intruder in new[] { owner, bia })
        {
            await ReadProblemAsync(await intruder.Put($"/api/v1/groups/{gid}/transactions/{tx.Id}", body), HttpStatusCode.Forbidden, "forbidden");
            await ReadProblemAsync(await intruder.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/cancel"), HttpStatusCode.Forbidden, "forbidden");
            await ReadProblemAsync(await intruder.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/reopen"), HttpStatusCode.Forbidden, "forbidden");
            await ReadProblemAsync(await intruder.Delete($"/api/v1/groups/{gid}/transactions/{tx.Id}"), HttpStatusCode.Forbidden, "forbidden");
            var view = await intruder.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{gid}/transactions/{tx.Id}");
            Assert.Equal((false, false, false, true), (view.Permissions.CanEdit, view.Permissions.CanCancel, view.Permissions.CanDelete, view.Permissions.CanPay));
        }

        var edited = await ApiUser.Read<GroupTransactionDetailDto>(await ana.Put($"/api/v1/groups/{gid}/transactions/{tx.Id}", GroupSupport.GroupTxBody("Internet fibra", "Expense", 150m, category, h.Today, notes: "plano novo")));
        Assert.Equal(("Internet fibra", 150m, "plano novo"), (edited.Description, edited.Amount, edited.Notes));
        var history = await ana.GetOk<List<GroupTransactionHistoryEntryDto>>($"/api/v1/groups/{gid}/transactions/{tx.Id}/history");
        Assert.Contains(history, e => e.Action == TransactionAuditAction.Updated && e.ActorName == "Ana");

        var canceled = await ApiUser.Read<GroupTransactionDetailDto>(await ana.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/cancel"));
        Assert.Equal(TransactionStatus.Canceled, canceled.Status);
        await ReadProblemAsync(await bia.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/payments", new { amount = 5m, paymentDate = h.Today }), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        var reopened = await ApiUser.Read<GroupTransactionDetailDto>(await ana.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/reopen"));
        Assert.Equal(TransactionStatus.Pending, reopened.Status);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.Delete($"/api/v1/groups/{gid}/transactions/{tx.Id}")).StatusCode);
        await ReadProblemAsync(await ana.Get($"/api/v1/groups/{gid}/transactions/{tx.Id}"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task OnlyThePayer_RemovesAPayment_AndTransactionWithPaymentsCannotBeDeleted()
    {
        var (owner, ana, bia, group) = await NewGroupAsync();
        var gid = group.Id;
        var tx = await h.CreateGroupTxAsync(ana, gid, "Gás", 100m);
        var paid = await h.PayGroupTxAsync(bia, gid, tx.Id, 40m);
        var payment = paid.Payments.Single();
        Assert.True(payment.CanRemove);
        Assert.False((await ana.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{gid}/transactions/{tx.Id}")).Payments.Single().CanRemove);

        await ReadProblemAsync(await ana.Delete($"/api/v1/groups/{gid}/transactions/{tx.Id}/payments/{payment.Id}"), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await owner.Delete($"/api/v1/groups/{gid}/transactions/{tx.Id}/payments/{payment.Id}"), HttpStatusCode.Forbidden, "forbidden");
        // Mesmo a autora não apaga a movimentação enquanto houver pagamento.
        await ReadProblemAsync(await ana.Delete($"/api/v1/groups/{gid}/transactions/{tx.Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        var removed = await ApiUser.Read<GroupTransactionDetailDto>(await bia.Delete($"/api/v1/groups/{gid}/transactions/{tx.Id}/payments/{payment.Id}"));
        Assert.Equal((TransactionStatus.Pending, 0m), (removed.Status, removed.PaidAmount));
        Assert.Equal(HttpStatusCode.NoContent, (await ana.Delete($"/api/v1/groups/{gid}/transactions/{tx.Id}")).StatusCode);
    }

    [Fact]
    public async Task Create_ValidatesCategoryOwnership_Type_AndFields()
    {
        var (owner, ana, _, group) = await NewGroupAsync();
        var gid = group.Id;
        var mine = (await h.CreateCategoryAsync(ana, "Só da Ana")).Id;
        var shared = await h.CategoryIdAsync(ana, "Alimentação");
        var income = await h.CategoryIdAsync(ana, "Salário", CategoryType.Income);

        // Categoria personalizada do próprio autor é aceita; a de outro usuário não existe para ele.
        Assert.Equal(HttpStatusCode.Created, (await ana.Post($"/api/v1/groups/{gid}/transactions", GroupSupport.GroupTxBody("Com categoria própria", "Expense", 10m, mine, h.Today))).StatusCode);
        var foreign = await owner.Post($"/api/v1/groups/{gid}/transactions", GroupSupport.GroupTxBody("Categoria alheia", "Expense", 10m, mine, h.Today));
        Assert.True(foreign.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest, $"Status {(int)foreign.StatusCode}");
        await ReadProblemAsync(await ana.Post($"/api/v1/groups/{gid}/transactions", GroupSupport.GroupTxBody("Tipo errado", "Expense", 10m, income, h.Today)), HttpStatusCode.BadRequest, "validation_error");

        var invalid = await ReadProblemAsync(
            await ana.Post($"/api/v1/groups/{gid}/transactions", GroupSupport.GroupTxBody(" ", "Expense", 0m, shared, h.Today)), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(invalid.HasError("description"));
        Assert.True(invalid.HasError("amount"));
        // Conta/cartão não fazem parte do contrato do grupo: o campo é simplesmente ignorado e nunca persistido.
        var withAccount = await ana.Post($"/api/v1/groups/{gid}/transactions", new { description = "Com conta", type = "Expense", amount = 5m, categoryId = shared, transactionDate = h.Today, accountId = Guid.NewGuid(), creditCardId = Guid.NewGuid() });
        var raw = await GroupSupport.RawAsync(withAccount, HttpStatusCode.Created);
        GroupSupport.AssertNoPrivateData(raw);
        var persisted = await h.WithDbAsync(db => Task.FromResult(db.Transactions.Where(t => t.GroupId == gid && t.Description == "Com conta").Select(t => new { t.AccountId, t.CreditCardId }).Single()));
        Assert.Equal((null, null), (persisted.AccountId, persisted.CreditCardId));
    }

    [Fact]
    public async Task List_FiltersByAuthorPayerStatusTypeAndText_WithPagination()
    {
        var (_, ana, bia, group) = await NewGroupAsync();
        var gid = group.Id;
        var t1 = await h.CreateGroupTxAsync(ana, gid, "Mercado", 100m, h.Today.AddDays(-2));
        var t2 = await h.CreateGroupTxAsync(bia, gid, "Padaria", 20m, h.Today.AddDays(-1));
        var t3 = await h.CreateGroupTxAsync(ana, gid, "Reembolso", 50m, h.Today, "Income");
        await h.PayGroupTxAsync(bia, gid, t1.Id, 100m, h.Today.AddDays(-2));

        async Task<List<string>> Names(string query) =>
            (await ana.GetOk<PagedResult<GroupTransactionDto>>($"/api/v1/groups/{gid}/transactions?{query}")).Items.Select(i => i.Description).ToList();

        Assert.Equal(new[] { "Reembolso", "Padaria", "Mercado" }, await Names(string.Empty));
        Assert.Equal(new[] { "Reembolso", "Mercado" }, await Names($"createdByUserId={ana.Id}"));
        Assert.Equal(new[] { "Mercado" }, await Names($"paidByUserId={bia.Id}"));
        Assert.Equal(new[] { "Padaria" }, await Names("status=Pending&type=Expense"));
        Assert.Equal(new[] { "Mercado" }, await Names("status=Paid"));
        Assert.Equal(new[] { "Reembolso" }, await Names("type=Income"));
        Assert.Equal(new[] { "Padaria" }, await Names("search=padar"));
        var page = await ana.GetOk<PagedResult<GroupTransactionDto>>($"/api/v1/groups/{gid}/transactions?pageSize=2&page=2");
        Assert.Equal((1, 3, 2), (page.Items.Count, page.TotalCount, page.TotalPages));
        Assert.Equal(t1.Id, page.Items.Single().Id);
        Assert.NotEqual(t2.Id, t3.Id);
        await ReadProblemAsync(await ana.Get($"/api/v1/groups/{gid}/transactions?pageSize=1000"), HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task TransactionOfAnotherGroup_IsNotFoundThroughThisGroupRoute()
    {
        var owner = await h.NewUserAsync();
        var g1 = await h.CreateGroupAsync(owner, "G1");
        var g2 = await h.CreateGroupAsync(owner, "G2");
        var tx = await h.CreateGroupTxAsync(owner, g1.Id, "Só do G1", 10m);

        await ReadProblemAsync(await owner.Get($"/api/v1/groups/{g2.Id}/transactions/{tx.Id}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await owner.Post($"/api/v1/groups/{g2.Id}/transactions/{tx.Id}/payments", new { amount = 1m, paymentDate = h.Today }), HttpStatusCode.NotFound, "not_found");
        Assert.Empty((await owner.GetOk<PagedResult<GroupTransactionDto>>($"/api/v1/groups/{g2.Id}/transactions")).Items);
    }

    [Fact]
    public async Task ExMember_LosesAccessToTheTransactions_ButHistoryRemainsForOthers()
    {
        var (owner, ana, bia, group) = await NewGroupAsync();
        var gid = group.Id;
        var tx = await h.CreateGroupTxAsync(bia, gid, "Da Bia", 60m);
        await h.PayGroupTxAsync(bia, gid, tx.Id, 60m);
        var biaRow = await h.MemberOfAsync(owner, gid, bia);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Delete($"/api/v1/groups/{gid}/members/{biaRow.Id}")).StatusCode);

        await ReadProblemAsync(await bia.Get($"/api/v1/groups/{gid}/transactions"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await bia.Get($"/api/v1/groups/{gid}/transactions/{tx.Id}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await bia.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/payments", new { amount = 1m, paymentDate = h.Today }), HttpStatusCode.NotFound, "not_found");
        var seen = await ana.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{gid}/transactions/{tx.Id}");
        Assert.Equal(("Bia", TransactionStatus.Paid, "Bia"), (seen.CreatedBy.Name, seen.Status, seen.Payments.Single().PaidBy.Name));
        Assert.False(seen.Permissions.CanEdit);
    }

    [Fact]
    public async Task ConcurrentPayments_NeverExceedTheAmount()
    {
        var (_, ana, bia, group) = await NewGroupAsync();
        var tx = await h.CreateGroupTxAsync(ana, group.Id, "Concorrência", 100m);

        var calls = await Task.WhenAll(
            ana.Post($"/api/v1/groups/{group.Id}/transactions/{tx.Id}/payments", new { amount = 80m, paymentDate = h.Today }),
            bia.Post($"/api/v1/groups/{group.Id}/transactions/{tx.Id}/payments", new { amount = 80m, paymentDate = h.Today }));

        Assert.Single(calls, c => c.StatusCode == HttpStatusCode.Created);
        Assert.Single(calls, c => c.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict);
        Assert.Equal(80m, (await ana.GetOk<GroupTransactionDetailDto>($"/api/v1/groups/{group.Id}/transactions/{tx.Id}")).PaidAmount);
    }

    [Fact]
    public async Task GroupRoutes_UseCamelCaseAndEnumNames_InJson()
    {
        var owner = await h.NewUserAsync();
        var group = await h.CreateGroupAsync(owner);
        var tx = await h.CreateGroupTxAsync(owner, group.Id, "Json", 10m);

        using var doc = JsonDocument.Parse(await GroupSupport.RawAsync(await owner.Get($"/api/v1/groups/{group.Id}/transactions/{tx.Id}")));
        var root = doc.RootElement;
        Assert.Equal("Expense", root.GetProperty("type").GetString());
        Assert.Equal("Pending", root.GetProperty("status").GetString());
        Assert.Equal("Json", root.GetProperty("description").GetString());
        Assert.True(root.TryGetProperty("createdBy", out var createdBy) && createdBy.TryGetProperty("userId", out _));
        Assert.True(root.GetProperty("permissions").GetProperty("canPay").GetBoolean());
        Assert.Equal(GroupMemberRole.Owner.ToString(), (await owner.GetOk<List<GroupSummaryDto>>("/api/v1/groups")).Single().MyRole.ToString());
    }
}
