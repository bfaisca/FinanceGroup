using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Installments;
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
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
