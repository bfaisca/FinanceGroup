using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Name).HasMaxLength(Account.NameMaxLength).IsRequired();
        builder.Property(a => a.Institution).HasMaxLength(Account.InstitutionMaxLength);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.UserId, a.IsActive });

        // O índice único (user_id, lower(name)) é criado por SQL na migration Etapa2_FinancialCore
        // (EF Core não modela índices por expressão). Nome: ux_accounts_user_id_lower_name.
    }
}

internal sealed class AccountBalanceSnapshotConfiguration : IEntityTypeConfiguration<AccountBalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<AccountBalanceSnapshot> builder)
    {
        builder.ToTable("account_balance_snapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Balance).HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.ReferenceDate).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Snapshot de referência: maior reference_date, depois maior created_at (D-019).
        builder.HasIndex(s => new { s.AccountId, s.ReferenceDate, s.CreatedAt })
            .IsDescending(false, true, true);
    }
}
