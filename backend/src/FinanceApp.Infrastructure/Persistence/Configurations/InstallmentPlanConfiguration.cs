using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Installments;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class InstallmentPlanConfiguration : IEntityTypeConfiguration<InstallmentPlan>
{
    public void Configure(EntityTypeBuilder<InstallmentPlan> builder)
    {
        builder.ToTable("installment_plans", t =>
        {
            t.HasCheckConstraint("ck_installment_plans_installment_count_range", "installment_count BETWEEN 2 AND 120");
            t.HasCheckConstraint("ck_installment_plans_total_amount_positive", "total_amount > 0");
            t.HasCheckConstraint("ck_installment_plans_card_without_account", "credit_card_id IS NULL OR account_id IS NULL");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Description).HasMaxLength(InstallmentPlan.DescriptionMaxLength).IsRequired();
        builder.Property(p => p.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.InstallmentCount).IsRequired();
        builder.Property(p => p.FirstDate).IsRequired();
        builder.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.Notes).HasMaxLength(Domain.Transactions.Transaction.NotesMaxLength);
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Account).WithMany().HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.CreditCard).WithMany().HasForeignKey(p => p.CreditCardId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.UserId, p.CreatedAt });
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.AccountId);
        builder.HasIndex(p => p.CreditCardId);
    }
}
