using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Recurrences;
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

            // Etapa 3: cartão só em despesas, sem conta e sempre com forma de pagamento CreditCard (D-025).
            t.HasCheckConstraint(
                "ck_transactions_credit_card_rules",
                "credit_card_id IS NULL OR (type = 'Expense' AND account_id IS NULL AND payment_method = 'CreditCard')");
            // Parcela e ocorrência são mutuamente exclusivas e carregam seus campos de origem de forma coerente.
            t.HasCheckConstraint(
                "ck_transactions_origin_consistency",
                "(installment_plan_id IS NULL) = (installment_number IS NULL) " +
                "AND (recurrence_rule_id IS NULL) = (occurrence_date IS NULL) " +
                "AND NOT (installment_plan_id IS NOT NULL AND recurrence_rule_id IS NOT NULL)");
            // O teto exato é a contagem do plano (outra tabela; garantido pela aplicação): aqui vale o limite global do plano.
            t.HasCheckConstraint(
                "ck_transactions_installment_number_range",
                "installment_number IS NULL OR installment_number BETWEEN 1 AND 120");
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
        builder.Property(t => t.IsManuallyEdited).IsRequired();
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

        builder.HasOne(t => t.CreditCard)
            .WithMany()
            .HasForeignKey(t => t.CreditCardId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.InstallmentPlan)
            .WithMany()
            .HasForeignKey(t => t.InstallmentPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RecurrenceRule>()
            .WithMany()
            .HasForeignKey(t => t.RecurrenceRuleId)
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
        builder.HasIndex(t => new { t.CreditCardId, t.DueDate })
            .HasFilter("credit_card_id IS NOT NULL")
            .HasDatabaseName("ix_transactions_credit_card_id_due_date");

        // Idempotência (D-010/D-026/D-027): NULLs nunca colidem, então movimentações comuns não são afetadas.
        builder.HasIndex(t => new { t.InstallmentPlanId, t.InstallmentNumber })
            .IsUnique()
            .HasDatabaseName("ux_transactions_installment_plan_id_number");
        builder.HasIndex(t => new { t.RecurrenceRuleId, t.OccurrenceDate })
            .IsUnique()
            .HasDatabaseName("ux_transactions_recurrence_rule_id_occurrence_date");
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
