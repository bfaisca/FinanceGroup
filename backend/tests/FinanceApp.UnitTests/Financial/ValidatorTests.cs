using FinanceApp.Application.Dashboard;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Financial;

public class FinancialValidatorTests
{
    private readonly FakeClock clock = CreateClock();
    private readonly CreateTransactionRequestValidator create;
    private readonly UpdateTransactionRequestValidator update = new();
    private readonly AddPaymentRequestValidator payment;
    private readonly TransactionListQueryValidator list = new();
    private readonly DashboardQueryValidator dashboard = new();

    public FinancialValidatorTests()
    {
        create = new CreateTransactionRequestValidator(clock);
        payment = new AddPaymentRequestValidator(clock);
    }

    private static FakeClock CreateClock()
    {
        var c = new FakeClock(Now);
        c.SetToday(Today);
        return c;
    }

    private static CreateTransactionRequest ValidCreate() => new()
    {
        Description = "Mercado",
        Type = TransactionType.Expense,
        Amount = 10.50m,
        CategoryId = Guid.NewGuid(),
        TransactionDate = Today,
    };

    private static string[] ErrorsFor<T>(FluentValidation.Results.ValidationResult result, string property) =>
        result.Errors.Where(e => e.PropertyName == property).Select(e => e.ErrorMessage).ToArray();

    // ------------------------------------------------------------ criação

    [Fact]
    public void Create_Valid_Passes() => Assert.True(create.Validate(ValidCreate()).IsValid);

    [Fact]
    public void Create_Valid_WithOptionalFields_Passes()
    {
        var request = new CreateTransactionRequest
        {
            Description = new string('a', 200),
            Type = TransactionType.Income,
            Amount = MoneyRules.MaxAmount,
            CategoryId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.BankSlip,
            Status = TransactionStatus.Planned,
            TransactionDate = Today,
            DueDate = Today,
            Notes = new string('n', 1000),
        };
        Assert.True(create.Validate(request).IsValid);
    }

    [Fact]
    public void Create_Description_Required_And_Max200()
    {
        Assert.Equal(["A descrição é obrigatória."], ErrorsFor<object>(create.Validate(Clone(ValidCreate(), description: "")), "Description"));
        Assert.Single(ErrorsFor<object>(create.Validate(Clone(ValidCreate(), description: new string('a', 201))), "Description"));
    }

    [Theory]
    [InlineData(0, "O valor deve ser maior que zero.")]
    [InlineData(-1, "O valor deve ser maior que zero.")]
    [InlineData(1.005, "O valor deve ter no máximo 2 casas decimais.")]
    public void Create_Amount_Rules(double amount, string message) =>
        Assert.Equal([message], ErrorsFor<object>(create.Validate(Clone(ValidCreate(), amount: (decimal)amount)), "Amount"));

    [Fact]
    public void Create_Amount_AboveLimit_IsInvalid() =>
        Assert.Equal(
            ["O valor excede o limite permitido."],
            ErrorsFor<object>(create.Validate(Clone(ValidCreate(), amount: MoneyRules.MaxAmount + 0.01m)), "Amount"));

    [Fact]
    public void Create_Type_Required()
    {
        var request = new CreateTransactionRequest { Description = "x", Amount = 1, CategoryId = Guid.NewGuid(), TransactionDate = Today };
        Assert.Equal(["O tipo da movimentação é obrigatório."], ErrorsFor<object>(create.Validate(request), "Type"));
    }

    [Fact]
    public void Create_Category_And_TransactionDate_Required()
    {
        var result = create.Validate(new CreateTransactionRequest { Description = "x", Type = TransactionType.Expense, Amount = 1 });
        Assert.Equal(["A categoria é obrigatória."], ErrorsFor<object>(result, "CategoryId"));
        Assert.Equal(["A data da movimentação é obrigatória."], ErrorsFor<object>(result, "TransactionDate"));
    }

    [Fact]
    public void Create_DueDate_BeforeTransactionDate_IsInvalid()
    {
        var request = new CreateTransactionRequest
        {
            Description = "x", Type = TransactionType.Expense, Amount = 1, CategoryId = Guid.NewGuid(),
            TransactionDate = Today, DueDate = Today.AddDays(-1),
        };
        Assert.Equal(["O vencimento não pode ser anterior à data da movimentação."], ErrorsFor<object>(create.Validate(request), "DueDate"));
    }

    [Theory]
    [InlineData(TransactionStatus.Paid)]
    [InlineData(TransactionStatus.Received)]
    [InlineData(TransactionStatus.Overdue)]
    [InlineData(TransactionStatus.Canceled)]
    public void Create_Status_OnlyPlannedOrPending(TransactionStatus status)
    {
        var request = new CreateTransactionRequest
        {
            Description = "x", Type = TransactionType.Expense, Amount = 1, CategoryId = Guid.NewGuid(), TransactionDate = Today, Status = status,
        };
        Assert.Single(ErrorsFor<object>(create.Validate(request), "Status"));
    }

    [Fact]
    public void Create_Notes_Max1000()
    {
        var request = new CreateTransactionRequest
        {
            Description = "x", Type = TransactionType.Expense, Amount = 1, CategoryId = Guid.NewGuid(), TransactionDate = Today, Notes = new string('n', 1001),
        };
        Assert.Single(ErrorsFor<object>(create.Validate(request), "Notes"));
    }

    [Fact]
    public void Create_InvalidEnumValues_AreRejected()
    {
        var request = new CreateTransactionRequest
        {
            Description = "x", Type = (TransactionType)99, Amount = 1, CategoryId = Guid.NewGuid(), TransactionDate = Today,
            PaymentMethod = (PaymentMethod)99,
        };
        var result = create.Validate(request);
        Assert.Single(ErrorsFor<object>(result, "Type"));
        Assert.Single(ErrorsFor<object>(result, "PaymentMethod"));
    }

