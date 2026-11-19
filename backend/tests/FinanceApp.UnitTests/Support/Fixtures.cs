using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.UnitTests.Support;

/// <summary>Dados de teste do núcleo financeiro. "Hoje" fixo: 2026-11-18.</summary>
internal static class Fixtures
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly DateOnly Today = new(2026, 11, 18);
    public static readonly DateTimeOffset Now = new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero);

    public static Category ExpenseCategory(string name = "Mercado") =>
        Category.CreateCustom(UserId, name, CategoryType.Expense, null, null, null);

    public static Category IncomeCategory(string name = "Bônus") =>
        Category.CreateCustom(UserId, name, CategoryType.Income, null, null, null);

    public static Account NewAccount(string name = "Conta Corrente") =>
        Account.Create(UserId, name, "Banco X", AccountType.Checking, Now);

    public static Transaction Expense(
        decimal amount = 100m,
        DateOnly? due = null,
        TransactionStatus status = TransactionStatus.Pending,
        DateOnly? date = null,
        Category? category = null,
        Account? account = null) =>
        Transaction.Create(
            UserId, "Compra", TransactionType.Expense, amount, category ?? ExpenseCategory(), account, PaymentMethod.Pix,
            status, date ?? Today, due, null, Now);

    public static Transaction Income(
        decimal amount = 100m,
        DateOnly? due = null,
        TransactionStatus status = TransactionStatus.Pending,
        DateOnly? date = null,
        Category? category = null) =>
        Transaction.Create(
            UserId, "Venda", TransactionType.Income, amount, category ?? IncomeCategory(), null, null,
            status, date ?? Today, due, null, Now);

    public static TransactionPayment Pay(this Transaction t, decimal amount, DateOnly? on = null, Guid? by = null, DateTimeOffset? at = null) =>
        t.AddPayment(by ?? UserId, amount, on ?? Today, Today, null, null, at ?? Now);
}
