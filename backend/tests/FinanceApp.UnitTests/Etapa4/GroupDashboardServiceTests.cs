using FinanceApp.Application.Accounts;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa4;

/// <summary>Dashboard do grupo (D-034 emendada por D-046/D-048) com números calculados à mão.</summary>
public sealed class GroupDashboardServiceTests : IDisposable
{
    private readonly GroupTestKit kit = new();

    public void Dispose() => kit.Dispose();

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private async Task Pay(Guid groupId, Guid txId, decimal amount, decimal interest, DateOnly? date = null) =>
        await kit.PayAsync(groupId, txId, amount, date, interest);

    /// <summary>
    /// Hoje = 2026-11-18; período 01/11–30/11. Novembro: Mercado 100 (Maria paga 30; Ana paga 50 + 2,50 de juros), Padaria 60 (Ana paga
    /// 60 + 6 de juros), Cinema 40 (Caio paga 10 + 0,50 de juros). Fora: cancelada de 25 (com pagamento de 5) e Outubro 80 (pago 80 + 4 de juros).
    /// </summary>
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

        kit.As(maria);
        var t1 = await kit.CreateTxAsync(group.Id, "Mercado", 100m, D(2026, 11, 5), categoryId: food);
        await Pay(group.Id, t1.Id, 30m, 0m);
        var t3 = await kit.CreateTxAsync(group.Id, "Cinema", 40m, D(2026, 11, 15), categoryId: leisure);
        Assert.Equal(TransactionStatus.Pending, t3.Status);
        var canceled = await kit.CreateTxAsync(group.Id, "Cancelada", 25m, D(2026, 11, 12), categoryId: food);
        await Pay(group.Id, canceled.Id, 5m, 1m);
        await kit.Transactions.CancelAsync(group.Id, canceled.Id, CancellationToken.None);
        var oct = await kit.CreateTxAsync(group.Id, "Outubro", 80m, D(2026, 10, 20), categoryId: food);
        await Pay(group.Id, oct.Id, 80m, 4m, D(2026, 10, 21));

        kit.As(ana);
        var t2 = await kit.CreateTxAsync(group.Id, "Padaria", 60m, D(2026, 11, 10), categoryId: food);
        await Pay(group.Id, t2.Id, 60m, 6m);
        await Pay(group.Id, t1.Id, 50m, 2.50m);

