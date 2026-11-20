using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa4;

/// <summary>Movimentação de grupo: um registro, sem dados privados, autoria e pagamentos por membro (D-032).</summary>
public class GroupTransactionDomainTests
{
    private static readonly Guid GroupId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid Payer2 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static Transaction NewGroupExpense(decimal amount = 100m, Category? category = null, DateOnly? due = null) =>
        Transaction.CreateForGroup(
            GroupId, UserId, "Mercado", TransactionType.Expense, amount, category ?? ExpenseCategory(), PaymentMethod.Pix,
            TransactionStatus.Pending, Today, due, null, Now);

    private static void Update(Transaction t, Guid by, decimal? amount = null) =>
        t.Update(by, "Mercado", amount ?? t.Amount, t.Type == TransactionType.Expense ? ExpenseCategory() : IncomeCategory(), null, null, t.TransactionDate, t.DueDate, null, null, Now.AddMinutes(1));

    [Fact]
    public void CreateForGroup_HasGroup_AndNoAccountCardPlanOrRecurrence()
    {
        var t = NewGroupExpense();

        Assert.Equal(GroupId, t.GroupId);
        Assert.True(t.IsGroupTransaction);
        Assert.Equal(UserId, t.CreatedByUserId);
        Assert.Null(t.AccountId);
        Assert.Null(t.CreditCardId);
        Assert.Null(t.InstallmentPlanId);
        Assert.Null(t.RecurrenceRuleId);
        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(TransactionAuditAction.Created, Assert.Single(t.AuditEntries).Action);
    }

    [Fact]
    public void IndividualTransaction_HasNoGroup() => Assert.Null(Expense().GroupId);

    [Fact]
    public void CreateForGroup_AcceptsDefaultCategories()
    {
        using var db = TestDb.Create();
        var defaultCategory = db.Categories.First(c => c.IsDefault && c.Type == CategoryType.Expense);

        var t = NewGroupExpense(category: defaultCategory);

        Assert.Equal(defaultCategory.Id, t.CategoryId);
    }

    [Fact]
    public void CreateForGroup_RejectsAnotherUsersCustomCategory()
    {
        var foreign = Category.CreateCustom(OtherUserId, "Dele", CategoryType.Expense, null, null, null);

        Assert.Throws<DomainException>(() => NewGroupExpense(category: foreign));
    }

    [Fact]
    public void Update_OnlyByTheAuthor_OthersAreForbidden()
    {
        var t = NewGroupExpense();

        Update(t, UserId, 120m);
        Assert.Equal(120m, t.Amount);
        Assert.Throws<PermissionDeniedException>(() => Update(t, OtherUserId, 130m));
        Assert.Equal(120m, t.Amount);
    }

    [Fact]
    public void CancelReopenDelete_OnlyByTheAuthor()
    {
        var t = NewGroupExpense();

        Assert.Throws<PermissionDeniedException>(() => t.Cancel(OtherUserId, Now));
        t.Cancel(UserId, Now);
        Assert.Throws<PermissionDeniedException>(() => t.Reopen(OtherUserId, Now));
        t.Reopen(UserId, Now);
        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Throws<PermissionDeniedException>(() => t.EnsureCanBeDeleted(OtherUserId));
        t.EnsureCanBeDeleted(UserId);
    }

    [Fact]
    public void Delete_AuthorWithPayments_IsRejectedAs422NotForbidden()
    {
        var t = NewGroupExpense();
        t.Pay(10m, by: UserId);

        var ex = Assert.Throws<DomainException>(() => t.EnsureCanBeDeleted(UserId));
        Assert.IsNotType<PermissionDeniedException>(ex);
    }

