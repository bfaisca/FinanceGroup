using FinanceApp.Application.Accounts;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa4;

/// <summary>Dashboard do grupo (D-034) com números calculados à mão.</summary>
public sealed class GroupDashboardServiceTests : IDisposable
{
    private readonly GroupTestKit kit = new();

    public void Dispose() => kit.Dispose();

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private async Task<(FinanceApp.Domain.Users.User Maria, FinanceApp.Domain.Users.User Ana, FinanceApp.Domain.Users.User Caio, Guid GroupId)> SeedAsync()
    {
        var maria = await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        var ana = await kit.NewUserAsync("Ana");
        await kit.JoinAsync(group.Id, maria, ana);
        var caio = await kit.NewUserAsync("Caio");
        await kit.JoinAsync(group.Id, maria, caio);

        var food = kit.ExpenseCategoryId("Alimentação");
        var leisure = kit.ExpenseCategoryId("Lazer");
        var salary = kit.IncomeCategoryId("Salário");

        // Hoje = 2026-11-18. Período do teste: 2026-11-01 a 2026-11-30.
        kit.As(maria);
        var t1 = await kit.CreateTxAsync(group.Id, "Mercado", 100m, D(2026, 11, 5), categoryId: food);
        await kit.PayAsync(group.Id, t1.Id, 30m);
        var t3 = await kit.CreateTxAsync(group.Id, "Cinema", 40m, D(2026, 11, 15), categoryId: leisure);
        Assert.Equal(TransactionStatus.Pending, t3.Status);
        var canceled = await kit.CreateTxAsync(group.Id, "Cancelada", 25m, D(2026, 11, 12), categoryId: food);
        await kit.PayAsync(group.Id, canceled.Id, 5m);
        await kit.Transactions.CancelAsync(group.Id, canceled.Id, CancellationToken.None);
        var oct = await kit.CreateTxAsync(group.Id, "Outubro", 80m, D(2026, 10, 20), categoryId: food);
        await kit.PayAsync(group.Id, oct.Id, 80m, D(2026, 10, 21));
        await kit.CreateTxAsync(group.Id, "Receita de outubro", 100m, D(2026, 10, 10), TransactionTypeArg.Income, salary);

        kit.As(ana);
        var t2 = await kit.CreateTxAsync(group.Id, "Padaria", 60m, D(2026, 11, 10), categoryId: food);
        await kit.PayAsync(group.Id, t2.Id, 60m);
        await kit.PayAsync(group.Id, t1.Id, 50m);
        var income = await kit.CreateTxAsync(group.Id, "Salário", 200m, D(2026, 11, 20), TransactionTypeArg.Income, salary);

        kit.As(caio);
        await kit.PayAsync(group.Id, income.Id, 150m);
        return (maria, ana, caio, group.Id);
    }

    private static GroupDashboardQuery November(int months = 6, string? endMonth = null) =>
        new() { From = D(2026, 11, 1), To = D(2026, 11, 30), Months = months, EndMonth = endMonth };

    [Fact]
    public async Task Totals_PaidAndPending_AreSeparate_AndCanceledAndOtherPeriodsAreIgnored()
    {
        var (_, ana, _, groupId) = await SeedAsync();
        kit.As(ana);

        var dashboard = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);

