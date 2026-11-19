using FinanceApp.Application.Accounts;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Transactions;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.UnitTests.Support;
using static FinanceApp.UnitTests.Support.Fixtures;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.UnitTests.Financial;

public sealed class AccountTests : IDisposable
{
    private readonly AppDbContext db = TestDb.Create();
    private readonly FakeCurrentUser currentUser = new() { UserId = UserId };
    private readonly FakeClock clock = new(Now);
    private readonly AccountService service;

    public AccountTests()
    {
        clock.SetToday(Today);
        service = new AccountService(
            db, currentUser, clock, new BalanceProjectionService(db),
            new CreateAccountRequestValidator(clock), new UpdateAccountRequestValidator(), new CreateBalanceSnapshotRequestValidator(clock));
    }

    public void Dispose() => db.Dispose();

    private static CreateAccountRequest NewAccount(string name = "Nubank", decimal? balance = null, DateOnly? date = null) =>
        new() { Name = name, Type = AccountType.Checking, InitialBalance = balance, BalanceReferenceDate = date };

    // ------------------------------------------------------------ domínio

    [Fact]
    public void Account_Create_NormalizesNameAndInstitution()
    {
        var account = Account.Create(UserId, "  Conta  ", "  ", AccountType.Wallet, Now);
        Assert.Equal("Conta", account.Name);
        Assert.Null(account.Institution);
        Assert.Equal("BRL", account.Currency);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void Account_RecordBalance_RejectsFutureDate_AndInactiveAccount()
    {
        var account = NewAccountEntity();
        Assert.Throws<DomainException>(() => account.RecordBalance(10m, Today.AddDays(1), Today, Now));

        account.Deactivate(Now);
        Assert.Throws<DomainException>(() => account.RecordBalance(10m, Today, Today, Now));
    }

    [Theory]
    [InlineData(10.123)]
    public void Account_RecordBalance_RejectsMoreThanTwoDecimals(double value) =>
        Assert.Throws<DomainException>(() => NewAccountEntity().RecordBalance((decimal)value, Today, Today, Now));

    [Fact]
    public void Account_RecordBalance_AcceptsNegativeAndLimits()
    {
        var account = NewAccountEntity();
        Assert.Equal(-50m, account.RecordBalance(-50m, Today, Today, Now).Balance);
        Assert.Equal(-MoneyRules.MaxAmount, account.RecordBalance(-MoneyRules.MaxAmount, Today, Today, Now).Balance);
        Assert.Throws<DomainException>(() => account.RecordBalance(MoneyRules.MaxAmount + 0.01m, Today, Today, Now));
    }

    private static Account NewAccountEntity() => Account.Create(UserId, "Conta", null, AccountType.Checking, Now);

    // ------------------------------------------------------------ serviço

    [Fact]
    public async Task Create_WithoutInitialBalance_HasNoSnapshotAndNoProjection()
    {
        var dto = await service.CreateAsync(NewAccount(), default);

        Assert.Null(dto.LatestSnapshot);
        Assert.Null(dto.ProjectedBalance);
        Assert.Equal(Today, dto.ProjectedAsOf);
        Assert.Equal("BRL", dto.Currency);
        Assert.Empty(await service.ListSnapshotsAsync(dto.Id, default));
    }

    [Fact]
    public async Task Create_WithInitialBalance_DefaultsReferenceDateToToday()
    {
        var dto = await service.CreateAsync(NewAccount(balance: 1500.50m), default);

        Assert.Equal(new LatestSnapshotDto(1500.50m, Today), dto.LatestSnapshot);
        Assert.Equal(1500.50m, dto.ProjectedBalance);
    }

    [Fact]
    public async Task Create_WithReferenceDateButNoBalance_IsValidationError()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(NewAccount(date: Today), default));
        Assert.True(ex.Errors.ContainsKey("initialBalance"));
    }

    [Fact]
    public async Task Create_WithFutureReferenceDate_IsValidationError()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(NewAccount(balance: 1m, date: Today.AddDays(1)), default));
        Assert.True(ex.Errors.ContainsKey("balanceReferenceDate"));
    }

    [Fact]
    public async Task Create_DuplicateName_IgnoresCase_Conflict()
    {
        await service.CreateAsync(NewAccount("Nubank"), default);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(NewAccount(" NUBANK "), default));
        Assert.Equal(ErrorCodes.Conflict, ex.Code);
    }

    [Fact]
    public async Task Create_SameNameForAnotherUser_IsAllowed()
    {
        await service.CreateAsync(NewAccount("Nubank"), default);
        currentUser.UserId = OtherUserId;
        await service.CreateAsync(NewAccount("Nubank"), default);
    }

    [Fact]
    public async Task Update_ToNameOfAnotherAccount_Conflict_ButOwnNameIsFine()
    {
        await service.CreateAsync(NewAccount("A"), default);
        var b = await service.CreateAsync(NewAccount("B"), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(b.Id, new UpdateAccountRequest { Name = "a", Type = AccountType.Savings }, default));

        var ok = await service.UpdateAsync(b.Id, new UpdateAccountRequest { Name = "B", Institution = "Itaú", Type = AccountType.Savings }, default);
        Assert.Equal(AccountType.Savings, ok.Type);
        Assert.Equal("Itaú", ok.Institution);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RequiresName(string name)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(NewAccount(name), default));
        Assert.True(ex.Errors.ContainsKey("name"));
    }

    [Fact]
    public async Task Create_RequiresType()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new CreateAccountRequest { Name = "X" }, default));
        Assert.Equal("O tipo da conta é obrigatório.", ex.Errors["type"][0]);
    }

    [Fact]
    public async Task Create_LongNameOrInstitution_IsInvalid()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new CreateAccountRequest { Name = new string('a', 101), Institution = new string('b', 101), Type = AccountType.Other }, default));
        Assert.True(ex.Errors.ContainsKey("name"));
        Assert.True(ex.Errors.ContainsKey("institution"));
    }

    [Fact]
    public async Task Snapshots_NewOneBecomesReference_AndOldOnesAreKept_MostRecentFirst()
    {
        var account = await service.CreateAsync(NewAccount(balance: 1000m, date: Today.AddDays(-10)), default);

        clock.Advance(TimeSpan.FromMinutes(1));
        await service.CreateSnapshotAsync(account.Id, new CreateBalanceSnapshotRequest { Balance = 2000m, ReferenceDate = Today.AddDays(-5) }, default);
        clock.Advance(TimeSpan.FromMinutes(1));
        await service.CreateSnapshotAsync(account.Id, new CreateBalanceSnapshotRequest { Balance = 1800m, ReferenceDate = Today.AddDays(-5) }, default);

        var list = await service.ListSnapshotsAsync(account.Id, default);
        Assert.Equal([1800m, 2000m, 1000m], list.Select(s => s.Balance));

        var current = await service.GetAsync(account.Id, null, default);
        Assert.Equal(new LatestSnapshotDto(1800m, Today.AddDays(-5)), current.LatestSnapshot);
        Assert.Equal(1800m, current.ProjectedBalance);
    }

    [Fact]
    public async Task Snapshot_OnInactiveAccount_Fails()
    {
        var account = await service.CreateAsync(NewAccount(), default);
        await service.DeactivateAsync(account.Id, default);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateSnapshotAsync(account.Id, new CreateBalanceSnapshotRequest { Balance = 1m, ReferenceDate = Today }, default));
    }

    [Fact]
    public async Task Snapshot_Validation()
    {
        var account = await service.CreateAsync(NewAccount(), default);

        var future = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateSnapshotAsync(account.Id, new CreateBalanceSnapshotRequest { Balance = 1m, ReferenceDate = Today.AddDays(1) }, default));
        Assert.True(future.Errors.ContainsKey("referenceDate"));

        var missing = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateSnapshotAsync(account.Id, new CreateBalanceSnapshotRequest(), default));
        Assert.True(missing.Errors.ContainsKey("balance"));
        Assert.True(missing.Errors.ContainsKey("referenceDate"));

        var decimals = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateSnapshotAsync(account.Id, new CreateBalanceSnapshotRequest { Balance = 1.234m, ReferenceDate = Today }, default));
        Assert.True(decimals.Errors.ContainsKey("balance"));

        var huge = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateSnapshotAsync(account.Id, new CreateBalanceSnapshotRequest { Balance = MoneyRules.MaxAmount + 1, ReferenceDate = Today }, default));
        Assert.True(huge.Errors.ContainsKey("balance"));
    }

    [Fact]
    public async Task Snapshot_AllowsNegativeBalance()
    {
        var account = await service.CreateAsync(NewAccount(), default);
        var snapshot = await service.CreateSnapshotAsync(account.Id, new CreateBalanceSnapshotRequest { Balance = -300.10m, ReferenceDate = Today }, default);
        Assert.Equal(-300.10m, snapshot.Balance);
    }

    [Fact]
    public async Task DeactivateAndActivate_AreIdempotent_AndListHidesInactive()
    {
        var account = await service.CreateAsync(NewAccount(), default);

        Assert.False((await service.DeactivateAsync(account.Id, default)).IsActive);
        Assert.False((await service.DeactivateAsync(account.Id, default)).IsActive);
        Assert.Empty(await service.ListAsync(false, null, default));
        Assert.Single(await service.ListAsync(true, null, default));

        Assert.True((await service.ActivateAsync(account.Id, default)).IsActive);
        Assert.Single(await service.ListAsync(false, null, default));
    }

    [Fact]
    public async Task OtherUsersAccount_IsNotFoundEverywhere_AndNotListed()
    {
        var mine = await service.CreateAsync(NewAccount(balance: 10m), default);
        currentUser.UserId = OtherUserId;

        Assert.Empty(await service.ListAsync(true, null, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(mine.Id, null, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(mine.Id, new UpdateAccountRequest { Name = "x", Type = AccountType.Other }, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeactivateAsync(mine.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ActivateAsync(mine.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ListSnapshotsAsync(mine.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateSnapshotAsync(mine.Id, new CreateBalanceSnapshotRequest { Balance = 1m, ReferenceDate = Today }, default));
    }

    [Fact]
    public async Task List_IsOrderedByName()
    {
        await service.CreateAsync(NewAccount("Carteira"), default);
        await service.CreateAsync(NewAccount("Banco"), default);
        Assert.Equal(["Banco", "Carteira"], (await service.ListAsync(false, null, default)).Select(a => a.Name));
    }

    [Fact]
    public async Task Projection_UsesEffectiveDate_AsOf_AndIgnoresCanceledAndOtherAccounts()
    {
        var account = await service.CreateAsync(NewAccount(balance: 1000m, date: Today.AddDays(-10)), default);
        var other = await service.CreateAsync(NewAccount("Outra", balance: 0m, date: Today.AddDays(-10)), default);
        var entity = db.Accounts.Single(a => a.Id == account.Id);
        var category = ExpenseCategory();
        var income = IncomeCategory();
        db.Categories.AddRange(category, income);

        // Conta: receita de 500 vencendo em +3 (data da movimentação antiga, mas vencimento define a janela).
        db.Transactions.Add(Transaction.Create(UserId, "Salário", TransactionType.Income, 500m, income, entity, null, TransactionStatus.Pending, Today.AddDays(-9), Today.AddDays(3), null, Now));
        // Despesa sem vencimento: usa transactionDate (-2).
        db.Transactions.Add(Transaction.Create(UserId, "Mercado", TransactionType.Expense, 120m, category, entity, null, TransactionStatus.Pending, Today.AddDays(-2), null, null, Now));
        // Antes da referência: ignorada.
        db.Transactions.Add(Transaction.Create(UserId, "Antiga", TransactionType.Expense, 999m, category, entity, null, TransactionStatus.Pending, Today.AddDays(-20), null, null, Now));
        // Cancelada: ignorada.
        var canceled = Transaction.Create(UserId, "Cancelada", TransactionType.Expense, 300m, category, entity, null, TransactionStatus.Pending, Today.AddDays(-1), null, null, Now);
        canceled.Cancel(UserId, Now);
        db.Transactions.Add(canceled);
        // Outra conta.
        db.Transactions.Add(Transaction.Create(UserId, "Outra", TransactionType.Expense, 50m, category, db.Accounts.Single(a => a.Id == other.Id), null, TransactionStatus.Pending, Today.AddDays(-1), null, null, Now));
        await db.SaveChangesAsync();

        Assert.Equal(1000m - 120m, (await service.GetAsync(account.Id, Today, default)).ProjectedBalance);
        Assert.Equal(1000m - 120m + 500m, (await service.GetAsync(account.Id, Today.AddDays(3), default)).ProjectedBalance);
        Assert.Equal(1000m, (await service.GetAsync(account.Id, Today.AddDays(-10), default)).ProjectedBalance);
        Assert.Equal(1000m, (await service.GetAsync(account.Id, Today.AddDays(-30), default)).ProjectedBalance);
        Assert.Equal(-50m, (await service.GetAsync(other.Id, Today, default)).ProjectedBalance);
    }
}
