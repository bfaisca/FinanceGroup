using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Financial;

/// <summary>Cobre cada linha de docs/STATE_MACHINE.md (estados, transições e invariantes).</summary>
public class TransactionStateMachineTests
{
    private static readonly DateTimeOffset Later = Now.AddMinutes(5);

    private static void Update(
        Transaction t,
        decimal? amount = null,
        TransactionStatus? status = null,
        DateOnly? due = null,
        Category? category = null,
        bool keepDue = true,
        string description = "Compra",
        Guid? by = null) =>
        t.Update(
            by ?? UserId, description, amount ?? t.Amount, category ?? (t.Type == TransactionType.Expense ? ExpenseCategory() : IncomeCategory()),
            null, t.PaymentMethod, t.TransactionDate, keepDue ? (due ?? t.DueDate) : due, t.Notes, status, Later);

    // ------------------------------------------------------------ criação

    [Theory]
    [InlineData(TransactionStatus.Planned)]
    [InlineData(TransactionStatus.Pending)]
    public void Create_AllowsPlannedAndPending(TransactionStatus status)
    {
        var t = Expense(status: status);

        Assert.Equal(status, t.Status);
        Assert.Equal(0m, t.PaidAmount);
        Assert.Equal(100m, t.RemainingAmount);
        Assert.Null(t.PaidAt);
        Assert.Null(t.PaidByUserId);
        Assert.Equal(UserId, t.CreatedByUserId);
    }

    [Theory]
    [InlineData(TransactionStatus.Paid)]
    [InlineData(TransactionStatus.Received)]
    [InlineData(TransactionStatus.Overdue)]
    [InlineData(TransactionStatus.Canceled)]
    public void Create_RejectsOtherStatuses(TransactionStatus status) =>
        Assert.Throws<DomainException>(() => Expense(status: status));

    [Fact]
    public void Create_RecordsCreatedAuditEntry()
    {
        var t = Expense();

        var entry = Assert.Single(t.AuditEntries);
        Assert.Equal(TransactionAuditAction.Created, entry.Action);
        Assert.Equal(UserId, entry.UserId);
        Assert.Equal(t.Id, entry.TransactionId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.123)]
    public void Create_RejectsInvalidAmounts(double amount) =>
        Assert.Throws<DomainException>(() => Expense(amount: (decimal)amount));

    [Fact]
    public void Create_RejectsAmountAboveLimit() =>
        Assert.Throws<DomainException>(() => Expense(amount: MoneyRules.MaxAmount + 0.01m));

    [Fact]
    public void Create_AcceptsAmountAtLimit() =>
        Assert.Equal(MoneyRules.MaxAmount, Expense(amount: MoneyRules.MaxAmount).Amount);

    [Fact]
    public void Create_RejectsDueDateBeforeTransactionDate() =>
        Assert.Throws<DomainException>(() => Expense(date: Today, due: Today.AddDays(-1)));

    [Fact]
    public void Create_AcceptsDueDateEqualToTransactionDate() =>
        Assert.Equal(Today, Expense(date: Today, due: Today).DueDate);

