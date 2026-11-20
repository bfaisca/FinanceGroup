using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.UnitTests.Etapa4;

public sealed class GroupTransactionServiceTests : IDisposable
{
    private readonly GroupTestKit kit = new();

    public void Dispose() => kit.Dispose();

    private async Task<(FinanceApp.Domain.Users.User Owner, FinanceApp.Domain.Users.User Member, GroupDetailDto Group)> SetupAsync()
    {
        var owner = await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        var member = await kit.NewUserAsync("Ana");
        await kit.JoinAsync(group.Id, owner, member);
        kit.As(owner);
        return (owner, member, group);
    }

    [Fact]
    public async Task Create_HasASingleRecord_CreatorAndPermissions_AndNoPrivateFields()
    {
        var (owner, _, group) = await SetupAsync();

        var tx = await kit.CreateTxAsync(group.Id, "Mercado", 100m);

        Assert.Equal(group.Id, tx.GroupId);
        Assert.Equal(owner.Id, tx.CreatedBy.UserId);
        Assert.Equal("Maria", tx.CreatedBy.Name);
        Assert.Equal(TransactionStatus.Pending, tx.Status);
        Assert.Equal(new GroupTransactionPermissionsDto(CanEdit: true, CanCancel: true, CanDelete: true, CanPay: true), tx.Permissions);
        Assert.Equal(1, await kit.Db.Transactions.CountAsync());
        var json = System.Text.Json.JsonSerializer.Serialize(tx);
        Assert.DoesNotContain("accountId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("creditCard", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Members_SeeEachOthersTransactions_ButOnlyTheAuthorEdits()
    {
        var (owner, member, group) = await SetupAsync();
        var tx = await kit.CreateTxAsync(group.Id, "Mercado", 100m);

        kit.As(member);
        var seen = await kit.Transactions.GetAsync(group.Id, tx.Id, CancellationToken.None);
        Assert.Equal(new GroupTransactionPermissionsDto(CanEdit: false, CanCancel: false, CanDelete: false, CanPay: true), seen.Permissions);

        var update = new UpdateGroupTransactionRequest
        {
            Description = "Invadido",
            Amount = 100m,
            CategoryId = tx.CategoryId,
            TransactionDate = kit.Today,
        };
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Transactions.UpdateAsync(group.Id, tx.Id, update, CancellationToken.None));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Transactions.CancelAsync(group.Id, tx.Id, CancellationToken.None));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Transactions.ReopenAsync(group.Id, tx.Id, CancellationToken.None));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Transactions.DeleteAsync(group.Id, tx.Id, CancellationToken.None));

        kit.As(owner);
        var edited = await kit.Transactions.UpdateAsync(group.Id, tx.Id, new UpdateGroupTransactionRequest { Description = "Atualizado", Amount = 100m, CategoryId = tx.CategoryId, TransactionDate = kit.Today }, CancellationToken.None);
        Assert.Equal("Atualizado", edited.Description);
        Assert.Contains(await kit.Transactions.GetHistoryAsync(group.Id, tx.Id, CancellationToken.None), e => e.Field == "description" && e.ActorName == "Maria");
    }

    [Fact]
    public async Task AdminRole_DoesNotGrantEditRightsOnOthersTransactions()
    {
        var (owner, member, group) = await SetupAsync();
        kit.As(member);
        var tx = await kit.CreateTxAsync(group.Id, "Da Ana", 50m);

        kit.As(owner); // Owner/Admin não alteram movimentações de terceiros (CLAUDE.md §6).
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Transactions.CancelAsync(group.Id, tx.Id, CancellationToken.None));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => kit.Transactions.DeleteAsync(group.Id, tx.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Payments_FromTwoMembers_SettleTheTransaction_AndAreAttributedToEach()
    {
        var (owner, member, group) = await SetupAsync();
        var tx = await kit.CreateTxAsync(group.Id, "Aluguel", 100m);

        var afterOwner = await kit.PayAsync(group.Id, tx.Id, 30m);
        Assert.Equal(TransactionStatus.Pending, afterOwner.Status);
        Assert.Equal(70m, afterOwner.RemainingAmount);

        kit.As(member);
        var afterMember = await kit.PayAsync(group.Id, tx.Id, 70m);

        Assert.Equal(TransactionStatus.Paid, afterMember.Status);
        Assert.Equal(100m, afterMember.PaidAmount);
        Assert.Equal(kit.Today, afterMember.PaidAt);
        Assert.Equal(new[] { owner.Id, member.Id }, afterMember.Payments.Select(p => p.PaidBy.UserId).ToArray());
        Assert.Equal(new[] { false, true }, afterMember.Payments.Select(p => p.CanRemove).ToArray());
        Assert.False(afterMember.Permissions.CanPay);
        Assert.Equal(new GroupTransactionPermissionsDto(false, false, false, false), afterMember.Permissions);
    }

    [Fact]
    public async Task Payment_AboveRemaining_IsRejected_AndFutureDateIsAValidationError()
    {
        var (_, member, group) = await SetupAsync();
        var tx = await kit.CreateTxAsync(group.Id, "Aluguel", 100m);
        await kit.PayAsync(group.Id, tx.Id, 80m);

        kit.As(member);
        await Assert.ThrowsAsync<DomainException>(() => kit.PayAsync(group.Id, tx.Id, 30m));
        await Assert.ThrowsAsync<FinanceApp.Application.Common.Exceptions.ValidationException>(
            () => kit.PayAsync(group.Id, tx.Id, 10m, kit.Today.AddDays(1)));
        Assert.Equal(80m, (await kit.Transactions.GetAsync(group.Id, tx.Id, CancellationToken.None)).PaidAmount);
    }

    [Fact]
    public async Task RemovePayment_OnlyThePayer_OthersAreForbidden_AndStatusIsRecalculated()
    {
        var (owner, member, group) = await SetupAsync();
        var tx = await kit.CreateTxAsync(group.Id, "Aluguel", 100m);
        await kit.PayAsync(group.Id, tx.Id, 40m);
        kit.As(member);
        var settled = await kit.PayAsync(group.Id, tx.Id, 60m);
        var ownerPayment = settled.Payments.Single(p => p.PaidBy.UserId == owner.Id);
        var memberPayment = settled.Payments.Single(p => p.PaidBy.UserId == member.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => kit.Transactions.RemovePaymentAsync(group.Id, tx.Id, ownerPayment.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Transactions.RemovePaymentAsync(group.Id, tx.Id, Guid.NewGuid(), CancellationToken.None));

        var after = await kit.Transactions.RemovePaymentAsync(group.Id, tx.Id, memberPayment.Id, CancellationToken.None);
        Assert.Equal(TransactionStatus.Pending, after.Status);
        Assert.Equal(40m, after.PaidAmount);
        Assert.Null(after.PaidAt);
    }

    [Fact]
    public async Task ArchivedGroup_IsReadOnly_ButStillReadable()
    {
        var (owner, member, group) = await SetupAsync();
        var tx = await kit.CreateTxAsync(group.Id, "Aluguel", 100m);
        var paid = await kit.PayAsync(group.Id, tx.Id, 10m);
        await kit.Groups.ArchiveAsync(group.Id, CancellationToken.None);

        var seen = await kit.Transactions.GetAsync(group.Id, tx.Id, CancellationToken.None);
        Assert.Equal(new GroupTransactionPermissionsDto(false, false, false, false), seen.Permissions);
        Assert.False(seen.Payments.Single().CanRemove);
        Assert.Single((await kit.Transactions.ListAsync(group.Id, new GroupTransactionListQuery(), CancellationToken.None)).Items);

        await Assert.ThrowsAsync<DomainException>(() => kit.CreateTxAsync(group.Id, "Nova", 5m));
        await Assert.ThrowsAsync<DomainException>(() => kit.PayAsync(group.Id, tx.Id, 5m));
        await Assert.ThrowsAsync<DomainException>(() => kit.Transactions.CancelAsync(group.Id, tx.Id, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => kit.Transactions.DeleteAsync(group.Id, tx.Id, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => kit.Transactions.RemovePaymentAsync(group.Id, tx.Id, paid.Payments.Single().Id, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => kit.Transactions.UpdateAsync(
            group.Id, tx.Id, new UpdateGroupTransactionRequest { Description = "x", Amount = 100m, CategoryId = tx.CategoryId, TransactionDate = kit.Today }, CancellationToken.None));

        kit.As(member);
        await Assert.ThrowsAsync<DomainException>(() => kit.PayAsync(group.Id, tx.Id, 5m));
        Assert.NotNull(owner);
    }

    [Fact]
    public async Task Delete_OnlyWithoutPayments_ElseCancel()
    {
        var (_, _, group) = await SetupAsync();
        var withPayments = await kit.CreateTxAsync(group.Id, "Com pagamento", 100m);
        var empty = await kit.CreateTxAsync(group.Id, "Sem pagamento", 100m);
        await kit.PayAsync(group.Id, withPayments.Id, 10m);

        await Assert.ThrowsAsync<DomainException>(() => kit.Transactions.DeleteAsync(group.Id, withPayments.Id, CancellationToken.None));
        await kit.Transactions.DeleteAsync(group.Id, empty.Id, CancellationToken.None);
        var canceled = await kit.Transactions.CancelAsync(group.Id, withPayments.Id, CancellationToken.None);
        Assert.Equal(TransactionStatus.Canceled, canceled.Status);
        Assert.False(canceled.Permissions.CanPay);
        Assert.True(canceled.Permissions.CanCancel); // "reabrir" usa a mesma permissão
        var reopened = await kit.Transactions.ReopenAsync(group.Id, withPayments.Id, CancellationToken.None);
        Assert.Equal(TransactionStatus.Pending, reopened.Status);
        Assert.Equal(1, await kit.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task TransactionOfAnotherGroup_IsNotFound_EvenForAMemberOfBoth()
    {
        var (_, _, group) = await SetupAsync();
        var other = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Outro" }, CancellationToken.None);
        var tx = await kit.CreateTxAsync(other.Id, "Do outro", 10m);

        await Assert.ThrowsAsync<NotFoundException>(() => kit.Transactions.GetAsync(group.Id, tx.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Transactions.CancelAsync(group.Id, tx.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.PayAsync(group.Id, tx.Id, 1m));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Transactions.GetHistoryAsync(group.Id, tx.Id, CancellationToken.None));
        Assert.Empty((await kit.Transactions.ListAsync(group.Id, new GroupTransactionListQuery(), CancellationToken.None)).Items);
    }

    [Fact]
    public async Task Category_MustBeDefaultOrTheCreators_AndOfTheSameType()
    {
        var (_, member, group) = await SetupAsync();
        var mine = FinanceApp.Domain.Categories.Category.CreateCustom(kit.CurrentUser.UserId!.Value, "Minha", FinanceApp.Domain.Categories.CategoryType.Expense, null, null, null);
        kit.Db.Categories.Add(mine);
        await kit.Db.SaveChangesAsync();

        var ok = await kit.CreateTxAsync(group.Id, "Com categoria própria", 10m, categoryId: mine.Id);
        Assert.Equal("Minha", ok.CategoryName);

        kit.As(member);
        var ex = await Assert.ThrowsAsync<FinanceApp.Application.Common.Exceptions.ValidationException>(() => kit.CreateTxAsync(group.Id, "Categoria alheia", 10m, categoryId: mine.Id));
        Assert.True(ex.Errors.ContainsKey("categoryId"));
        await Assert.ThrowsAsync<FinanceApp.Application.Common.Exceptions.ValidationException>(
            () => kit.CreateTxAsync(group.Id, "Tipo errado", 10m, categoryId: kit.IncomeCategoryId()));
    }

    [Fact]
    public async Task Payment_WithTheAccountOfAnotherUser_IsAValidationError()
    {
        var (owner, member, group) = await SetupAsync();
        var account = FinanceApp.Domain.Accounts.Account.Create(owner.Id, "Conta", null, FinanceApp.Domain.Accounts.AccountType.Checking, kit.Clock.UtcNow);
        kit.Db.Accounts.Add(account);
        await kit.Db.SaveChangesAsync();
        var tx = await kit.CreateTxAsync(group.Id, "Aluguel", 100m);

        kit.As(member);
        var ex = await Assert.ThrowsAsync<FinanceApp.Application.Common.Exceptions.ValidationException>(() => kit.Transactions.AddPaymentAsync(
            group.Id, tx.Id, new FinanceApp.Application.Transactions.AddPaymentRequest { Amount = 10m, PaymentDate = kit.Today, AccountId = account.Id }, CancellationToken.None));
        Assert.True(ex.Errors.ContainsKey("accountId"));

        // O próprio dono da conta pode usá-la; ela fica no pagamento, mas nunca aparece no DTO.
        kit.As(owner);
        var paid = await kit.Transactions.AddPaymentAsync(
            group.Id, tx.Id, new FinanceApp.Application.Transactions.AddPaymentRequest { Amount = 10m, PaymentDate = kit.Today, AccountId = account.Id }, CancellationToken.None);
        Assert.Equal(account.Id, (await kit.Db.TransactionPayments.AsNoTracking().SingleAsync()).AccountId);
        Assert.DoesNotContain(account.Id.ToString(), System.Text.Json.JsonSerializer.Serialize(paid), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_FiltersByCreatorPayerStatusTypeAndSearch()
    {
        var (owner, member, group) = await SetupAsync();
        var rent = await kit.CreateTxAsync(group.Id, "Aluguel", 100m);
        await kit.CreateTxAsync(group.Id, "Salário extra", 50m, type: TransactionTypeArg.Income);
        kit.As(member);
        var market = await kit.CreateTxAsync(group.Id, "Mercado", 20m);
        await kit.PayAsync(group.Id, rent.Id, 10m);
        await kit.PayAsync(group.Id, market.Id, 20m);

        async Task<string[]> Names(GroupTransactionListQuery q) =>
            (await kit.Transactions.ListAsync(group.Id, q, CancellationToken.None)).Items.Select(i => i.Description).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "Aluguel", "Salário extra" }, await Names(new GroupTransactionListQuery { CreatedByUserId = owner.Id }));
        Assert.Equal(new[] { "Aluguel", "Mercado" }, await Names(new GroupTransactionListQuery { PaidByUserId = member.Id }));
        Assert.Equal(new[] { "Mercado" }, await Names(new GroupTransactionListQuery { Status = TransactionStatus.Paid }));
        Assert.Equal(new[] { "Salário extra" }, await Names(new GroupTransactionListQuery { Type = TransactionType.Income }));
        Assert.Equal(new[] { "Aluguel" }, await Names(new GroupTransactionListQuery { Search = "ALUG" }));
        Assert.Equal(new[] { "Aluguel" }, await Names(new GroupTransactionListQuery { CreatedByUserId = owner.Id, Status = TransactionStatus.Pending, Type = TransactionType.Expense }));
        Assert.Empty(await Names(new GroupTransactionListQuery { PaidByUserId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task History_ShowsTheActorNames_MostRecentFirst()
    {
        var (_, member, group) = await SetupAsync();
        var tx = await kit.CreateTxAsync(group.Id, "Aluguel", 100m);
        kit.Clock.Advance(TimeSpan.FromMinutes(5));
        kit.As(member);
        await kit.PayAsync(group.Id, tx.Id, 10m);

        var history = await kit.Transactions.GetHistoryAsync(group.Id, tx.Id, CancellationToken.None);

        Assert.Equal(TransactionAuditAction.PaymentAdded, history[0].Action);
        Assert.Equal("Ana", history[0].ActorName);
        Assert.Equal(TransactionAuditAction.Created, history[^1].Action);
        Assert.Equal("Maria", history[^1].ActorName);
    }
}
