using System.Net;
using System.Text.Json;
using FinanceApp.Application.Accounts;
using FinanceApp.Application.Common;
using FinanceApp.Application.Groups;
using FinanceApp.Application.Notifications;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>
/// Etapa 4c (D-046 a D-049, contrato §16.2/§16.3/§17) sobre a API real com PostgreSQL: só despesas, lançar já paga, vencimento livre,
/// juros/multa, edição do próprio pagamento, recorrência com N ocorrências e dashboard "Pago × Pendente". "Hoje" = 2026-11-18.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class GroupAdjustmentsApiTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 11, 18);
    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public GroupAdjustmentsApiTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private async Task<(ApiUser Brendo, ApiUser Ana, ApiUser Carla, Guid GroupId)> NewGroupAsync()
    {
        var brendo = await h.NewUserAsync("Brendo");
        var ana = await h.NewUserAsync("Ana");
        var carla = await h.NewUserAsync("Carla");
        var group = await h.CreateGroupAsync(brendo, "Casa");
        await h.JoinAsync(brendo, group.Id, ana);
        await h.JoinAsync(brendo, group.Id, carla);
        return (brendo, ana, carla, group.Id);
    }

    private static async Task<List<NotificationDto>> EventsAsync(ApiUser user) =>
        (await user.GetOk<PagedResult<NotificationDto>>("/api/v1/notifications?pageSize=50")).Items
        .Where(n => n.Type != NotificationType.GroupInvitationReceived)
        .ToList();

    [Fact]
    public async Task FullFlow_RecurrentExpenseWithFirstPayment_InterestEditingDashboardAndProjection()
    {
        var (brendo, ana, carla, gid) = await NewGroupAsync();
        var anaAccount = await h.CreateAccountAsync(ana, "Corrente da Ana", balance: 1000m, referenceDate: D(2026, 11, 1));
        var housing = await h.CategoryIdAsync(brendo, "Moradia");

        // Brendo lança "Aluguel" 3x mensal, já paga a 1ª (500 + 12 de juros) e com vencimento ANTERIOR à data da movimentação.
        var first = await h.CreateGroupTxAsync(
            brendo, gid, "Aluguel", 500m, D(2026, 11, 5), housing, due: D(2026, 11, 1),
            payment: new { paymentDate = D(2026, 11, 10), interestAmount = 12m },
            recurrence: new { frequency = "Monthly", occurrences = 3 });

        Assert.Equal((TransactionStatus.Paid, 500m, 500m, 12m, 512m, 0m), (first.Status, first.Amount, first.PaidAmount, first.InterestPaid, first.TotalPaid, first.RemainingAmount));
        Assert.Equal((1, 3), (first.Series!.Number, first.Series.Total));
        var firstPayment = Assert.Single(first.Payments);
        Assert.Equal((brendo.Id, 500m, 12m, 512m, D(2026, 11, 10)), (firstPayment.PaidBy.UserId, firstPayment.Amount, firstPayment.InterestAmount, firstPayment.TotalAmount, firstPayment.PaymentDate));

        // Ocorrências: datas D+(i-1)·mês e vencimento deslocado com o mesmo deslocamento em dias (-4); o pagamento só vale para a 1ª.
        var items = await h.ListGroupTxAsync(brendo, gid);
        Assert.Equal(new[] { D(2026, 11, 5), D(2026, 12, 5), D(2027, 1, 5) }, items.Select(t => t.TransactionDate).ToArray());
        Assert.Equal(new DateOnly?[] { D(2026, 11, 1), D(2026, 12, 1), D(2027, 1, 1) }, items.Select(t => t.DueDate).ToArray());
        Assert.Equal(new[] { TransactionStatus.Paid, TransactionStatus.Pending, TransactionStatus.Pending }, items.Select(t => t.Status).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, items.Select(t => t.Series!.Number).ToArray());
        Assert.All(items, t => Assert.Equal((first.Series.Id, 3, 500m, brendo.Id), (t.Series!.Id, t.Series.Total, t.Amount, t.CreatedBy.UserId)));
        Assert.Equal(1, await h.WithDbAsync(db => db.TransactionPayments.CountAsync(p => items.Select(i => i.Id).Contains(p.TransactionId))));

        // Ana e Carla: UMA notificação de criação da série + a do pagamento (valor total 512). Brendo nenhuma.
        foreach (var viewer in new[] { ana, carla })
        {
            var events = await EventsAsync(viewer);
            Assert.Equal(2, events.Count);
            var created = Assert.Single(events, n => n.Type == NotificationType.GroupTransactionCreated);
            Assert.Equal((500m, 3, "Aluguel"), (created.Transaction!.Amount, created.Transaction.SeriesOccurrences, created.Transaction.Description));
            var paid = Assert.Single(events, n => n.Type == NotificationType.GroupPaymentRegistered);
            Assert.Equal((512m, (int?)null, "Brendo"), (paid.Transaction!.Amount, paid.Transaction.SeriesOccurrences, paid.Actor!.Name));
        }

        Assert.Empty(await EventsAsync(brendo));

        // Ana paga a 2ª ocorrência (500) com 23,50 de juros, usando a conta dela: valor original 500, totalPaid 523,50, Paid.
        var second = items[1];
        var afterAna = await h.PayGroupTxAsync(ana, gid, second.Id, 500m, D(2026, 11, 17), anaAccount.Id, interest: 23.50m);
        Assert.Equal((TransactionStatus.Paid, 500m, 500m, 23.50m, 523.50m, 0m), (afterAna.Status, afterAna.Amount, afterAna.PaidAmount, afterAna.InterestPaid, afterAna.TotalPaid, afterAna.RemainingAmount));
        var anaPayment = Assert.Single(afterAna.Payments);
        Assert.True(anaPayment.CanEdit);
        // Notificação do pagamento com juros = valor total.
        var anaPaymentNotification = (await EventsAsync(brendo)).Single(n => n.Type == NotificationType.GroupPaymentRegistered);
        Assert.Equal(523.50m, anaPaymentNotification.Transaction!.Amount);
        // Projeção individual (D-033/D-048): 1000 − (500 + 23,50) = 476,50.
        Assert.Equal(476.50m, (await ana.GetOk<List<AccountDto>>("/api/v1/accounts")).Single().ProjectedBalance);

        // Edição do próprio pagamento: juros 30; só o pagador (403 aos demais, inclusive Brendo, que é o dono do grupo).
        var url = $"/api/v1/groups/{gid}/transactions/{second.Id}/payments/{anaPayment.Id}";
        var edited = await ApiUser.Read<GroupTransactionDetailDto>(await ana.Put(url, new { amount = 500m, interestAmount = 30m, paymentDate = D(2026, 11, 17), accountId = anaAccount.Id }));
        Assert.Equal((TransactionStatus.Paid, 500m, 30m, 530m), (edited.Status, edited.PaidAmount, edited.InterestPaid, edited.TotalPaid));
        Assert.True(edited.Payments[0].UpdatedAt >= edited.Payments[0].CreatedAt);
        Assert.Equal(470m, (await ana.GetOk<List<AccountDto>>("/api/v1/accounts")).Single().ProjectedBalance);
        var history = await ana.GetOk<List<GroupTransactionHistoryEntryDto>>($"/api/v1/groups/{gid}/transactions/{second.Id}/history");
        var change = Assert.Single(history, e => e.Action == TransactionAuditAction.PaymentUpdated);
        Assert.Equal(("Ana", "500.00 + 23.50 juros (2026-11-17)", "500.00 + 30.00 juros (2026-11-17)"), (change.ActorName, change.OldValue, change.NewValue));
        // Editar sem accountId MANTÉM a conta privada (a API nunca a devolve ao cliente); clearAccount desvincula.
        await ana.Put(url, new { amount = 500m, interestAmount = 40m, paymentDate = D(2026, 11, 17) });
        Assert.Equal(460m, (await ana.GetOk<List<AccountDto>>("/api/v1/accounts")).Single().ProjectedBalance);
        await ana.Put(url, new { amount = 500m, interestAmount = 40m, paymentDate = D(2026, 11, 17), clearAccount = true });
        Assert.Equal(1000m, (await ana.GetOk<List<AccountDto>>("/api/v1/accounts")).Single().ProjectedBalance);
        await ana.Put(url, new { amount = 500m, interestAmount = 30m, paymentDate = D(2026, 11, 17), accountId = anaAccount.Id });
        Assert.Equal(470m, (await ana.GetOk<List<AccountDto>>("/api/v1/accounts")).Single().ProjectedBalance);
        var body = new { amount = 1m, paymentDate = D(2026, 11, 17) };
        await ReadProblemAsync(await carla.Put(url, body), HttpStatusCode.Forbidden, "forbidden");
        await ReadProblemAsync(await brendo.Put(url, body), HttpStatusCode.Forbidden, "forbidden");
        var outsider = await h.NewUserAsync("Fora");
        await ReadProblemAsync(await outsider.Put(url, body), HttpStatusCode.NotFound, "not_found");
        // Reduzir o principal reabre a conta (Pending); somar acima do valor → 422.
        var reduced = await ApiUser.Read<GroupTransactionDetailDto>(await ana.Put(url, new { amount = 200m, interestAmount = 0m, paymentDate = D(2026, 11, 17) }));
        Assert.Equal((TransactionStatus.Pending, 200m, 300m, (DateOnly?)null), (reduced.Status, reduced.PaidAmount, reduced.RemainingAmount, reduced.PaidAt));
        await ReadProblemAsync(await ana.Put(url, new { amount = 500.01m, paymentDate = D(2026, 11, 17) }), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await ana.Put(url, new { amount = 500m, interestAmount = 30m, paymentDate = D(2026, 11, 17), accountId = anaAccount.Id });

        // Outra despesa do mês: "Luz" 200 (Brendo), com Carla pagando 50 + 3 de juros → pendente de verdade.
        var light = await h.CreateGroupTxAsync(brendo, gid, "Luz", 200m, D(2026, 11, 8), await h.CategoryIdAsync(brendo, "Moradia"));
        await h.PayGroupTxAsync(carla, gid, light.Id, 50m, D(2026, 11, 9), interest: 3m);

        // Dashboard 01/11–31/12: originais 500 + 500 + 200 = 1200 | principal 500 + 500 + 50 = 1050 | juros 12 + 30 + 3 = 45 | pago 1095 | pendente 150.
        var raw = await GroupSupport.RawAsync(await brendo.Get($"/api/v1/groups/{gid}/dashboard?from=2026-11-01&to=2026-12-31"));
        GroupSupport.AssertNoPrivateData(raw, brendo.Session.Email, ana.Session.Email, carla.Session.Email);
        Assert.DoesNotContain(anaAccount.Id.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        var dashboard = JsonSerializer.Deserialize<GroupDashboardDto>(raw, ApiUser.Json)!;
        Assert.Equal(new GroupDashboardTotalsDto(1200m, 1050m, 45m, 1095m, 150m), dashboard.Totals);
        // Pago por quem (1095): Ana 530 (48,40%), Brendo 512 (46,76%), Carla 53 (4,84%).
        Assert.Equal(new[] { "Ana", "Brendo", "Carla" }, dashboard.PaidBy.Select(p => p.Name).ToArray());
        Assert.Equal((530m, 30m, 48.40m), (dashboard.PaidBy[0].PaidAmount, dashboard.PaidBy[0].InterestAmount, dashboard.PaidBy[0].Percent));
        Assert.Equal((512m, 12m, 46.76m), (dashboard.PaidBy[1].PaidAmount, dashboard.PaidBy[1].InterestAmount, dashboard.PaidBy[1].Percent));
        Assert.Equal((53m, 3m, 4.84m), (dashboard.PaidBy[2].PaidAmount, dashboard.PaidBy[2].InterestAmount, dashboard.PaidBy[2].Percent));
        Assert.Equal(1095m, dashboard.PaidBy.Sum(p => p.PaidAmount));
        var november = dashboard.Monthly.Single(m => m.Month == "2026-11");
        var december = dashboard.Monthly.Single(m => m.Month == "2026-12");
        Assert.Equal((700m, 565m), (november.Expenses, november.Paid));
        Assert.Equal((500m, 530m), (december.Expenses, december.Paid));

        // Cada ocorrência é independente: editar o valor, cancelar e excluir (sem pagamentos).
        var third = items[2];
        var edit = await ApiUser.Read<GroupTransactionDetailDto>(await brendo.Put(
            $"/api/v1/groups/{gid}/transactions/{third.Id}", GroupSupport.GroupTxBody("Aluguel (reajuste)", 550m, housing, third.TransactionDate, third.DueDate)));
        Assert.Equal((550m, 3), (edit.Amount, edit.Series!.Total));
        Assert.Equal(TransactionStatus.Canceled, (await ApiUser.Read<GroupTransactionDetailDto>(await brendo.Post($"/api/v1/groups/{gid}/transactions/{third.Id}/cancel"))).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await brendo.Delete($"/api/v1/groups/{gid}/transactions/{third.Id}")).StatusCode);
        var remaining = await h.ListGroupTxAsync(brendo, gid, "search=Aluguel");
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, t => Assert.Equal(3, t.Series!.Total));
    }

    [Fact]
    public async Task Create_Income_Is422_AndTheDatabaseConstraintRejectsAGroupIncomeRow()
    {
        var (brendo, _, _, gid) = await NewGroupAsync();
        var salary = await h.CategoryIdAsync(brendo, "Salário", FinanceApp.Domain.Categories.CategoryType.Income);
        var before = (await h.ListGroupTxAsync(brendo, gid)).Count;

        var rejected = await brendo.Post($"/api/v1/groups/{gid}/transactions", new { description = "Salário", type = "Income", amount = 10m, categoryId = salary, transactionDate = FixedToday });
        await ReadProblemAsync(rejected, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Equal(before, (await h.ListGroupTxAsync(brendo, gid)).Count);

        // Defesa em profundidade: mesmo por SQL direto, o banco não aceita receita num grupo.
        var income = await h.CreateTxAsync(brendo, "Receita individual", "Income", 10m, salary, FixedToday);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => h.WithDbAsync(async db =>
            await db.Database.ExecuteSqlRawAsync("UPDATE transactions SET group_id = {0} WHERE id = {1}", gid, income.Id)));
        Assert.Equal("ck_transactions_group_rules", ex.ConstraintName);
    }

    [Fact]
    public async Task Create_WithPayment_Atomicity_Validation_AndPartialPayment()
    {
        var (brendo, ana, _, gid) = await NewGroupAsync();
        var housing = await h.CategoryIdAsync(brendo, "Moradia");
        var transactionsBefore = await h.WithDbAsync(db => db.Transactions.CountAsync(t => t.GroupId == gid));
        var seriesBefore = await h.WithDbAsync(db => db.GroupTransactionSeries.CountAsync(s => s.GroupId == gid));
        var url = $"/api/v1/groups/{gid}/transactions";
        object Body(object? payment = null, object? recurrence = null) =>
            GroupSupport.GroupTxBody("Atômica", 100m, housing, FixedToday, payment: payment, recurrence: recurrence);

        // Pagamento acima do valor → 422 e NADA é gravado (nem as outras ocorrências, nem a série).
        await ReadProblemAsync(
            await brendo.Post(url, Body(new { amount = 100.01m, paymentDate = FixedToday }, new { frequency = "Weekly", occurrences = 4 })),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Equal((transactionsBefore, seriesBefore), (
            await h.WithDbAsync(db => db.Transactions.CountAsync(t => t.GroupId == gid)),
            await h.WithDbAsync(db => db.GroupTransactionSeries.CountAsync(s => s.GroupId == gid))));
        var notificationsOfAna = (await EventsAsync(ana)).Count;
        Assert.Equal(0, notificationsOfAna);

        // Validações (400): data futura, juros negativo, frequência Daily/Custom, ocorrências fora de 2–60.
        var future = await ReadProblemAsync(await brendo.Post(url, Body(new { paymentDate = FixedToday.AddDays(1) })), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(future.HasError("payment.paymentDate"));
        var negative = await ReadProblemAsync(await brendo.Post(url, Body(new { paymentDate = FixedToday, interestAmount = -1m })), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(negative.HasError("payment.interestAmount"));
        foreach (var frequency in new[] { "Daily", "Custom" })
        {
            var invalid = await ReadProblemAsync(await brendo.Post(url, Body(recurrence: new { frequency, occurrences = 3 })), HttpStatusCode.BadRequest, "validation_error");
            Assert.True(invalid.HasError("recurrence.frequency"));
        }

        foreach (var occurrences in new[] { 1, 61 })
        {
            var invalid = await ReadProblemAsync(await brendo.Post(url, Body(recurrence: new { frequency = "Monthly", occurrences })), HttpStatusCode.BadRequest, "validation_error");
            Assert.True(invalid.HasError("recurrence.occurrences"));
        }

        // Pagamento parcial na criação: a conta nasce Pending e o restante continua em aberto.
        var partial = await ApiUser.Read<GroupTransactionDetailDto>(await brendo.Post(url, Body(new { amount = 40m, paymentDate = FixedToday.AddDays(-30) })), HttpStatusCode.Created);
        Assert.Equal((TransactionStatus.Pending, 40m, 60m), (partial.Status, partial.PaidAmount, partial.RemainingAmount));

        // Vencimento no passado e anterior à data da movimentação é aceito (e a conta nasce atrasada).
        var overdue = await h.CreateGroupTxAsync(brendo, gid, "Atrasada", 10m, FixedToday, housing, due: FixedToday.AddDays(-40));
        Assert.Equal((TransactionStatus.Overdue, (DateOnly?)FixedToday.AddDays(-40)), (overdue.Status, overdue.DueDate));
        // Edição também aceita vencimento livre.
        var edited = await ApiUser.Read<GroupTransactionDetailDto>(await brendo.Put(
            $"/api/v1/groups/{gid}/transactions/{overdue.Id}", GroupSupport.GroupTxBody("Atrasada", 10m, housing, FixedToday, FixedToday.AddDays(-90))));
        Assert.Equal(FixedToday.AddDays(-90), edited.DueDate);
    }

    [Fact]
    public async Task Recurrence_AllFrequencies_FollowTheEndOfMonthRule_AndJsonHasNoPrivateData()
    {
        var (brendo, ana, _, gid) = await NewGroupAsync();
        var housing = await h.CategoryIdAsync(brendo, "Moradia");
        var anaAccount = await h.CreateAccountAsync(ana, "Privada", balance: 10m, referenceDate: D(2026, 11, 1));
        var cases = new (string Frequency, string[] Expected)[]
        {
            ("Weekly", ["2026-01-31", "2026-02-07", "2026-02-14"]),
            ("Biweekly", ["2026-01-31", "2026-02-14", "2026-02-28"]),
            ("Monthly", ["2026-01-31", "2026-02-28", "2026-03-31"]),
            ("Bimonthly", ["2026-01-31", "2026-03-31", "2026-05-31"]),
            ("Quarterly", ["2026-01-31", "2026-04-30", "2026-07-31"]),
            ("Semiannual", ["2026-01-31", "2026-07-31", "2027-01-31"]),
            ("Annual", ["2026-01-31", "2027-01-31", "2028-01-31"]),
        };

        foreach (var (frequency, expected) in cases)
        {
            var response = await brendo.Post(
                $"/api/v1/groups/{gid}/transactions",
                GroupSupport.GroupTxBody($"Série {frequency}", 10m, housing, D(2026, 1, 31), recurrence: new { frequency, occurrences = 3 }));
            var raw = await GroupSupport.RawAsync(response, HttpStatusCode.Created);
            GroupSupport.AssertNoPrivateData(raw, anaAccount.Id.ToString());
            var created = JsonSerializer.Deserialize<GroupTransactionDetailDto>(raw, ApiUser.Json)!;

            var items = await h.ListGroupTxAsync(brendo, gid, $"search={Uri.EscapeDataString($"Série {frequency}")}");
            Assert.Equal(expected, items.Select(t => t.TransactionDate.ToString("yyyy-MM-dd")).ToArray());
            Assert.Equal(created.Series!.Id, items[0].Series!.Id);
        }
    }

    [Fact]
    public async Task PaymentWithAccount_OfAnotherUser_OrOnCanceledTransaction_OrArchivedGroup_IsRejected()
    {
        var (brendo, ana, _, gid) = await NewGroupAsync();
        var brendoAccount = await h.CreateAccountAsync(brendo, "Do Brendo");
        var tx = await h.CreateGroupTxAsync(brendo, gid, "Conta", 100m);
        var paid = await h.PayGroupTxAsync(ana, gid, tx.Id, 40m);
        var url = $"/api/v1/groups/{gid}/transactions/{tx.Id}/payments/{paid.Payments.Single().Id}";

        // Conta de outro usuário na edição → 400/404/422 (nunca aceita).
        var foreign = await ana.Put(url, new { amount = 40m, paymentDate = FixedToday, accountId = brendoAccount.Id });
        Assert.True(foreign.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity, $"Status {(int)foreign.StatusCode}");

        await brendo.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/cancel");
        await ReadProblemAsync(await ana.Put(url, new { amount = 50m, paymentDate = FixedToday }), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await brendo.Post($"/api/v1/groups/{gid}/transactions/{tx.Id}/reopen");
        Assert.Equal(HttpStatusCode.OK, (await ana.Put(url, new { amount = 50m, paymentDate = FixedToday })).StatusCode);

        await brendo.Post($"/api/v1/groups/{gid}/archive");
        await ReadProblemAsync(await ana.Put(url, new { amount = 60m, paymentDate = FixedToday }), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }
}
