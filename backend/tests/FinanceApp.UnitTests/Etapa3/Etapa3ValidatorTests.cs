using FinanceApp.Application.CreditCards;
using FinanceApp.Application.Installments;
using FinanceApp.Application.Recurrences;
using FinanceApp.Application.Reports;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using FinanceApp.UnitTests.Support;
using FluentValidation.Results;
using static FinanceApp.UnitTests.Support.Fixtures;

namespace FinanceApp.UnitTests.Etapa3;

/// <summary>Validadores da Etapa 3 (mensagens em pt-BR e nomes de campo do contrato).</summary>
public class Etapa3ValidatorTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private readonly FakeClock clock = CreateClock();
    private readonly CreateCreditCardRequestValidator card = new();
    private readonly CreateInstallmentPlanRequestValidator plan = new();
    private readonly CreateRecurrenceRequestValidator rule;
    private readonly UpdateRecurrenceRequestValidator ruleUpdate = new();

    public Etapa3ValidatorTests() => rule = new CreateRecurrenceRequestValidator(clock);

    private static FakeClock CreateClock()
    {
        var c = new FakeClock(Now);
        c.SetToday(Today);
        return c;
    }

    private static bool Has(ValidationResult result, string property) => result.Errors.Any(e => e.PropertyName == property);

    private static CreateCreditCardRequest ValidCard() => new() { Name = "Visa", CreditLimit = 5000m, ClosingDay = 10, DueDay = 20 };

    private static CreateInstallmentPlanRequest ValidPlan() => new()
    {
        Description = "Sofá", TotalAmount = 100m, InstallmentCount = 3, FirstDate = Today, CategoryId = Guid.NewGuid(),
    };

    private static CreateRecurrenceRequest ValidRule() => new()
    {
        Description = "Streaming", Type = TransactionType.Expense, Amount = 30m, CategoryId = Guid.NewGuid(),
        Frequency = RecurrenceFrequency.Monthly, StartDate = Today,
    };

    // ---------------------------------------------------------------- cartão

    [Fact]
    public void Card_Valid_Passes() => Assert.True(card.Validate(ValidCard()).IsValid);

    [Fact]
    public void Card_Limit_ZeroIsAllowed_NegativeOrMoreThanTwoDecimalsIsNot()
    {
        Assert.True(card.Validate(new CreateCreditCardRequest { Name = "Visa", CreditLimit = 0m, ClosingDay = 1, DueDay = 31 }).IsValid);
        Assert.True(Has(card.Validate(new CreateCreditCardRequest { Name = "Visa", CreditLimit = -1m, ClosingDay = 1, DueDay = 1 }), "CreditLimit"));
        Assert.True(Has(card.Validate(new CreateCreditCardRequest { Name = "Visa", CreditLimit = 10.123m, ClosingDay = 1, DueDay = 1 }), "CreditLimit"));
        Assert.True(Has(card.Validate(new CreateCreditCardRequest { Name = "Visa", ClosingDay = 1, DueDay = 1 }), "CreditLimit"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(-3)]
    public void Card_Days_OutOfRange_Fail(int day)
    {
        var result = card.Validate(new CreateCreditCardRequest { Name = "Visa", CreditLimit = 1m, ClosingDay = day, DueDay = day });

        Assert.True(Has(result, "ClosingDay"));
        Assert.True(Has(result, "DueDay"));
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("entre 1 e 31", StringComparison.Ordinal));
    }

    [Fact]
    public void Card_Name_RequiredAndMaxLength()
    {
        Assert.True(Has(card.Validate(new CreateCreditCardRequest { Name = " ", CreditLimit = 1m, ClosingDay = 1, DueDay = 1 }), "Name"));
        Assert.True(Has(card.Validate(new CreateCreditCardRequest { Name = new string('a', 101), CreditLimit = 1m, ClosingDay = 1, DueDay = 1 }), "Name"));
    }

    // ---------------------------------------------------------------- parcelamento

    [Fact]
    public void Plan_Valid_Passes() => Assert.True(plan.Validate(ValidPlan()).IsValid);

    [Theory]
    [InlineData(1)]
    [InlineData(121)]
    [InlineData(0)]
    public void Plan_CountOutOfRange_Fails(int count)
    {
        var request = new CreateInstallmentPlanRequest
        {
            Description = "Sofá", TotalAmount = 100m, InstallmentCount = count, FirstDate = Today, CategoryId = Guid.NewGuid(),
        };

        Assert.True(Has(plan.Validate(request), "InstallmentCount"));
    }

    [Fact]
    public void Plan_Total_MustBeAtLeastOneCentPerInstallment_AndPositive()
    {
        var tiny = new CreateInstallmentPlanRequest { Description = "X", TotalAmount = 0.02m, InstallmentCount = 3, FirstDate = Today, CategoryId = Guid.NewGuid() };
        var zero = new CreateInstallmentPlanRequest { Description = "X", TotalAmount = 0m, InstallmentCount = 3, FirstDate = Today, CategoryId = Guid.NewGuid() };
        var cents = new CreateInstallmentPlanRequest { Description = "X", TotalAmount = 10.001m, InstallmentCount = 3, FirstDate = Today, CategoryId = Guid.NewGuid() };

        Assert.True(Has(plan.Validate(tiny), "TotalAmount"));
        Assert.True(Has(plan.Validate(zero), "TotalAmount"));
        Assert.True(Has(plan.Validate(cents), "TotalAmount"));
    }

    [Fact]
    public void Plan_FirstDate_RequiredAndSane()
    {
        Assert.True(Has(plan.Validate(new CreateInstallmentPlanRequest { Description = "X", TotalAmount = 10m, InstallmentCount = 2, CategoryId = Guid.NewGuid() }), "FirstDate"));
        Assert.True(Has(plan.Validate(new CreateInstallmentPlanRequest { Description = "X", TotalAmount = 10m, InstallmentCount = 2, FirstDate = D(1500, 1, 1), CategoryId = Guid.NewGuid() }), "FirstDate"));
    }

    [Fact]
    public void Plan_WithCard_RejectsAccountAndNonCardPaymentMethod()
    {
        var request = new CreateInstallmentPlanRequest
        {
            Description = "X", TotalAmount = 10m, InstallmentCount = 2, FirstDate = Today, CategoryId = Guid.NewGuid(),
            CreditCardId = Guid.NewGuid(), AccountId = Guid.NewGuid(), PaymentMethod = PaymentMethod.Pix,
        };

        var result = plan.Validate(request);

        Assert.True(Has(result, "AccountId"));
        Assert.True(Has(result, "PaymentMethod"));
    }

    // ---------------------------------------------------------------- recorrência

    [Fact]
    public void Rule_Valid_Passes() => Assert.True(rule.Validate(ValidRule()).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    [InlineData(-1)]
    public void Rule_Interval_OutOfRange_Fails(int interval)
    {
        var request = new CreateRecurrenceRequest
        {
            Description = "X", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(),
            Frequency = RecurrenceFrequency.Custom, Interval = interval, StartDate = Today,
        };

        Assert.True(Has(rule.Validate(request), "Interval"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public void Rule_Interval_Boundaries_Pass(int interval)
    {
        var request = new CreateRecurrenceRequest
        {
            Description = "X", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(),
            Frequency = RecurrenceFrequency.Custom, Interval = interval, StartDate = Today,
        };

        Assert.True(rule.Validate(request).IsValid);
    }

    [Fact]
    public void Rule_EndDate_BeforeStart_Fails()
    {
        var request = new CreateRecurrenceRequest
        {
            Description = "X", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(),
            Frequency = RecurrenceFrequency.Monthly, StartDate = Today, EndDate = Today.AddDays(-1),
        };

        Assert.True(Has(rule.Validate(request), "EndDate"));
    }

    [Fact]
    public void Rule_StartDate_RequiredAndNotMoreThan366DaysInThePast()
    {
        var missing = new CreateRecurrenceRequest { Description = "X", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(), Frequency = RecurrenceFrequency.Monthly };
        var tooOld = new CreateRecurrenceRequest { Description = "X", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(), Frequency = RecurrenceFrequency.Monthly, StartDate = Today.AddDays(-367) };
        var limit = new CreateRecurrenceRequest { Description = "X", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(), Frequency = RecurrenceFrequency.Monthly, StartDate = Today.AddDays(-366) };

        Assert.True(Has(rule.Validate(missing), "StartDate"));
        Assert.True(Has(rule.Validate(tooOld), "StartDate"));
        Assert.True(rule.Validate(limit).IsValid);
    }

    [Fact]
    public void Rule_RequiresTypeFrequencyAndPositiveAmount()
    {
        var result = rule.Validate(new CreateRecurrenceRequest { Description = "", Amount = 0m, StartDate = Today });

        Assert.True(Has(result, "Description"));
        Assert.True(Has(result, "Type"));
        Assert.True(Has(result, "Amount"));
        Assert.True(Has(result, "Frequency"));
        Assert.True(Has(result, "CategoryId"));
    }

    [Fact]
    public void Rule_IncomeCannotUseCard_AccountNotAllowedWithCard()
    {
        var withCard = new CreateRecurrenceRequest
        {
            Description = "X", Type = TransactionType.Income, Amount = 1m, CategoryId = Guid.NewGuid(),
            Frequency = RecurrenceFrequency.Monthly, StartDate = Today, CreditCardId = Guid.NewGuid(), AccountId = Guid.NewGuid(),
        };

        var result = rule.Validate(withCard);

        Assert.True(Has(result, "CreditCardId"));
        Assert.True(Has(result, "AccountId"));
    }

    [Fact]
    public void RuleUpdate_Valid_Passes_AndEndDateMustBeSane()
    {
        var ok = new UpdateRecurrenceRequest { Description = "X", Amount = 1m, CategoryId = Guid.NewGuid(), Frequency = RecurrenceFrequency.Weekly };
        var bad = new UpdateRecurrenceRequest { Description = "X", Amount = 1m, CategoryId = Guid.NewGuid(), Frequency = RecurrenceFrequency.Weekly, EndDate = D(1500, 1, 1) };

        Assert.True(ruleUpdate.Validate(ok).IsValid);
        Assert.True(Has(ruleUpdate.Validate(bad), "EndDate"));
    }

    // ---------------------------------------------------------------- relatórios

    [Fact]
    public void ReportPeriod_FromToRequired_Ordered_AndMax366Days()
    {
        var validator = new SummaryQueryValidator();

        Assert.True(Has(validator.Validate(new SummaryQuery()), "From"));
        Assert.True(Has(validator.Validate(new SummaryQuery()), "To"));
        Assert.True(Has(validator.Validate(new SummaryQuery { From = D(2026, 6, 30), To = D(2026, 6, 1) }), "To"));
        Assert.True(validator.Validate(new SummaryQuery { From = D(2026, 1, 1), To = D(2027, 1, 1) }).IsValid);   // 366 dias
        Assert.True(Has(validator.Validate(new SummaryQuery { From = D(2026, 1, 1), To = D(2027, 1, 2) }), "To")); // 367 dias
    }

    [Fact]
    public void ReportTypedAndEvolutionAndInstallments_Validators()
    {
        var typed = new TypedPeriodQueryValidator();
        var evolution = new MonthlyEvolutionQueryValidator();
        var installments = new InstallmentsReportQueryValidator();

        Assert.True(typed.Validate(new TypedPeriodQuery { From = D(2026, 6, 1), To = D(2026, 6, 30), Type = TransactionType.Income }).IsValid);
        Assert.True(Has(typed.Validate(new TypedPeriodQuery { From = D(2026, 6, 1), To = D(2026, 6, 30), Type = (TransactionType)99 }), "Type"));
        Assert.True(evolution.Validate(new MonthlyEvolutionQuery { Months = 36, EndMonth = "2026-06" }).IsValid);
        Assert.True(Has(evolution.Validate(new MonthlyEvolutionQuery { Months = 37 }), "Months"));
        Assert.True(Has(evolution.Validate(new MonthlyEvolutionQuery { Months = 0 }), "Months"));
        Assert.True(Has(evolution.Validate(new MonthlyEvolutionQuery { EndMonth = "2026-13" }), "EndMonth"));
        Assert.True(installments.Validate(new InstallmentsReportQuery { Months = 12 }).IsValid);
        Assert.True(Has(installments.Validate(new InstallmentsReportQuery { Months = 0 }), "Months"));
    }
}