    // ------------------------------------------------------------ atualização

    [Fact]
    public void Update_Valid_Passes() =>
        Assert.True(update.Validate(new UpdateTransactionRequest
        {
            Description = "x", Amount = 1, CategoryId = Guid.NewGuid(), TransactionDate = Today, DueDate = Today.AddDays(1), Status = TransactionStatus.Pending,
        }).IsValid);

    [Fact]
    public void Update_Invalid_ReportsEachField()
    {
        var result = update.Validate(new UpdateTransactionRequest { Description = "", Amount = 0, Status = TransactionStatus.Paid });
        Assert.NotEmpty(ErrorsFor<object>(result, "Description"));
        Assert.NotEmpty(ErrorsFor<object>(result, "Amount"));
        Assert.NotEmpty(ErrorsFor<object>(result, "CategoryId"));
        Assert.NotEmpty(ErrorsFor<object>(result, "TransactionDate"));
        Assert.NotEmpty(ErrorsFor<object>(result, "Status"));
    }

    // ------------------------------------------------------------ pagamento

    [Fact]
    public void Payment_Valid_Passes() =>
        Assert.True(payment.Validate(new AddPaymentRequest { Amount = 10m, PaymentDate = Today, PaymentMethod = PaymentMethod.Pix }).IsValid);

    [Fact]
    public void Payment_FutureDate_IsInvalid() =>
        Assert.Equal(
            ["A data do pagamento não pode ser futura."],
            ErrorsFor<object>(payment.Validate(new AddPaymentRequest { Amount = 10m, PaymentDate = Today.AddDays(1) }), "PaymentDate"));

    [Fact]
    public void Payment_DateIsRequired() =>
        Assert.Equal(
            ["A data do pagamento é obrigatória."],
            ErrorsFor<object>(payment.Validate(new AddPaymentRequest { Amount = 10m }), "PaymentDate"));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(0.001)]
    public void Payment_Amount_Invalid(double amount) =>
        Assert.Single(ErrorsFor<object>(payment.Validate(new AddPaymentRequest { Amount = (decimal)amount, PaymentDate = Today }), "Amount"));

    [Fact]
    public void Payment_Today_IsInjected()
    {
        clock.SetToday(Today.AddDays(5));
        Assert.True(payment.Validate(new AddPaymentRequest { Amount = 1m, PaymentDate = Today.AddDays(5) }).IsValid);
        Assert.False(payment.Validate(new AddPaymentRequest { Amount = 1m, PaymentDate = Today.AddDays(6) }).IsValid);
    }

    // ------------------------------------------------------------ listagem

    [Fact]
    public void List_Defaults_Pass() => Assert.True(list.Validate(new TransactionListQuery()).IsValid);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void List_PagingBounds(int page, int pageSize) =>
        Assert.False(list.Validate(new TransactionListQuery { Page = page, PageSize = pageSize }).IsValid);

    [Theory]
    [InlineData("transactionDate")]
    [InlineData("-transactionDate")]
    [InlineData("dueDate")]
    [InlineData("-amount")]
    [InlineData("description")]
    [InlineData("-createdAt")]
    [InlineData("TransactionDate")]
    public void List_Sort_Whitelist_Accepts(string sort) =>
        Assert.True(list.Validate(new TransactionListQuery { Sort = sort }).IsValid);

    [Theory]
    [InlineData("status")]
    [InlineData("-id")]
    [InlineData("amount; drop table")]
    public void List_Sort_Rejects(string sort) =>
        Assert.False(list.Validate(new TransactionListQuery { Sort = sort }).IsValid);

    [Fact]
    public void List_InvertedRanges_AreInvalid()
    {
        Assert.False(list.Validate(new TransactionListQuery { From = Today, To = Today.AddDays(-1) }).IsValid);
        Assert.False(list.Validate(new TransactionListQuery { DueFrom = Today, DueTo = Today.AddDays(-1) }).IsValid);
        Assert.True(list.Validate(new TransactionListQuery { From = Today, To = Today }).IsValid);
    }

    // ------------------------------------------------------------ dashboard

    [Fact]
    public void Dashboard_FromAndTo_AreRequired()
    {
        var result = dashboard.Validate(new DashboardQuery());
        Assert.Single(ErrorsFor<object>(result, "From"));
        Assert.Single(ErrorsFor<object>(result, "To"));
    }

    [Fact]
    public void Dashboard_FromAfterTo_IsInvalid() =>
        Assert.False(dashboard.Validate(new DashboardQuery { From = Today, To = Today.AddDays(-1) }).IsValid);

    [Fact]
    public void Dashboard_Range_Limits()
    {
        var from = new DateOnly(2026, 1, 1);
        Assert.True(dashboard.Validate(new DashboardQuery { From = from, To = from }).IsValid);
        Assert.True(dashboard.Validate(new DashboardQuery { From = from, To = from.AddDays(365) }).IsValid); // 366 dias
        Assert.False(dashboard.Validate(new DashboardQuery { From = from, To = from.AddDays(366) }).IsValid); // 367 dias
    }

    private static CreateTransactionRequest Clone(CreateTransactionRequest r, string? description = null, decimal? amount = null) => new()
    {
        Description = description ?? r.Description,
        Type = r.Type,
        Amount = amount ?? r.Amount,
        CategoryId = r.CategoryId,
        TransactionDate = r.TransactionDate,
    };
}