    [Fact]
    public void Payments_FromSeveralMembers_AccumulateUntilSettled()
    {
        var t = NewGroupExpense(100m);

        t.Pay(30m, by: UserId);
        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(30m, t.PaidAmount);
        Assert.Null(t.PaidAt);

        t.Pay(50m, by: Payer2);
        Assert.Equal(80m, t.PaidAmount);
        Assert.Equal(20m, t.RemainingAmount);
        Assert.Equal(TransactionStatus.Pending, t.Status);

        t.Pay(20m, by: OtherUserId);
        Assert.Equal(TransactionStatus.Paid, t.Status);
        Assert.Equal(100m, t.PaidAmount);
        Assert.Equal(Today, t.PaidAt);
        Assert.Equal(new[] { UserId, Payer2, OtherUserId }, t.Payments.OrderBy(p => p.CreatedAt).Select(p => p.CreatedByUserId).ToArray());
    }

    [Fact]
    public void Payment_AboveRemaining_IsRejected_EvenFromAnotherMember()
    {
        var t = NewGroupExpense(100m);
        t.Pay(70m, by: UserId);

        var ex = Assert.Throws<DomainException>(() => t.Pay(40m, by: Payer2));

        Assert.Contains("excede", ex.Message);
        Assert.Equal(70m, t.PaidAmount);
    }

    [Fact]
    public void Income_SettledByPaymentsFromTwoMembers_BecomesReceived()
    {
        var t = Transaction.CreateForGroup(
            GroupId, UserId, "Reembolso", TransactionType.Income, 100m, IncomeCategory(), null, TransactionStatus.Pending, Today, null, null, Now);

        t.Pay(40m, by: UserId);
        t.Pay(60m, by: Payer2);

        Assert.Equal(TransactionStatus.Received, t.Status);
    }

    [Fact]
    public void RemovePayment_OnlyByThePayer()
    {
        var t = NewGroupExpense(100m);
        var mine = t.Pay(40m, by: UserId);
        var theirs = t.Pay(60m, by: Payer2);
        Assert.Equal(TransactionStatus.Paid, t.Status);

        Assert.Throws<PermissionDeniedException>(() => t.RemovePayment(UserId, theirs.Id, Now));
        Assert.Equal(100m, t.PaidAmount);

        t.RemovePayment(Payer2, theirs.Id, Now);
        Assert.Equal(40m, t.PaidAmount);
        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Null(t.PaidAt);
        Assert.Contains(mine, t.Payments);
    }

    [Fact]
    public void Payment_WithAnotherUsersAccount_IsRejected()
    {
        var t = NewGroupExpense(100m);
        var ownerAccount = NewAccount();

        t.AddPayment(UserId, 10m, Today, Today, ownerAccount, null, Now);
        Assert.Equal(ownerAccount.Id, t.Payments.Single().AccountId);
        Assert.Throws<DomainException>(() => t.AddPayment(Payer2, 10m, Today, Today, ownerAccount, null, Now));
        Assert.Equal(10m, t.PaidAmount);
    }

    [Fact]
    public void CanceledGroupTransaction_RejectsPaymentsFromAnyone()
    {
        var t = NewGroupExpense(100m);
        t.Cancel(UserId, Now);

        Assert.Throws<DomainException>(() => t.Pay(10m, by: Payer2));
    }

    [Fact]
    public void Payment_DetachAccount_KeepsThePayment()
    {
        var t = NewGroupExpense(100m);
        var payment = t.AddPayment(UserId, 10m, Today, Today, NewAccount(), null, Now);

        payment.DetachAccount();

        Assert.Null(payment.AccountId);
        Assert.Equal(10m, t.PaidAmount);
    }

    [Fact]
    public void AuditEntries_RecordWhoActed()
    {
        var t = NewGroupExpense(100m);
        t.Pay(10m, by: Payer2);

        Assert.Contains(t.AuditEntries, e => e.Action == TransactionAuditAction.PaymentAdded && e.UserId == Payer2);
        Assert.Contains(t.AuditEntries, e => e.Action == TransactionAuditAction.Created && e.UserId == UserId);
    }
}