        kit.As(caio);
        await Pay(group.Id, t3.Id, 10m, 0.50m);
        return (maria, ana, caio, group.Id);
    }

    private static GroupDashboardQuery November(int months = 6, string? endMonth = null) =>
        new() { From = D(2026, 11, 1), To = D(2026, 11, 30), Months = months, EndMonth = endMonth };

    [Fact]
    public async Task Totals_PaidIsPrincipalPlusInterest_PendingIsOnlyPrincipal_CanceledAndOtherPeriodsAreIgnored()
    {
        var (_, ana, _, groupId) = await SeedAsync();
        kit.As(ana);

        var dashboard = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);

        // Originais 100 + 60 + 40 = 200 | principal pago 30 + 50 + 60 + 10 = 150 | juros 2,50 + 6 + 0,50 = 9 | pago 159 | pendente 200 − 150 = 50.
        Assert.Equal(new GroupDashboardTotalsDto(200m, 150m, 9m, 159m, 50m), dashboard.Totals);
        Assert.Equal(D(2026, 11, 1), dashboard.Period.From);
        Assert.Equal(D(2026, 11, 30), dashboard.Period.To);
    }

    [Fact]
    public async Task PaidBy_SumsPrincipalAndInterestPerPayer_WithSeparatedInterestAndPercentOverThePaidTotal()
    {
        var (maria, ana, caio, groupId) = await SeedAsync();
        kit.As(maria);

        var dashboard = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);

        // Ana: 50 + 2,50 + 60 + 6 = 118,50 (juros 8,50); Maria: 30; Caio: 10 + 0,50 = 10,50 (juros 0,50). Total 159.
        Assert.Equal(new[] { ana.Id, maria.Id, caio.Id }, dashboard.PaidBy.Select(i => i.UserId).ToArray());
        Assert.Equal((118.50m, 8.50m, 74.53m), (dashboard.PaidBy[0].PaidAmount, dashboard.PaidBy[0].InterestAmount, dashboard.PaidBy[0].Percent));
        Assert.Equal((30m, 0m, 18.87m), (dashboard.PaidBy[1].PaidAmount, dashboard.PaidBy[1].InterestAmount, dashboard.PaidBy[1].Percent));
        Assert.Equal((10.50m, 0.50m, 6.60m), (dashboard.PaidBy[2].PaidAmount, dashboard.PaidBy[2].InterestAmount, dashboard.PaidBy[2].Percent));
        Assert.Equal(dashboard.Totals.ExpensesPaid, dashboard.PaidBy.Sum(i => i.PaidAmount));
        Assert.Equal(dashboard.Totals.InterestPaid, dashboard.PaidBy.Sum(i => i.InterestAmount));
        Assert.All(dashboard.PaidBy, i => Assert.Equal(GroupMemberStatus.Active, i.Status));
    }

    [Fact]
    public async Task OnlyPendingTransactions_ProduceNoPaidBy()
    {
        await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.CreateTxAsync(group.Id, "Conta de luz", 90m, D(2026, 11, 3));

        var dashboard = await kit.Dashboard.GetAsync(group.Id, November(), CancellationToken.None);

        Assert.Equal(new GroupDashboardTotalsDto(90m, 0m, 0m, 0m, 90m), dashboard.Totals);
        Assert.Empty(dashboard.PaidBy);
    }

    [Fact]
    public async Task InterestAlone_NeverReducesThePending_AndPaidCanExceedTheOriginal()
    {
        await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        var tx = await kit.CreateTxAsync(group.Id, "Aluguel", 500m, D(2026, 11, 3));
        await Pay(group.Id, tx.Id, 500m, 23.50m);

        var dashboard = await kit.Dashboard.GetAsync(group.Id, November(), CancellationToken.None);

        Assert.Equal(new GroupDashboardTotalsDto(500m, 500m, 23.50m, 523.50m, 0m), dashboard.Totals);
        Assert.Equal(100m, Assert.Single(dashboard.PaidBy).Percent);
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
    public async Task Monthly_ShowsOriginalAndEffectivelyPaid_FillsEmptyMonths_AndEndsAtEndMonth()
    {
        var (maria, _, _, groupId) = await SeedAsync();
        kit.As(maria);

        var defaultEnd = await kit.Dashboard.GetAsync(groupId, November(months: 3), CancellationToken.None);

        Assert.Equal(new[] { "2026-09", "2026-10", "2026-11" }, defaultEnd.Monthly.Select(m => m.Month).ToArray());
        Assert.Equal((0m, 0m), (defaultEnd.Monthly[0].Expenses, defaultEnd.Monthly[0].Paid));
        Assert.Equal((80m, 84m), (defaultEnd.Monthly[1].Expenses, defaultEnd.Monthly[1].Paid));
        Assert.Equal((200m, 159m), (defaultEnd.Monthly[2].Expenses, defaultEnd.Monthly[2].Paid));

        var custom = await kit.Dashboard.GetAsync(groupId, November(months: 2, endMonth: "2026-10"), CancellationToken.None);
        Assert.Equal(new[] { "2026-09", "2026-10" }, custom.Monthly.Select(m => m.Month).ToArray());

        var wide = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);
        Assert.Equal(6, wide.Monthly.Count);
        Assert.Equal("2026-06", wide.Monthly[0].Month);
    }

    [Fact]
    public async Task PreviousPeriod_ComparesTheOriginalExpensesWithTheSameDurationBefore()
    {
        var (maria, _, _, groupId) = await SeedAsync();
        kit.As(maria);

        var dashboard = await kit.Dashboard.GetAsync(groupId, November(), CancellationToken.None);

        // 01/11–30/11 (30 dias) → 02/10–31/10: despesas 80 → (200 − 80) / 80 = +150%.
        var previous = dashboard.PreviousPeriod;
        Assert.Equal((D(2026, 10, 2), D(2026, 10, 31)), (previous.From, previous.To));
        Assert.Equal(80m, previous.Expenses);
        Assert.Equal(150m, previous.ExpensesChangePercent);
    }

    [Fact]
    public async Task PreviousPeriod_WithoutBase_HasNullChange()
    {
        await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.CreateTxAsync(group.Id, "Mercado", 10m, D(2026, 11, 5));

        var dashboard = await kit.Dashboard.GetAsync(group.Id, November(), CancellationToken.None);

        Assert.Equal(0m, dashboard.PreviousPeriod.Expenses);
        Assert.Null(dashboard.PreviousPeriod.ExpensesChangePercent);
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

        var paid = dashboard.PaidBy.Single(i => i.UserId == caio.Id);
        Assert.Equal("Usuário removido", paid.Name);
        Assert.Equal(GroupMemberStatus.Left, paid.Status);
        Assert.Equal(10.50m, paid.PaidAmount);
        Assert.Equal(GroupMemberStatus.Active, dashboard.PaidBy.Single(i => i.UserId == ana.Id).Status);
    }

    [Fact]
    public async Task EmptyGroup_ReturnsZeros_AndEmptyLists()
    {
        await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);

        var dashboard = await kit.Dashboard.GetAsync(group.Id, November(), CancellationToken.None);

        Assert.Equal(new GroupDashboardTotalsDto(0m, 0m, 0m, 0m, 0m), dashboard.Totals);
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