        // Despesas: 100 + 60 + 40 = 200 | pago: 30 + 50 + 60 = 140 | pendente: 60. Receita: 200 | recebido 150 | pendente 50.
        Assert.Equal(new GroupDashboardTotalsDto(200m, 140m, 60m, 200m, 150m, 50m, 0m), dashboard.Totals);
        Assert.Equal(D(2026, 11, 1), dashboard.Period.From);
        Assert.Equal(D(2026, 11, 30), dashboard.Period.To);
    }

    [Fact]
    public async Task CreatedBy_And_PaidBy_AreCalculatedSeparately()
    {
        var (maria, ana, caio, groupId) = await SeedAsync();
        kit.As(maria);

        var dashboard = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);

        // Quem CADASTROU: Maria 100+40 = 140 de despesa; Ana 60 de despesa e 200 de receita.
        Assert.Equal(new[] { ana.Id, maria.Id }, dashboard.CreatedBy.Select(i => i.UserId).ToArray());
        var anaCreated = dashboard.CreatedBy[0];
        Assert.Equal((60m, 30m, 200m, 100m), (anaCreated.ExpenseAmount, anaCreated.ExpensePercent, anaCreated.IncomeAmount, anaCreated.IncomePercent));
        var mariaCreated = dashboard.CreatedBy[1];
        Assert.Equal((140m, 70m, 0m, 0m), (mariaCreated.ExpenseAmount, mariaCreated.ExpensePercent, mariaCreated.IncomeAmount, mariaCreated.IncomePercent));

        // Quem PAGOU/RECEBEU (soma dos pagamentos): Caio recebeu 150; Ana pagou 50 + 60 = 110; Maria pagou 30 (o pendente não conta).
        Assert.Equal(new[] { caio.Id, ana.Id, maria.Id }, dashboard.PaidBy.Select(i => i.UserId).ToArray());
        var caioPaid = dashboard.PaidBy[0];
        Assert.Equal((0m, 0m, 150m, 100m), (caioPaid.ExpenseAmount, caioPaid.ExpensePercent, caioPaid.IncomeAmount, caioPaid.IncomePercent));
        var anaPaid = dashboard.PaidBy[1];
        Assert.Equal((110m, 78.57m), (anaPaid.ExpenseAmount, anaPaid.ExpensePercent));
        var mariaPaid = dashboard.PaidBy[2];
        Assert.Equal((30m, 21.43m), (mariaPaid.ExpenseAmount, mariaPaid.ExpensePercent));
        Assert.Equal(dashboard.Totals.ExpensesPaid, dashboard.PaidBy.Sum(i => i.ExpenseAmount));
        Assert.Equal(dashboard.Totals.IncomeReceived, dashboard.PaidBy.Sum(i => i.IncomeAmount));
        Assert.All(dashboard.PaidBy.Concat(dashboard.CreatedBy), i => Assert.Equal(GroupMemberStatus.Active, i.Status));
    }

    [Fact]
    public async Task OnlyPendingTransactions_ProduceNoPaidBy()
    {
        var maria = await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.CreateTxAsync(group.Id, "Conta de luz", 90m, D(2026, 11, 3));

        var dashboard = await kit.Dashboard.GetAsync(group.Id, November(), CancellationToken.None);

        Assert.Equal(90m, dashboard.Totals.Expenses);
        Assert.Equal(0m, dashboard.Totals.ExpensesPaid);
        Assert.Equal(90m, dashboard.Totals.ExpensesPending);
        Assert.Empty(dashboard.PaidBy);
        var created = Assert.Single(dashboard.CreatedBy);
        Assert.Equal(maria.Id, created.UserId);
        Assert.Equal(100m, created.ExpensePercent);
    }

    [Fact]
    public async Task ExpensesByCategory_GroupsByRoot_WithSubcategoryDetail()
    {
        var (maria, _, _, groupId) = await SeedAsync();
        kit.As(maria);
        var parent = kit.ExpenseCategoryId("Alimentação");
        var sub = FinanceApp.Domain.Categories.Category.CreateCustom(
            maria.Id, "Feira", FinanceApp.Domain.Categories.CategoryType.Expense,
            await kit.Db.Categories.FirstAsync(c => c.Id == parent), null, "#112233");
        kit.Db.Categories.Add(sub);
        await kit.Db.SaveChangesAsync();
        await kit.CreateTxAsync(groupId, "Feira de sábado", 40m, D(2026, 11, 14), categoryId: sub.Id);

        var dashboard = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);

        // Alimentação: 100 + 60 + 40 (subcategoria) = 200 de 240 (83,33%); Lazer 40 (16,67%).
        Assert.Equal(new[] { "Alimentação", "Lazer" }, dashboard.ExpensesByCategory.Select(c => c.Name).ToArray());
        var food = dashboard.ExpensesByCategory[0];
        Assert.Equal((200m, 83.33m), (food.Amount, food.Percent));
        var child = Assert.Single(food.Children);
        Assert.Equal(("Feira", 40m, 20m), (child.Name, child.Amount, child.Percent));
        Assert.Equal((40m, 16.67m), (dashboard.ExpensesByCategory[1].Amount, dashboard.ExpensesByCategory[1].Percent));
        Assert.Empty(dashboard.ExpensesByCategory[1].Children);
    }

    [Fact]
    public async Task Monthly_FillsEmptyMonths_AndEndsAtEndMonth()
    {
        var (maria, _, _, groupId) = await SeedAsync();
        kit.As(maria);

        var defaultEnd = await kit.Dashboard.GetAsync(groupId, November(months: 3), CancellationToken.None);
        Assert.Equal(new[] { "2026-09", "2026-10", "2026-11" }, defaultEnd.Monthly.Select(m => m.Month).ToArray());
        Assert.Equal((0m, 0m), (defaultEnd.Monthly[0].Income, defaultEnd.Monthly[0].Expenses));
        Assert.Equal((100m, 80m), (defaultEnd.Monthly[1].Income, defaultEnd.Monthly[1].Expenses));
        Assert.Equal((200m, 200m), (defaultEnd.Monthly[2].Income, defaultEnd.Monthly[2].Expenses));

        var custom = await kit.Dashboard.GetAsync(groupId, November(months: 2, endMonth: "2026-10"), CancellationToken.None);
        Assert.Equal(new[] { "2026-09", "2026-10" }, custom.Monthly.Select(m => m.Month).ToArray());

        var wide = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);
        Assert.Equal(6, wide.Monthly.Count);
        Assert.Equal("2026-06", wide.Monthly[0].Month);
    }

    [Fact]
    public async Task PreviousPeriod_ComparesWithTheSameDurationBefore()
    {
        var (maria, _, _, groupId) = await SeedAsync();
        kit.As(maria);

        var dashboard = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);

        // 01/11–30/11 (30 dias) → 02/10–31/10: despesas 80, receitas 100.
        var previous = dashboard.PreviousPeriod;
        Assert.Equal((D(2026, 10, 2), D(2026, 10, 31)), (previous.From, previous.To));
        Assert.Equal((80m, 100m), (previous.Expenses, previous.Income));
        Assert.Equal(150m, previous.ExpensesChangePercent);
        Assert.Equal(100m, previous.IncomeChangePercent);
    }

    [Fact]
    public async Task PreviousPeriod_WithoutBase_HasNullChange()
    {
        var maria = await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.CreateTxAsync(group.Id, "Mercado", 10m, D(2026, 11, 5));

        var dashboard = await kit.Dashboard.GetAsync(group.Id, November(), CancellationToken.None);

        Assert.Equal(0m, dashboard.PreviousPeriod.Expenses);
        Assert.Null(dashboard.PreviousPeriod.ExpensesChangePercent);
        Assert.Null(dashboard.PreviousPeriod.IncomeChangePercent);
        Assert.NotNull(maria);
    }

    [Fact]
    public async Task FormerMembers_StayInTheStatistics_WithTheirStatus_AndRemovedAccountsShowTheGenericName()
    {
        var (maria, ana, caio, groupId) = await SeedAsync();
        kit.As(caio);
        await kit.Groups.LeaveAsync(groupId, CancellationToken.None);
        var caioUser = await kit.Db.Users.SingleAsync(u => u.Id == caio.Id);
        caioUser.Anonymize(kit.Clock.UtcNow);
        await kit.Db.SaveChangesAsync();
        kit.As(maria);

        var dashboard = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);

        var received = dashboard.PaidBy.Single(i => i.UserId == caio.Id);
        Assert.Equal("Usuário removido", received.Name);
        Assert.Equal(GroupMemberStatus.Left, received.Status);
        Assert.Equal(150m, received.IncomeAmount);
        Assert.Equal(GroupMemberStatus.Active, dashboard.PaidBy.Single(i => i.UserId == ana.Id).Status);
    }

    [Fact]
    public async Task EmptyGroup_ReturnsZeros_AndEmptyLists()
    {
        await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);

        var dashboard = await kit.Dashboard.GetAsync(group.Id, November(), CancellationToken.None);

        Assert.Equal(new GroupDashboardTotalsDto(0m, 0m, 0m, 0m, 0m, 0m, 0m), dashboard.Totals);
        Assert.Empty(dashboard.CreatedBy);
        Assert.Empty(dashboard.PaidBy);
        Assert.Empty(dashboard.ExpensesByCategory);
        Assert.Equal(6, dashboard.Monthly.Count);
    }

    [Fact]
    public async Task InvalidPeriod_IsAValidationError()
    {
        await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);

        await Assert.ThrowsAsync<FinanceApp.Application.Common.Exceptions.ValidationException>(
            () => kit.Dashboard.GetAsync(group.Id, new GroupDashboardQuery { From = D(2026, 11, 30), To = D(2026, 11, 1) }, CancellationToken.None));
    }
}

