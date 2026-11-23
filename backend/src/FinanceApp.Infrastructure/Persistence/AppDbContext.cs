using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Telegram;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceApp.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<TransactionPayment> TransactionPayments => Set<TransactionPayment>();

    public DbSet<TransactionAuditEntry> TransactionAuditEntries => Set<TransactionAuditEntry>();

    public DbSet<CreditCard> CreditCards => Set<CreditCard>();

    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();

    public DbSet<RecurrenceRule> RecurrenceRules => Set<RecurrenceRule>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();

    public DbSet<GroupTransactionSeries> GroupTransactionSeries => Set<GroupTransactionSeries>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<TelegramLink> TelegramLinks => Set<TelegramLink>();

    public DbSet<TelegramLinkCode> TelegramLinkCodes => Set<TelegramLinkCode>();

    public DbSet<TelegramPendingExpense> TelegramPendingExpenses => Set<TelegramPendingExpense>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            // Deadlock entre gravações concorrentes (ex.: duas gerações da mesma recorrência): é conflito de concorrência, não erro interno.
            throw new ConcurrencyConflictException(ex);
        }
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
