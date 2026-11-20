using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class RecurrenceRuleConfiguration : IEntityTypeConfiguration<RecurrenceRule>
{
    public void Configure(EntityTypeBuilder<RecurrenceRule> builder)
    {
        builder.ToTable("recurrence_rules", t =>
        {
            t.HasCheckConstraint("ck_recurrence_rules_interval_range", "\"interval\" BETWEEN 1 AND 365");
            t.HasCheckConstraint("ck_recurrence_rules_amount_positive", "amount > 0");
            t.HasCheckConstraint("ck_recurrence_rules_end_date_after_start", "end_date IS NULL OR end_date >= start_date");
            t.HasCheckConstraint(
                "ck_recurrence_rules_card_expense_without_account",
                "credit_card_id IS NULL OR (type = 'Expense' AND account_id IS NULL)");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Description).HasMaxLength(RecurrenceRule.DescriptionMaxLength).IsRequired();
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.PaymentMethod).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Notes).HasMaxLength(Domain.Transactions.Transaction.NotesMaxLength);
        builder.Property(r => r.Frequency).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Interval).IsRequired();
        builder.Property(r => r.StartDate).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Account).WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CreditCard).WithMany().HasForeignKey(r => r.CreditCardId).OnDelete(DeleteBehavior.Restrict);

        // Serviço em segundo plano: varre as regras ativas de todos os usuários.
        builder.HasIndex(r => r.IsActive);
        builder.HasIndex(r => new { r.UserId, r.IsActive });
        builder.HasIndex(r => r.CategoryId);
        builder.HasIndex(r => r.AccountId);
        builder.HasIndex(r => r.CreditCardId);

        // Edições simultâneas da mesma regra: a segunda recebe 409 em vez de regenerar sobre dados desatualizados.
        builder.HasXminConcurrencyToken();
    }
}