/// <summary>D-033/D-048: pagamentos de despesas de grupo com conta própria subtraem do saldo projetado dessa conta, principal + juros (números calculados à mão).</summary>
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
        db.AddRange(account, otherAccount, category);
        db.AccountBalanceSnapshots.Add(account.RecordBalance(1000m, D(6, 1), D(6, 1), Now));
        db.AccountBalanceSnapshots.Add(otherAccount.RecordBalance(500m, D(6, 1), D(6, 1), Now));
        var today = D(6, 30);

        Transaction GroupTx(string name, decimal amount, DateOnly date) =>
            Transaction.CreateForGroup(GroupId, UserId, name, amount, category, null, TransactionStatus.Pending, date, null, null, Now);

        // Despesa de 100: eu paguei 60 em 05/06 com a conta; Ana pagou 40 (sem efeito sobre a minha conta).
        var rent = GroupTx("Aluguel", 100m, D(6, 3));
        rent.AddPayment(UserId, 60m, D(6, 5), today, account, null, Now);
        rent.AddPayment(Ana, 40m, D(6, 6), today, null, null, Now);
        // Paguei 50 na MESMA data do snapshot (01/06): estritamente posterior → não entra.
        var sameDay = GroupTx("Mesmo dia", 50m, D(5, 30));
        sameDay.AddPayment(UserId, 50m, D(6, 1), today, account, null, Now);
        // Juros/multa (D-048): paguei 80 de principal + 5 de juros em 10/06 com a conta → subtrai 85.
        var withInterest = GroupTx("Com juros", 200m, D(6, 8));
        withInterest.AddPayment(UserId, 80m, D(6, 10), today, account, null, Now, interestAmount: 5m);
        // Pagamento SEM conta (10 em 07/06): só o grupo.
        var noAccount = GroupTx("Sem conta", 10m, D(6, 7));
        noAccount.AddPayment(UserId, 10m, D(6, 7), today, null, null, Now);
        // Pagamento em 20/06, mas a movimentação foi cancelada → fora (STATE_MACHINE.md).
        var canceled = GroupTx("Cancelada", 30m, D(6, 19));
        canceled.AddPayment(UserId, 30m, D(6, 20), today, account, null, Now);
        canceled.Cancel(UserId, Now);
        // Pagamento em 25/06 → só entra quando asOf ≥ 25/06.
        var late = GroupTx("Tarde", 70m, D(6, 24));
        late.AddPayment(UserId, 70m, D(6, 25), today, account, null, Now);
        // Pagamento da outra conta (500): não afeta a primeira.
        var elsewhere = GroupTx("Outra conta", 40m, D(6, 11));
        elsewhere.AddPayment(UserId, 40m, D(6, 12), today, otherAccount, null, Now);
        // Movimentação individual (despesa de 15 com vencimento 12/06 na conta): vale o valor TOTAL, como antes.
        var individual = Transaction.Create(UserId, "Individual", TransactionType.Expense, 15m, category, account, null, TransactionStatus.Pending, D(6, 12), D(6, 12), null, Now);
        db.Transactions.AddRange(rent, sameDay, withInterest, noAccount, canceled, late, elsewhere, individual);
        await db.SaveChangesAsync();
        var service = new BalanceProjectionService(db);

        var at0615 = await service.GetAsync([account.Id, otherAccount.Id], D(6, 15), CancellationToken.None);
        var at0630 = await service.GetAsync([account.Id, otherAccount.Id], D(6, 30), CancellationToken.None);
        var at0604 = await service.GetAsync([account.Id], D(6, 4), CancellationToken.None);

        // 06/15: 1000 − 60 (aluguel) − 85 (80 + 5 de juros) − 15 (individual) = 840.
        Assert.Equal(840m, at0615[account.Id].ProjectedBalance);
        // 06/30: 840 − 70 (tarde) = 770.
        Assert.Equal(770m, at0630[account.Id].ProjectedBalance);
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
        var t = Transaction.CreateForGroup(GroupId, UserId, "Grupo", 300m, category, null, TransactionStatus.Pending, D(6, 5), D(6, 8), null, Now);
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
