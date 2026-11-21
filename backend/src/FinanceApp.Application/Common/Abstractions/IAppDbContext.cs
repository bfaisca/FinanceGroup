using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FinanceApp.Application.Common.Abstractions;

/// <summary>
/// Unidade de trabalho da aplicação. Expõe os conjuntos de entidades; repositórios
/// só serão introduzidos quando houver ganho real (ver ARCHITECTURE.md).
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    DbSet<Account> Accounts { get; }

    DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots { get; }

    DbSet<Category> Categories { get; }

    DbSet<Transaction> Transactions { get; }

    DbSet<TransactionPayment> TransactionPayments { get; }

    DbSet<TransactionAuditEntry> TransactionAuditEntries { get; }

    DbSet<CreditCard> CreditCards { get; }

    DbSet<InstallmentPlan> InstallmentPlans { get; }

    DbSet<RecurrenceRule> RecurrenceRules { get; }

    DbSet<Group> Groups { get; }

    DbSet<GroupMember> GroupMembers { get; }

    DbSet<GroupInvitation> GroupInvitations { get; }

    DbSet<Notification> Notifications { get; }

    /// <summary>Rastreador de alterações (usado para descartar entidades pendentes após uma disputa de unicidade).</summary>
    ChangeTracker ChangeTracker { get; }

    /// <exception cref="Exceptions.UniqueConstraintViolationException">Violação de índice único.</exception>
    /// <exception cref="Exceptions.ConcurrencyConflictException">Conflito de concorrência otimista.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
