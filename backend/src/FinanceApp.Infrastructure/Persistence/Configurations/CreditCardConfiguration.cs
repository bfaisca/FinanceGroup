using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("credit_cards", t =>
        {
            t.HasCheckConstraint("ck_credit_cards_closing_day_range", "closing_day BETWEEN 1 AND 31");
            t.HasCheckConstraint("ck_credit_cards_due_day_range", "due_day BETWEEN 1 AND 31");
            t.HasCheckConstraint("ck_credit_cards_credit_limit_non_negative", "credit_limit >= 0");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(CreditCard.NameMaxLength).IsRequired();
        builder.Property(c => c.Institution).HasMaxLength(CreditCard.InstitutionMaxLength);
        builder.Property(c => c.CreditLimit).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.ClosingDay).IsRequired();
        builder.Property(c => c.DueDay).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // RESTRICT (e não SET NULL): remover uma conta alteraria silenciosamente a projeção de saldo das compras do cartão.
        // Contas só são removidas na exclusão da conta do usuário, que remove cartões e contas na mesma transação.
        builder.HasOne(c => c.Account)
            .WithMany()
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.UserId, c.IsActive });
        builder.HasIndex(c => c.AccountId);

        // O índice único (user_id, lower(name)) é criado por SQL na migration Etapa3_CardsInstallmentsRecurrences
        // (EF Core não modela índices por expressão). Nome: ux_credit_cards_user_id_lower_name.
    }
}