/// <summary>D-033: pagamentos de grupo com conta própria entram no saldo projetado dessa conta (números calculados à mão).</summary>
public sealed class GroupPaymentProjectionTests
{
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid Ana = Guid.NewGuid();
    private static DateOnly D(int m, int d) => new(2026, m, d);

    [Fact]
    public async Task GroupPayments_WithTheUsersAccount_AffectOnlyThatAccount_OnThePaymentDate()
    {
        using var db = TestDb.Create();
        var account = NewAccount();
        var otherAccount = NewAccount("Outra");
        var category = ExpenseCategory();
        var incomeCategory = IncomeCategory();
        db.AddRange(account, otherAccount, category, incomeCategory);
        db.AccountBalanceSnapshots.Add(account.RecordBalance(1000m, D(6, 1), D(6, 1), Now));
        db.AccountBalanceSnapshots.Add(otherAccount.RecordBalance(500m, D(6, 1), D(6, 1), Now));
        var today = D(6, 30);

        Transaction GroupTx(string name, TransactionType type, decimal amount, DateOnly date) =>
            Transaction.CreateForGroup(GroupId, UserId, name, type, amount, type == TransactionType.Expense ? category : incomeCategory, null, TransactionStatus.Pending, date, null, null, Now);

        // Despesa de 100: eu paguei 60 em 05/06 com a conta; Ana pagou 40 (sem efeito sobre a minha conta).
        var rent = GroupTx("Aluguel", TransactionType.Expense, 100m, D(6, 3));
        rent.AddPayment(UserId, 60m, D(6, 5), today, account, null, Now);
        rent.AddPayment(Ana, 40m, D(6, 6), today, null, null, Now);
        // Paguei 50 na MESMA data do snapshot (01/06): estritamente posterior → não entra.
        var sameDay = GroupTx("Mesmo dia", TransactionType.Expense, 50m, D(5, 30));
        sameDay.AddPayment(UserId, 50m, D(6, 1), today, account, null, Now);
        // Recebi 120 de uma receita de 200 em 10/06 com a conta → soma.
        var refund = GroupTx("Reembolso", TransactionType.Income, 200m, D(6, 8));
        refund.AddPayment(UserId, 120m, D(6, 10), today, account, null, Now);
        // Pagamento SEM conta (10 em 07/06): só o grupo.
        var noAccount = GroupTx("Sem conta", TransactionType.Expense, 10m, D(6, 7));
        noAccount.AddPayment(UserId, 10m, D(6, 7), today, null, null, Now);
        // Pagamento em 20/06, mas a movimentação foi cancelada → fora (STATE_MACHINE.md).
        var canceled = GroupTx("Cancelada", TransactionType.Expense, 30m, D(6, 19));
        canceled.AddPayment(UserId, 30m, D(6, 20), today, account, null, Now);
        canceled.Cancel(UserId, Now);
        // Pagamento em 25/06 → só entra quando asOf ≥ 25/06.
        var late = GroupTx("Tarde", TransactionType.Expense, 70m, D(6, 24));
        late.AddPayment(UserId, 70m, D(6, 25), today, account, null, Now);
        // Pagamento da outra conta (500): não afeta a primeira.
        var elsewhere = GroupTx("Outra conta", TransactionType.Expense, 40m, D(6, 11));
        elsewhere.AddPayment(UserId, 40m, D(6, 12), today, otherAccount, null, Now);
        // Movimentação individual (despesa de 15 com vencimento 12/06 na conta): vale o valor TOTAL, como antes.
        var individual = Transaction.Create(UserId, "Individual", TransactionType.Expense, 15m, category, account, null, TransactionStatus.Pending, D(6, 12), D(6, 12), null, Now);
        db.Transactions.AddRange(rent, sameDay, refund, noAccount, canceled, late, elsewhere, individual);
        await db.SaveChangesAsync();
        var service = new BalanceProjectionService(db);

        var at0615 = await service.GetAsync([account.Id, otherAccount.Id], D(6, 15), CancellationToken.None);
        var at0630 = await service.GetAsync([account.Id, otherAccount.Id], D(6, 30), CancellationToken.None);
        var at0604 = await service.GetAsync([account.Id], D(6, 4), CancellationToken.None);

        // 06/15: 1000 − 60 (aluguel) + 120 (reembolso) − 15 (individual) = 1045.
        Assert.Equal(1045m, at0615[account.Id].ProjectedBalance);
        // 06/30: 1045 − 70 (tarde) = 975.
        Assert.Equal(975m, at0630[account.Id].ProjectedBalance);
        // Antes de qualquer pagamento da conta só vale o saldo informado.
        Assert.Equal(1000m, at0604[account.Id].ProjectedBalance);
        // Outra conta: 500 − 40 (pago em 12/06) = 460 desde 12/06.
        Assert.Equal(460m, at0615[otherAccount.Id].ProjectedBalance);
        Assert.Equal(460m, at0630[otherAccount.Id].ProjectedBalance);
    }

    [Fact]
    public async Task GroupPayments_DoNotCountTheTransactionAmountItself()
    {
        using var db = TestDb.Create();
        var account = NewAccount();
        var category = ExpenseCategory();
        db.AddRange(account, category);
        db.AccountBalanceSnapshots.Add(account.RecordBalance(1000m, D(6, 1), D(6, 1), Now));
        var t = Transaction.CreateForGroup(GroupId, UserId, "Grupo", TransactionType.Expense, 300m, category, null, TransactionStatus.Pending, D(6, 5), D(6, 8), null, Now);
        db.Transactions.Add(t);
        await db.SaveChangesAsync();
        var service = new BalanceProjectionService(db);

        // Sem pagamento algum, a despesa de grupo (300) não altera o saldo, mesmo com vencimento no período.
        var before = await service.GetAsync([account.Id], D(6, 30), CancellationToken.None);
        Assert.Equal(1000m, before[account.Id].ProjectedBalance);

        t.AddPayment(UserId, 120m, D(6, 9), D(6, 30), account, null, Now);
        await db.SaveChangesAsync();
        var after = await service.GetAsync([account.Id], D(6, 30), CancellationToken.None);
        Assert.Equal(880m, after[account.Id].ProjectedBalance);
    }
}
