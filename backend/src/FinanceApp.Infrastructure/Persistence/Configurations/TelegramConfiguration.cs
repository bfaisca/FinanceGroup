using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Telegram;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class TelegramLinkConfiguration : IEntityTypeConfiguration<TelegramLink>
{
    public void Configure(EntityTypeBuilder<TelegramLink> builder)
    {
        builder.ToTable("telegram_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => l.UserId).IsUnique();
        builder.HasIndex(l => l.ChatId).IsUnique();
    }
}

internal sealed class TelegramLinkCodeConfiguration : IEntityTypeConfiguration<TelegramLinkCode>
{
    public void Configure(EntityTypeBuilder<TelegramLinkCode> builder)
    {
        builder.ToTable("telegram_link_codes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.CodeHash).HasMaxLength(TelegramLinkCode.HashMaxLength).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.CodeHash).IsUnique();
        builder.HasIndex(c => c.UserId);
    }
}

internal sealed class TelegramPendingExpenseConfiguration : IEntityTypeConfiguration<TelegramPendingExpense>
{
    public void Configure(EntityTypeBuilder<TelegramPendingExpense> builder)
    {
        builder.ToTable("telegram_pending_expenses", t => t.HasCheckConstraint("ck_telegram_pending_expenses_amount", "amount > 0"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.Description).HasMaxLength(Transaction.DescriptionMaxLength).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Group>().WithMany().HasForeignKey(p => p.GroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.CreatedAt);
    }
}

internal sealed class TelegramGroupSelectionConfiguration : IEntityTypeConfiguration<TelegramGroupSelection>
{
    public void Configure(EntityTypeBuilder<TelegramGroupSelection> builder)
    {
        builder.ToTable("telegram_group_selections");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Group>().WithMany().HasForeignKey(s => s.GroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.UserId).IsUnique();
    }
}
