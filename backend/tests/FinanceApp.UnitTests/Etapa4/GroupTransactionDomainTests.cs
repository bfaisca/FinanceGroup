using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Recurrences;
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
            GroupId, UserId, "Mercado", amount, category ?? ExpenseCategory(), PaymentMethod.Pix,
            TransactionStatus.Pending, Today, due, null, Now);

    private static void Update(Transaction t, Guid by, decimal? amount = null) =>
        t.Update(by, "Mercado", amount ?? t.Amount, ExpenseCategory(), null, null, t.TransactionDate, t.DueDate, null, null, Now.AddMinutes(1));

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

    [Fact]
    public void CreateForGroup_IsAlwaysAnExpense()
    {
        var t = NewGroupExpense();

        Assert.Equal(TransactionType.Expense, t.Type);
    }

    [Fact]
    public void CreateForGroup_DueDateIsFree_BeforeTheTransactionDateAndInThePast()
    {
        var t = NewGroupExpense(due: Today.AddDays(-30));

        Assert.Equal(Today.AddDays(-30), t.DueDate);
        Assert.Equal(TransactionStatus.Overdue, t.GetPresentedStatus(Today));
    }

    [Fact]
    public void IndividualTransaction_StillRejectsADueDateBeforeTheDate() =>
        Assert.Throws<DomainException>(() => Transaction.Create(
            UserId, "x", TransactionType.Expense, 10m, ExpenseCategory(), null, null, TransactionStatus.Pending, Today, Today.AddDays(-1), null, Now));

    [Fact]
    public void Update_GroupExpense_AllowsDueDateBeforeTheDate()
    {
        var t = NewGroupExpense();

        t.Update(UserId, "Mercado", 100m, ExpenseCategory(), null, null, Today, Today.AddDays(-5), null, null, Now.AddMinutes(1));

        Assert.Equal(Today.AddDays(-5), t.DueDate);
    }

    // ------------------------------------------------------------------ juros/multa (D-048)

    [Fact]
    public void Interest_DoesNotCountForTheStatus_OnlyThePrincipalDoes()
    {
        var t = NewGroupExpense(500m);

        var payment = t.AddPayment(UserId, 500m, Today, Today, null, null, Now, interestAmount: 23.50m);

        Assert.Equal(TransactionStatus.Paid, t.Status);
        Assert.Equal(500m, t.Amount);
        Assert.Equal(500m, t.PaidAmount);
        Assert.Equal(0m, t.RemainingAmount);
        Assert.Equal(23.50m, payment.InterestAmount);
        Assert.Equal(523.50m, payment.TotalAmount);
    }

    [Fact]
    public void Interest_PartialPrincipalWithInterest_StaysPending()
    {
        var t = NewGroupExpense(500m);

        t.AddPayment(UserId, 200m, Today, Today, null, null, Now, interestAmount: 50m);

        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(300m, t.RemainingAmount);
    }

    [Fact]
    public void Interest_DoesNotExtendTheLimit_PrincipalAboveRemainingIsRejected()
    {
        var t = NewGroupExpense(100m);

        Assert.Throws<DomainException>(() => t.AddPayment(UserId, 100.01m, Today, Today, null, null, Now, interestAmount: 5m));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.234)]
    public void Interest_NegativeOrWithTooManyDecimals_IsRejected(double interest)
    {
        var t = NewGroupExpense(100m);

        Assert.Throws<DomainException>(() => t.AddPayment(UserId, 10m, Today, Today, null, null, Now, interestAmount: (decimal)interest));
    }

    [Fact]
    public void Interest_IsNotAllowedInIndividualPayments()
    {
        var t = Expense();

        Assert.Throws<DomainException>(() => t.AddPayment(UserId, 10m, Today, Today, null, null, Now, interestAmount: 1m));
    }

    // ------------------------------------------------------------------ edição do pagamento (D-048)

    [Fact]
    public void UpdatePayment_RecalculatesPaidAmountAndStatus_AndRecordsHistory()
    {
        var t = NewGroupExpense(100m);
        var payment = t.AddPayment(UserId, 100m, Today, Today, null, null, Now);
        Assert.Equal(TransactionStatus.Paid, t.Status);

        t.UpdatePayment(UserId, payment.Id, 60m, 5m, Today.AddDays(-1), Today, null, PaymentMethod.Pix, Now.AddHours(1));

        Assert.Equal(60m, t.PaidAmount);
        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Null(t.PaidAt);
        Assert.Null(t.PaidByUserId);
        Assert.Equal((60m, 5m, Today.AddDays(-1), PaymentMethod.Pix), (payment.Amount, payment.InterestAmount, payment.PaymentDate, payment.PaymentMethod));
        Assert.Equal(Now.AddHours(1), payment.UpdatedAt);
        var audit = Assert.Single(t.AuditEntries, e => e.Action == TransactionAuditAction.PaymentUpdated);
        Assert.Equal("100.00 (2026-11-18)", audit.OldValue);
        Assert.Equal("60.00 + 5.00 juros (2026-11-17)", audit.NewValue);

        t.UpdatePayment(UserId, payment.Id, 100m, 0m, Today, Today, null, null, Now.AddHours(2));
        Assert.Equal(TransactionStatus.Paid, t.Status);
        Assert.Equal(Today, t.PaidAt);
        Assert.Equal(UserId, t.PaidByUserId);
    }

    [Fact]
    public void UpdatePayment_OnlyByThePayer()
    {
        var t = NewGroupExpense(100m);
        var payment = t.AddPayment(Payer2, 40m, Today, Today, null, null, Now);

        Assert.Throws<PermissionDeniedException>(() => t.UpdatePayment(UserId, payment.Id, 10m, 0m, Today, Today, null, null, Now));
        Assert.Equal(40m, t.PaidAmount);
    }

    [Fact]
    public void UpdatePayment_ValidatesTheSumOfPrincipalsAgainstTheAmount()
    {
        var t = NewGroupExpense(100m);
        var mine = t.AddPayment(UserId, 30m, Today, Today, null, null, Now);
        t.AddPayment(Payer2, 60m, Today, Today, null, null, Now);

        t.UpdatePayment(UserId, mine.Id, 40m, 0m, Today, Today, null, null, Now);
        Assert.Equal(100m, t.PaidAmount);
        Assert.Throws<DomainException>(() => t.UpdatePayment(UserId, mine.Id, 40.01m, 0m, Today, Today, null, null, Now));
        Assert.Equal(100m, t.PaidAmount);
    }

    [Fact]
    public void UpdatePayment_InCanceledTransaction_IsRejected_AndFutureDateToo()
    {
        var t = NewGroupExpense(100m);
        var payment = t.AddPayment(UserId, 40m, Today, Today, null, null, Now);

        Assert.Throws<DomainException>(() => t.UpdatePayment(UserId, payment.Id, 40m, 0m, Today.AddDays(1), Today, null, null, Now));
        t.Cancel(UserId, Now);
        Assert.Throws<DomainException>(() => t.UpdatePayment(UserId, payment.Id, 50m, 0m, Today, Today, null, null, Now));
    }

    [Fact]
    public void UpdatePayment_RejectsAnotherUsersAccount_AndIndividualTransactions()
    {
        var t = NewGroupExpense(100m);
        var payment = t.AddPayment(Payer2, 40m, Today, Today, null, null, Now);
        var foreignAccount = NewAccount(); // pertence a UserId, não ao pagador Payer2

        Assert.Throws<DomainException>(() => t.UpdatePayment(Payer2, payment.Id, 40m, 0m, Today, Today, foreignAccount, null, Now));
        var individual = Expense();
        var p = individual.Pay(10m);
        Assert.Throws<DomainException>(() => individual.UpdatePayment(UserId, p.Id, 10m, 0m, Today, Today, null, null, Now));
    }

    // ------------------------------------------------------------------ série (D-049)

    [Fact]
    public void CreateForGroup_WithSeries_CarriesSeriesAndNumber()
    {
        var series = GroupTransactionSeries.Create(GroupId, UserId, RecurrenceFrequency.Monthly, 3, Now);

        var t = Transaction.CreateForGroup(
            GroupId, UserId, "Aluguel", 100m, ExpenseCategory(), null, TransactionStatus.Pending, Today, null, null, Now, series, 2);

        Assert.Equal((series.Id, 2), (t.GroupSeriesId, t.SeriesNumber));
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Daily)]
    [InlineData(RecurrenceFrequency.Custom)]
    public void Series_RejectsDailyAndCustom(RecurrenceFrequency frequency) =>
        Assert.Throws<DomainException>(() => GroupTransactionSeries.Create(GroupId, UserId, frequency, 3, Now));

    [Theory]
    [InlineData(1)]
    [InlineData(61)]
    public void Series_RejectsOccurrencesOutsideTwoToSixty(int occurrences) =>
        Assert.Throws<DomainException>(() => GroupTransactionSeries.Create(GroupId, UserId, RecurrenceFrequency.Monthly, occurrences, Now));

    [Theory]
    [InlineData(RecurrenceFrequency.Weekly, new[] { "2026-01-31", "2026-02-07", "2026-02-14", "2026-02-21" })]
    [InlineData(RecurrenceFrequency.Biweekly, new[] { "2026-01-31", "2026-02-14", "2026-02-28", "2026-03-14" })]
    [InlineData(RecurrenceFrequency.Monthly, new[] { "2026-01-31", "2026-02-28", "2026-03-31", "2026-04-30" })]
    [InlineData(RecurrenceFrequency.Bimonthly, new[] { "2026-01-31", "2026-03-31", "2026-05-31", "2026-07-31" })]
    [InlineData(RecurrenceFrequency.Quarterly, new[] { "2026-01-31", "2026-04-30", "2026-07-31", "2026-10-31" })]
    [InlineData(RecurrenceFrequency.Semiannual, new[] { "2026-01-31", "2026-07-31", "2027-01-31", "2027-07-31" })]
    [InlineData(RecurrenceFrequency.Annual, new[] { "2026-01-31", "2027-01-31", "2028-01-31", "2029-01-31" })]
    public void Series_DatesAreStartPlusIStepsFromTheStart_WithEndOfMonthAdjustmentAndNoDrift(RecurrenceFrequency frequency, string[] expected)
    {
        var series = GroupTransactionSeries.Create(GroupId, UserId, frequency, 4, Now);
        var start = new DateOnly(2026, 1, 31);

        var dates = Enumerable.Range(0, 4).Select(i => series.DateOf(start, i).ToString("yyyy-MM-dd")).ToArray();

        Assert.Equal(expected, dates);
    }
}