    [Fact]
    public void Create_RejectsCategoryOfOtherType()
    {
        var ex = Assert.Throws<DomainException>(() => Expense(category: IncomeCategory()));
        Assert.Contains("mesmo tipo", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_RejectsInactiveCategory()
    {
        var category = ExpenseCategory();
        category.Deactivate();
        Assert.Throws<DomainException>(() => Expense(category: category));
    }

    [Fact]
    public void Create_RejectsInactiveAccount()
    {
        var account = NewAccount();
        account.Deactivate(Now);
        Assert.Throws<DomainException>(() => Expense(account: account));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RequiresDescription(string description) =>
        Assert.Throws<DomainException>(() => Transaction.Create(
            UserId, description, TransactionType.Expense, 10m, ExpenseCategory(), null, null, TransactionStatus.Pending, Today, null, null, Now));

    // ------------------------------------------------------------ estado apresentado (Overdue)

    [Fact]
    public void Overdue_PendingWithPastDueDate()
    {
        var t = Expense(due: Today.AddDays(-1), date: Today.AddDays(-60));
        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(TransactionStatus.Overdue, t.GetPresentedStatus(Today));
    }

    [Fact]
    public void Overdue_PlannedWithPastDueDate() =>
        Assert.Equal(TransactionStatus.Overdue, Expense(due: Today.AddDays(-1), date: Today.AddDays(-60), status: TransactionStatus.Planned).GetPresentedStatus(Today));

    [Fact]
    public void Overdue_NotWhenDueToday() =>
        Assert.Equal(TransactionStatus.Pending, Expense(due: Today).GetPresentedStatus(Today));

    [Fact]
    public void Overdue_NotWhenDueInFuture() =>
        Assert.Equal(TransactionStatus.Pending, Expense(due: Today.AddDays(1)).GetPresentedStatus(Today));

    [Fact]
    public void Overdue_NeverWithoutDueDate() =>
        Assert.Equal(TransactionStatus.Pending, Expense().GetPresentedStatus(Today.AddYears(5)));

    [Fact]
    public void Overdue_DependsOnInjectedToday()
    {
        var t = Expense(due: Today);
        Assert.Equal(TransactionStatus.Pending, t.GetPresentedStatus(Today));
        Assert.Equal(TransactionStatus.Overdue, t.GetPresentedStatus(Today.AddDays(1)));
    }

    [Fact]
    public void Overdue_PartiallyPaidWithPastDueDate()
    {
        var t = Expense(due: Today.AddDays(-3), date: Today.AddDays(-60));
        t.Pay(40m);
        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(TransactionStatus.Overdue, t.GetPresentedStatus(Today));
    }

    [Fact]
    public void Overdue_NotForSettledEvenIfDueDatePassed()
    {
        var t = Expense(due: Today.AddDays(-3), date: Today.AddDays(-60));
        t.Pay(100m);
        Assert.Equal(TransactionStatus.Paid, t.GetPresentedStatus(Today));
    }

    [Fact]
    public void Overdue_NotForCanceled()
    {
        var t = Expense(due: Today.AddDays(-3), date: Today.AddDays(-60));
        t.Cancel(UserId, Now);
        Assert.Equal(TransactionStatus.Canceled, t.GetPresentedStatus(Today));
    }

    [Fact]
    public void Overdue_IsNeverPersisted()
    {
        var t = Expense(due: Today.AddDays(-30), date: Today.AddDays(-60));
        Assert.NotEqual(TransactionStatus.Overdue, t.Status);
        t.Pay(10m);
        Assert.NotEqual(TransactionStatus.Overdue, t.Status);
    }

    // ------------------------------------------------------------ pagamentos

    [Fact]
    public void Payment_Partial_BecomesPending()
    {
        var t = Expense(status: TransactionStatus.Planned);

        var payment = t.Pay(30m);

        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(30m, t.PaidAmount);
        Assert.Equal(70m, t.RemainingAmount);
        Assert.Null(t.PaidAt);
        Assert.Null(t.PaidByUserId);
        Assert.Equal(30m, payment.Amount);
        Assert.Single(t.Payments);
    }

    [Fact]
    public void Payment_FullExpense_BecomesPaid_WithPaidAtAndPaidBy()
    {
        var t = Expense();
        var date = Today.AddDays(-2);

        t.Pay(100m, on: date, by: OtherUserId);

        Assert.Equal(TransactionStatus.Paid, t.Status);
        Assert.Equal(date, t.PaidAt);
        Assert.Equal(OtherUserId, t.PaidByUserId);
        Assert.Equal(UserId, t.CreatedByUserId);
        Assert.Equal(0m, t.RemainingAmount);
    }

    [Fact]
    public void Payment_FullIncome_BecomesReceived()
    {
        var t = Income(250m);
        t.Pay(250m);
        Assert.Equal(TransactionStatus.Received, t.Status);
    }

    [Fact]
    public void Payment_Planned_FullPayment_BecomesPaid()
    {
        var t = Expense(status: TransactionStatus.Planned);
        t.Pay(100m);
        Assert.Equal(TransactionStatus.Paid, t.Status);
    }

    [Fact]
    public void Payment_TwoPartials_CompletingOneDefinesPaidAtAndPaidBy()
    {
        var t = Expense();
        t.Pay(60m, on: Today.AddDays(-5), by: UserId, at: Now);

        t.Pay(40m, on: Today.AddDays(-1), by: OtherUserId, at: Now.AddMinutes(1));

        Assert.Equal(TransactionStatus.Paid, t.Status);
        Assert.Equal(Today.AddDays(-1), t.PaidAt);
        Assert.Equal(OtherUserId, t.PaidByUserId);
        Assert.Equal(2, t.Payments.Count);
    }

    [Fact]
    public void Payment_CompletingWithEarlierDate_PaidAtIsTheCompletingPaymentsDate()
    {
        var t = Expense();
        t.Pay(60m, on: Today);

        t.Pay(40m, on: Today.AddDays(-10), at: Now.AddMinutes(1));

        Assert.Equal(Today.AddDays(-10), t.PaidAt);
    }

    [Fact]
    public void Payment_ExceedingRemaining_Fails_AndKeepsState()
    {
        var t = Expense();
        t.Pay(60m);

        var ex = Assert.Throws<DomainException>(() => t.Pay(40.01m));

        Assert.Contains("excede", ex.Message, StringComparison.Ordinal);
        Assert.Equal(60m, t.PaidAmount);
        Assert.Single(t.Payments);
        Assert.Equal(TransactionStatus.Pending, t.Status);
    }

    [Fact]
    public void Payment_OnSettledTransaction_Fails()
    {
        var t = Expense();
        t.Pay(100m);
        Assert.Throws<DomainException>(() => t.Pay(0.01m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    public void Payment_InvalidAmount_Fails(double amount) =>
        Assert.Throws<DomainException>(() => Expense().Pay((decimal)amount));

    [Fact]
    public void Payment_FutureDate_Fails() =>
        Assert.Throws<DomainException>(() => Expense().Pay(10m, on: Today.AddDays(1)));

    [Fact]
    public void Payment_TodayDate_IsAllowed() =>
        Assert.Equal(Today, Expense().Pay(10m, on: Today).PaymentDate);

    [Fact]
    public void Payment_OnCanceled_Fails()
    {
        var t = Expense();
        t.Cancel(UserId, Now);
        Assert.Throws<DomainException>(() => t.Pay(10m));
    }

    [Fact]
    public void Payment_WithInactiveAccount_Fails()
    {
        var account = NewAccount();
        account.Deactivate(Now);
        Assert.Throws<DomainException>(() => Expense().AddPayment(UserId, 10m, Today, Today, account, null, Now));
    }

    [Fact]
    public void Payment_RecordsAuditEntry()
    {
        var t = Expense();
        t.Pay(10m);
        var entry = t.AuditEntries.Last();
        Assert.Equal(TransactionAuditAction.PaymentAdded, entry.Action);
        Assert.Contains("10.00", entry.NewValue, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ remover pagamento

    [Fact]
    public void RemovePayment_FromSettled_BecomesPending_AndClearsMarkers()
    {
        var t = Expense();
        var p = t.Pay(100m, by: OtherUserId);

        t.RemovePayment(UserId, p.Id, Later);

        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Null(t.PaidAt);
        Assert.Null(t.PaidByUserId);
        Assert.Equal(0m, t.PaidAmount);
        Assert.Empty(t.Payments);
    }

    [Fact]
    public void RemovePayment_StillPartial_RemainsPending()
    {
        var t = Expense();
        var first = t.Pay(30m);
        t.Pay(20m);

        t.RemovePayment(UserId, first.Id, Later);

        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(20m, t.PaidAmount);
    }

    [Fact]
    public void RemovePayment_FromCompleted_WithTwoPayments_ReturnsToPending()
    {
        var t = Expense();
        t.Pay(60m);
        var second = t.Pay(40m, at: Now.AddMinutes(1));
        Assert.Equal(TransactionStatus.Paid, t.Status);

        t.RemovePayment(UserId, second.Id, Later);

        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(60m, t.PaidAmount);
        Assert.Null(t.PaidAt);
    }

    [Fact]
    public void RemovePayment_AllowsPayingAgainAfterwards()
    {
        var t = Expense();
        var p = t.Pay(100m);
        t.RemovePayment(UserId, p.Id, Later);

        t.Pay(100m);

        Assert.Equal(TransactionStatus.Paid, t.Status);
    }

    [Fact]
    public void RemovePayment_Unknown_Fails() =>
        Assert.Throws<DomainException>(() => Expense().RemovePayment(UserId, Guid.NewGuid(), Later));

    [Fact]
    public void RemovePayment_OnCanceled_Fails()
    {
        var t = Expense();
        var p = t.Pay(10m);
        t.Cancel(UserId, Now);
        Assert.Throws<DomainException>(() => t.RemovePayment(UserId, p.Id, Later));
    }

    [Fact]
    public void RemovePayment_RecordsAuditEntry()
    {
        var t = Expense();
        var p = t.Pay(10m);
        t.RemovePayment(UserId, p.Id, Later);
        Assert.Equal(TransactionAuditAction.PaymentRemoved, t.AuditEntries.Last().Action);
        Assert.NotNull(t.AuditEntries.Last().OldValue);
    }

    // ------------------------------------------------------------ cancelar / reabrir

    [Fact]
    public void Cancel_FromPending_PreservesPayments()
    {
        var t = Expense();
        t.Pay(30m);

        t.Cancel(UserId, Later);

        Assert.Equal(TransactionStatus.Canceled, t.Status);
        Assert.Equal(30m, t.PaidAmount);
        Assert.Single(t.Payments);
        Assert.Equal(TransactionStatus.Canceled, t.GetPresentedStatus(Today));
    }

    [Fact]
    public void Cancel_FromSettled_ClearsPaidMarkersButKeepsPayments()
    {
        var t = Expense();
        t.Pay(100m);

        t.Cancel(UserId, Later);

        Assert.Equal(TransactionStatus.Canceled, t.Status);
        Assert.Null(t.PaidAt);
        Assert.Null(t.PaidByUserId);
        Assert.Equal(100m, t.PaidAmount);
    }

    [Theory]
    [InlineData(TransactionStatus.Planned)]
    [InlineData(TransactionStatus.Pending)]
    public void Cancel_FromPlannedOrPending(TransactionStatus status)
    {
        var t = Expense(status: status);
        t.Cancel(UserId, Later);
        Assert.Equal(TransactionStatus.Canceled, t.Status);
    }

    [Fact]
    public void Cancel_Twice_Fails()
    {
        var t = Expense();
        t.Cancel(UserId, Later);
        Assert.Throws<DomainException>(() => t.Cancel(UserId, Later));
    }

    [Fact]
    public void Reopen_WithoutPayments_BecomesPending()
    {
        var t = Expense(status: TransactionStatus.Planned);
        t.Cancel(UserId, Later);

        t.Reopen(UserId, Later);

        Assert.Equal(TransactionStatus.Pending, t.Status);
    }

    [Fact]
    public void Reopen_WithPartialPayments_BecomesPending()
    {
        var t = Expense();
        t.Pay(40m);
        t.Cancel(UserId, Later);

        t.Reopen(UserId, Later);

        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(40m, t.PaidAmount);
        Assert.Null(t.PaidAt);
    }

    [Fact]
    public void Reopen_FullyPaid_ReturnsToPaid_WithMarkersRestored()
    {
        var t = Expense();
        t.Pay(100m, on: Today.AddDays(-1), by: OtherUserId);
        t.Cancel(UserId, Later);

        t.Reopen(UserId, Later);

        Assert.Equal(TransactionStatus.Paid, t.Status);
        Assert.Equal(Today.AddDays(-1), t.PaidAt);
        Assert.Equal(OtherUserId, t.PaidByUserId);
    }

    [Fact]
    public void Reopen_FullyReceivedIncome_ReturnsToReceived()
    {
        var t = Income(80m);
        t.Pay(80m);
        t.Cancel(UserId, Later);
        t.Reopen(UserId, Later);
        Assert.Equal(TransactionStatus.Received, t.Status);
    }

    [Fact]
    public void Reopen_NotCanceled_Fails() =>
        Assert.Throws<DomainException>(() => Expense().Reopen(UserId, Later));

    [Fact]
    public void CancelAndReopen_RecordAuditEntries()
    {
        var t = Expense();
        t.Cancel(UserId, Later);
        t.Reopen(UserId, Later);

        var actions = t.AuditEntries.Select(e => e.Action).ToList();
        Assert.Equal([TransactionAuditAction.Created, TransactionAuditAction.Canceled, TransactionAuditAction.Reopened], actions);
    }

    [Fact]
    public void Canceled_CannotBeEdited()
    {
        var t = Expense();
        t.Cancel(UserId, Later);
        Assert.Throws<DomainException>(() => Update(t, description: "Outra"));
    }

    // ------------------------------------------------------------ edição

    [Fact]
    public void Update_AmountBelowPaid_Fails()
    {
        var t = Expense();
        t.Pay(60m);

        var ex = Assert.Throws<DomainException>(() => Update(t, amount: 59.99m));

        Assert.Contains("menor que o total já pago", ex.Message, StringComparison.Ordinal);
        Assert.Equal(100m, t.Amount);
    }

    [Fact]
    public void Update_AmountEqualToPaid_SettlesTransaction()
    {
        var t = Expense();
        t.Pay(60m, on: Today.AddDays(-2), by: OtherUserId);

        Update(t, amount: 60m);

        Assert.Equal(TransactionStatus.Paid, t.Status);
        Assert.Equal(Today.AddDays(-2), t.PaidAt);
        Assert.Equal(OtherUserId, t.PaidByUserId);
    }

    [Fact]
    public void Update_IncreasingAmountOfSettled_ReturnsToPending()
    {
        var t = Expense();
        t.Pay(100m);

        Update(t, amount: 150m);

        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(50m, t.RemainingAmount);
        Assert.Null(t.PaidAt);
        Assert.Null(t.PaidByUserId);
    }

    [Fact]
    public void Update_DecreasingAmountOfPartial_StaysPending()
    {
        var t = Expense();
        t.Pay(30m);
        Update(t, amount: 50m);
        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal(20m, t.RemainingAmount);
    }

    [Fact]
    public void Update_CanSwitchBetweenPlannedAndPending_WhenNoPayments()
    {
        var t = Expense();
        Update(t, status: TransactionStatus.Planned);
        Assert.Equal(TransactionStatus.Planned, t.Status);
        Update(t, status: TransactionStatus.Pending);
        Assert.Equal(TransactionStatus.Pending, t.Status);
    }

    [Fact]
    public void Update_WithoutStatus_KeepsPlanned()
    {
        var t = Expense(status: TransactionStatus.Planned);
        Update(t, description: "Novo nome");
        Assert.Equal(TransactionStatus.Planned, t.Status);
    }

    [Fact]
    public void Update_PlannedRequested_WithPayments_Fails()
    {
        var t = Expense();
        t.Pay(10m);
        Assert.Throws<DomainException>(() => Update(t, status: TransactionStatus.Planned));
    }

    [Fact]
    public void Update_PendingRequested_WithPartialPayments_IsAccepted()
    {
        var t = Expense();
        t.Pay(10m);
        Update(t, status: TransactionStatus.Pending);
        Assert.Equal(TransactionStatus.Pending, t.Status);
    }

    [Theory]
    [InlineData(TransactionStatus.Paid)]
    [InlineData(TransactionStatus.Received)]
    [InlineData(TransactionStatus.Overdue)]
    [InlineData(TransactionStatus.Canceled)]
    public void Update_RejectsNonSelectableStatus(TransactionStatus status) =>
        Assert.Throws<DomainException>(() => Update(Expense(), status: status));

    [Fact]
    public void Update_TypeIsImmutable_CategoryOfOtherTypeIsRejected()
    {
        var t = Expense();
        var ex = Assert.Throws<DomainException>(() => Update(t, category: IncomeCategory()));
        Assert.Contains("mesmo tipo", ex.Message, StringComparison.Ordinal);
        Assert.Equal(TransactionType.Expense, t.Type);
    }

    [Fact]
    public void Update_RejectsDueDateBeforeTransactionDate()
    {
        var t = Expense(date: Today);
        Assert.Throws<DomainException>(() => Update(t, due: Today.AddDays(-1)));
    }

    [Fact]
    public void Update_ToInactiveCategory_Fails_ButKeepingTheSameInactiveCategoryIsAllowed()
    {
        var category = ExpenseCategory();
        var t = Expense(category: category);
        category.Deactivate();

        // Mesma categoria (agora inativa): editar outros campos continua possível.
        t.Update(UserId, "Outra descrição", 100m, category, null, null, t.TransactionDate, null, null, null, Later);
        Assert.Equal("Outra descrição", t.Description);

        // Trocar para outra categoria inativa não.
        var other = ExpenseCategory("Outra");
        other.Deactivate();
        Assert.Throws<DomainException>(() =>
            t.Update(UserId, "x", 100m, other, null, null, t.TransactionDate, null, null, null, Later));
    }

    [Fact]
    public void Update_RecordsOneAuditRowPerChangedField()
    {
        var category = ExpenseCategory();
        var t = Expense(due: Today.AddDays(5), category: category);
        var baseline = t.AuditEntries.Count;

        t.Update(UserId, "Compra editada", 120m, category, null, PaymentMethod.Pix, t.TransactionDate, Today.AddDays(9), "obs", null, Later);

        var entries = t.AuditEntries.Skip(baseline).ToList();
        Assert.All(entries, e => Assert.Equal(TransactionAuditAction.Updated, e.Action));
        var fields = entries.Select(e => e.Field).OrderBy(f => f, StringComparer.Ordinal).ToList();
        Assert.Equal(["amount", "description", "dueDate", "notes"], fields);
        var amount = entries.Single(e => e.Field == "amount");
        Assert.Equal("100.00", amount.OldValue);
        Assert.Equal("120.00", amount.NewValue);
        var due = entries.Single(e => e.Field == "dueDate");
        Assert.Equal(Today.AddDays(5).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), due.OldValue);
    }

    [Fact]
    public void Update_WithoutChanges_RecordsNothing()
    {
        var category = ExpenseCategory();
        var t = Expense(category: category);
        var baseline = t.AuditEntries.Count;

        t.Update(UserId, t.Description, t.Amount, category, null, t.PaymentMethod, t.TransactionDate, t.DueDate, t.Notes, null, Later);

        Assert.Equal(baseline, t.AuditEntries.Count);
    }

    [Fact]
    public void Update_AutomaticStatusChange_IsRecorded()
    {
        var t = Expense();
        t.Pay(60m);
        var baseline = t.AuditEntries.Count;

        Update(t, amount: 60m);

        var status = t.AuditEntries.Skip(baseline).Single(e => e.Field == "status");
        Assert.Equal("Pending", status.OldValue);
        Assert.Equal("Paid", status.NewValue);
    }

    // ------------------------------------------------------------ exclusão

    [Fact]
    public void Delete_WithoutPayments_IsAllowed() => Expense().EnsureCanBeDeleted();

    [Fact]
    public void Delete_Canceled_WithoutPayments_IsAllowed()
    {
        var t = Expense();
        t.Cancel(UserId, Later);
        t.EnsureCanBeDeleted();
    }

    [Fact]
    public void Delete_WithPayments_Fails()
    {
        var t = Expense();
        t.Pay(10m);
        Assert.Throws<DomainException>(t.EnsureCanBeDeleted);
    }

    [Fact]
    public void Delete_WithPayments_EvenIfCanceled_Fails()
    {
        var t = Expense();
        t.Pay(10m);
        t.Cancel(UserId, Later);
        Assert.Throws<DomainException>(t.EnsureCanBeDeleted);
    }

    [Fact]
    public void Delete_AfterRemovingAllPayments_IsAllowed()
    {
        var t = Expense();
        var p = t.Pay(10m);
        t.RemovePayment(UserId, p.Id, Later);
        t.EnsureCanBeDeleted();
    }

    // ------------------------------------------------------------ invariantes

    [Fact]
    public void Invariant_PaidAmountNeverExceedsAmount_AcrossManyOperations()
    {
        var t = Expense(100m);
        var random = new Random(42);
        for (var i = 0; i < 200; i++)
        {
            try
            {
                switch (random.Next(3))
                {
                    case 0:
                        t.Pay(Math.Round((decimal)random.NextDouble() * 60m + 0.01m, 2), at: Now.AddSeconds(i));
                        break;
                    case 1 when t.Payments.Count > 0:
                        t.RemovePayment(UserId, t.Payments.First().Id, Later);
                        break;
                    default:
                        Update(t, amount: Math.Round((decimal)random.NextDouble() * 150m + 1m, 2));
                        break;
                }
            }
            catch (DomainException)
            {
                // operações inválidas são recusadas sem corromper o estado
            }

            Assert.InRange(t.PaidAmount, 0m, t.Amount);
            Assert.Equal(t.Payments.Sum(p => p.Amount), t.PaidAmount);
            Assert.Equal(t.PaidAmount == t.Amount, t.IsSettled);
            Assert.Equal(t.IsSettled, t.PaidAt is not null);
            Assert.Equal(t.IsSettled, t.PaidByUserId is not null);
            Assert.NotEqual(TransactionStatus.Overdue, t.Status);
        }
    }

    [Fact]
    public void Presented_CanBeComputedFromPersistedValues()
    {
        Assert.Equal(
            TransactionStatus.Overdue,
            Transaction.ComputePresentedStatus(TransactionStatus.Pending, Today.AddDays(-1), 10m, Today));
        Assert.Equal(
            TransactionStatus.Paid,
            Transaction.ComputePresentedStatus(TransactionStatus.Paid, Today.AddDays(-1), 0m, Today));
    }
}
