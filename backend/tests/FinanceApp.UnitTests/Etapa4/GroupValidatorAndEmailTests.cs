using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Emails;
using FinanceApp.Application.Groups;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Transactions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace FinanceApp.UnitTests.Etapa4;

public class GroupValidatorAndEmailTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Casa", true)]
    public void CreateGroup_NameRequired(string name, bool valid) =>
        Assert.Equal(valid, new CreateGroupRequestValidator().Validate(new CreateGroupRequest { Name = name }).IsValid);

    [Fact]
    public void CreateGroup_LimitsAreEnforced_WithPtBrMessages()
    {
        var result = new CreateGroupRequestValidator().Validate(
            new CreateGroupRequest { Name = new string('a', 101), Description = new string('b', 501) });

        Assert.Contains(result.Errors, e => e.PropertyName == "Name" && e.ErrorMessage.Contains("1 e 100", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.PropertyName == "Description" && e.ErrorMessage.Contains("500", StringComparison.Ordinal));
        Assert.True(new CreateGroupRequestValidator().Validate(
            new CreateGroupRequest { Name = new string('a', 100), Description = new string('b', 500) }).IsValid);
    }

    [Theory]
    [InlineData(GroupMemberRole.Admin, true)]
    [InlineData(GroupMemberRole.Member, true)]
    [InlineData(GroupMemberRole.Owner, false)]
    public void ChangeRole_OnlyAdminOrMember(GroupMemberRole role, bool valid) =>
        Assert.Equal(valid, new ChangeMemberRoleRequestValidator().Validate(new ChangeMemberRoleRequest { Role = role }).IsValid);

    [Fact]
    public void ChangeRole_RoleIsRequired() =>
        Assert.False(new ChangeMemberRoleRequestValidator().Validate(new ChangeMemberRoleRequest()).IsValid);

    [Theory]
    [InlineData("ana@exemplo.com", true)]
    [InlineData("ana", false)]
    [InlineData("", false)]
    public void Invitation_EmailFormat(string email, bool valid) =>
        Assert.Equal(valid, new CreateInvitationRequestValidator().Validate(new CreateInvitationRequest { Email = email }).IsValid);

    private static CreateGroupTransactionRequest ValidCreate() => new()
    {
        Description = "Mercado",
        Type = TransactionType.Expense,
        Amount = 10m,
        CategoryId = Guid.NewGuid(),
        TransactionDate = Today,
    };

    [Fact]
    public void GroupTransaction_ValidRequest_Passes() =>
        Assert.True(new CreateGroupTransactionRequestValidator().Validate(ValidCreate()).IsValid);

    [Fact]
    public void GroupTransaction_RejectsInvalidFields()
    {
        var validator = new CreateGroupTransactionRequestValidator();

        Assert.False(validator.Validate(new CreateGroupTransactionRequest { Description = "x", Type = TransactionType.Expense, Amount = 0m, CategoryId = Guid.NewGuid(), TransactionDate = Today }).IsValid);
        Assert.False(validator.Validate(new CreateGroupTransactionRequest { Description = "", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(), TransactionDate = Today }).IsValid);
        Assert.False(validator.Validate(new CreateGroupTransactionRequest { Description = "x", Type = null, Amount = 1m, CategoryId = Guid.NewGuid(), TransactionDate = Today }).IsValid);
        Assert.False(validator.Validate(new CreateGroupTransactionRequest { Description = "x", Type = TransactionType.Expense, Amount = 1.234m, CategoryId = Guid.NewGuid(), TransactionDate = Today }).IsValid);
        Assert.False(validator.Validate(new CreateGroupTransactionRequest { Description = "x", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.Empty, TransactionDate = Today }).IsValid);
        Assert.False(validator.Validate(new CreateGroupTransactionRequest { Description = "x", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(), TransactionDate = null }).IsValid);
        Assert.False(validator.Validate(new CreateGroupTransactionRequest { Description = "x", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(), TransactionDate = Today, DueDate = Today.AddDays(-1) }).IsValid);
        Assert.False(validator.Validate(new CreateGroupTransactionRequest { Description = "x", Type = TransactionType.Expense, Amount = 1m, CategoryId = Guid.NewGuid(), TransactionDate = Today, Status = TransactionStatus.Paid }).IsValid);
    }

    [Fact]
    public void GroupTransactionList_ValidatesPagingAndSort()
    {
        var validator = new GroupTransactionListQueryValidator();

        Assert.True(validator.Validate(new GroupTransactionListQuery()).IsValid);
        Assert.True(validator.Validate(new GroupTransactionListQuery { Sort = "-amount", PageSize = 100 }).IsValid);
        Assert.False(validator.Validate(new GroupTransactionListQuery { PageSize = 101 }).IsValid);
        Assert.False(validator.Validate(new GroupTransactionListQuery { Page = 0 }).IsValid);
        Assert.False(validator.Validate(new GroupTransactionListQuery { Sort = "accountId" }).IsValid);
        Assert.False(validator.Validate(new GroupTransactionListQuery { From = Today, To = Today.AddDays(-1) }).IsValid);
    }

    [Fact]
    public void GroupDashboardQuery_ValidatesPeriodMonthsAndEndMonth()
    {
        var validator = new GroupDashboardQueryValidator();

        Assert.True(validator.Validate(new GroupDashboardQuery { From = Today, To = Today }).IsValid);
        Assert.True(validator.Validate(new GroupDashboardQuery { From = Today, To = Today.AddDays(365), Months = 36, EndMonth = "2026-11" }).IsValid);
        Assert.False(validator.Validate(new GroupDashboardQuery { From = Today, To = Today.AddDays(366) }).IsValid);
        Assert.False(validator.Validate(new GroupDashboardQuery { From = Today, To = Today.AddDays(-1) }).IsValid);
        Assert.False(validator.Validate(new GroupDashboardQuery { To = Today }).IsValid);
        Assert.False(validator.Validate(new GroupDashboardQuery { From = Today, To = Today, Months = 0 }).IsValid);
        Assert.False(validator.Validate(new GroupDashboardQuery { From = Today, To = Today, Months = 37 }).IsValid);
        Assert.False(validator.Validate(new GroupDashboardQuery { From = Today, To = Today, EndMonth = "2026-13" }).IsValid);
    }

    [Fact]
    public void InvitationEmail_IsInPortuguese_HasTheFrontendLink_AndEncodesHtml()
    {
        var factory = new GroupInvitationEmailFactory(MsOptions.Create(new AppOptions { FrontendBaseUrl = "http://front.test/" }));

        var message = factory.Create("ana@exemplo.com", "Maria <b>", "Casa & Cia", "TOKEN-abc_123", new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("ana@exemplo.com", message.ToAddress);
        Assert.Contains("Convite para o grupo", message.Subject, StringComparison.Ordinal);
        Assert.Contains("http://front.test/invitations/TOKEN-abc_123", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("25/11/2026", message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Casa &amp; Cia", message.HtmlBody, StringComparison.Ordinal);
        Assert.Equal("http://front.test/invitations/TOKEN-abc_123", factory.BuildInvitationLink("TOKEN-abc_123"));
    }
}
