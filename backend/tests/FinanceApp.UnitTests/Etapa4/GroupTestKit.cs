using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Emails;
using FinanceApp.Application.Groups;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Users;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Security;
using FinanceApp.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace FinanceApp.UnitTests.Etapa4;

/// <summary>Serviços de grupo reais sobre um DbContext em memória, com usuário autenticado trocável (<see cref="As"/>).</summary>
internal sealed class GroupTestKit : IDisposable
{
    public const string FrontendUrl = "http://front.test";

    public GroupTestKit()
    {
        Db = TestDb.Create();
        Clock = new FakeClock(new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.Zero));
        Clock.SetToday(new DateOnly(2026, 11, 18));
        Access = new GroupAccess(Db, CurrentUser);

        var appOptions = MsOptions.Create(new AppOptions { FrontendBaseUrl = FrontendUrl });
        Groups = new GroupService(
            Db, CurrentUser, Clock, Access,
            new CreateGroupRequestValidator(), new UpdateGroupRequestValidator(), new ChangeMemberRoleRequestValidator());
        Invitations = new GroupInvitationService(
            Db, CurrentUser, Clock, Access, Tokens, Emails, new GroupInvitationEmailFactory(appOptions),
            new CreateInvitationRequestValidator(), NullLogger<GroupInvitationService>.Instance);
        Transactions = new GroupTransactionService(
            Db, Clock, Access,
            new GroupTransactionListQueryValidator(), new CreateGroupTransactionRequestValidator(),
            new UpdateGroupTransactionRequestValidator(), new AddPaymentRequestValidator(Clock));
        Dashboard = new GroupDashboardService(Db, Access, new GroupDashboardQueryValidator());
    }

    public AppDbContext Db { get; }

    public FakeClock Clock { get; }

    public FakeCurrentUser CurrentUser { get; } = new();

    public CapturingEmailSender Emails { get; } = new();

    public SecureTokenService Tokens { get; } = new();

    public GroupAccess Access { get; }

    public GroupService Groups { get; }

    public GroupInvitationService Invitations { get; }

    public GroupTransactionService Transactions { get; }

    public GroupDashboardService Dashboard { get; }

    public DateOnly Today => Clock.Today;

    public void Dispose() => Db.Dispose();

    /// <summary>Cria o usuário e passa a agir como ele.</summary>
    public async Task<User> NewUserAsync(string name, string? email = null)
    {
        var user = User.Create(name, email ?? $"{name.ToLowerInvariant()}@exemplo.com", "hash", Clock.UtcNow);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        CurrentUser.UserId = user.Id;
        return user;
    }

    public void As(User user) => CurrentUser.UserId = user.Id;

    /// <summary>Convida o usuário (agindo como <paramref name="admin"/>) e o faz aceitar; devolve o token usado.</summary>
    public async Task<string> JoinAsync(Guid groupId, User admin, User invitee)
    {
        As(admin);
        await Invitations.CreateAsync(groupId, new CreateInvitationRequest { Email = invitee.Email }, CancellationToken.None);
        var token = LastToken(invitee.Email);
        As(invitee);
        await Invitations.AcceptAsync(token, CancellationToken.None);
        return token;
    }

    public string LastToken(string address)
    {
        var body = Emails.Sent.Last(m => m.ToAddress == address).TextBody;
        var marker = $"{FrontendUrl}/invitations/";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = body.IndexOfAny([' ', '\n', '\r'], start);
        return body[start..(end < 0 ? body.Length : end)];
    }

    public Guid ExpenseCategoryId(string name = "Alimentação") =>
        Db.Categories.AsNoTracking().First(c => c.IsDefault && c.Name == name && c.Type == CategoryType.Expense).Id;

    public Guid IncomeCategoryId(string name = "Salário") =>
        Db.Categories.AsNoTracking().First(c => c.IsDefault && c.Name == name && c.Type == CategoryType.Income).Id;

    public Task<GroupTransactionDetailDto> CreateTxAsync(
        Guid groupId, string description, decimal amount, DateOnly? date = null, TransactionTypeArg type = TransactionTypeArg.Expense, Guid? categoryId = null) =>
        Transactions.CreateAsync(
            groupId,
            new CreateGroupTransactionRequest
            {
                Description = description,
                Type = type == TransactionTypeArg.Expense ? FinanceApp.Domain.Transactions.TransactionType.Expense : FinanceApp.Domain.Transactions.TransactionType.Income,
                Amount = amount,
                CategoryId = categoryId ?? (type == TransactionTypeArg.Expense ? ExpenseCategoryId() : IncomeCategoryId()),
                TransactionDate = date ?? Today,
            },
            CancellationToken.None);

    public Task<GroupTransactionDetailDto> PayAsync(Guid groupId, Guid txId, decimal amount, DateOnly? date = null) =>
        Transactions.AddPaymentAsync(
            groupId, txId, new AddPaymentRequest { Amount = amount, PaymentDate = date ?? Today }, CancellationToken.None);
}

internal enum TransactionTypeArg
{
    Expense,
    Income,
}
