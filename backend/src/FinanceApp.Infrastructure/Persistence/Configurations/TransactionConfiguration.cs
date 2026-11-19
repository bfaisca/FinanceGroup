using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", t =>
        {
            t.HasCheckConstraint("ck_transactions_amount_positive", "amount > 0");
            t.HasCheckConstraint("ck_transactions_paid_amount_range", "paid_amount >= 0 AND paid_amount <= amount");
            t.HasCheckConstraint("ck_transactions_due_date_after_date", "due_date IS NULL OR due_date >= transaction_date");
            t.HasCheckConstraint(
                "ck_transactions_status_settlement",
                "status IN ('Planned','Pending','Paid','Received','Canceled') " +
                "AND (status = 'Canceled' OR ((status IN ('Paid','Received')) = (paid_amount = amount))) " +
                "AND (status <> 'Paid' OR type = 'Expense') " +
                "AND (status <> 'Received' OR type = 'Income') " +
                "AND (paid_at IS NULL OR status IN ('Paid','Received')) " +
                "AND (paid_by_user_id IS NULL OR status IN ('Paid','Received'))");
        });
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Description).HasMaxLength(Transaction.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.PaidAmount).HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(t => t.PaymentMethod).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(t => t.TransactionDate).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(Transaction.NotesMaxLength);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.PaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Payments)
            .WithOne()
            .HasForeignKey(p => p.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(t => t.AuditEntries)
            .WithOne()
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.AuditEntries).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Listagens, dashboard e filtros.
        builder.HasIndex(t => new { t.CreatedByUserId, t.TransactionDate });
        builder.HasIndex(t => t.AccountId);
        builder.HasIndex(t => t.CategoryId);
        builder.HasIndex(t => new { t.CreatedByUserId, t.DueDate })
            .HasFilter("status IN ('Planned','Pending')")
            .HasDatabaseName("ix_transactions_created_by_user_id_due_date_open");

        // Pagamentos simultâneos: o segundo a gravar recebe conflito (409) em vez de ultrapassar o valor.
        builder.HasXminConcurrencyToken();
    }
}

internal sealed class TransactionPaymentConfiguration : IEntityTypeConfiguration<TransactionPayment>
{
    public void Configure(EntityTypeBuilder<TransactionPayment> builder)
    {
        builder.ToTable("transaction_payments", t => t.HasCheckConstraint("ck_transaction_payments_amount_positive", "amount > 0"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.PaymentDate).IsRequired();
        builder.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Account)
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.TransactionId);
        builder.HasIndex(p => p.AccountId);
    }
}

internal sealed class TransactionAuditEntryConfiguration : IEntityTypeConfiguration<TransactionAuditEntry>
{
    public void Configure(EntityTypeBuilder<TransactionAuditEntry> builder)
    {
        builder.ToTable("transaction_audit_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Sequence).ValueGeneratedOnAdd().UseIdentityByDefaultColumn();
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Field).HasMaxLength(TransactionAuditEntry.FieldMaxLength);
        builder.Property(e => e.OldValue).HasMaxLength(TransactionAuditEntry.ValueMaxLength);
        builder.Property(e => e.NewValue).HasMaxLength(TransactionAuditEntry.ValueMaxLength);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TransactionId, e.CreatedAt });
    }
}
