using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(RefreshToken.TokenHashMaxLength).IsRequired();
        builder.Property(t => t.CreatedByIp).HasMaxLength(RefreshToken.IpMaxLength);
        builder.Property(t => t.RevocationReason).HasConversion<string>().HasMaxLength(32);
        builder.Ignore(t => t.IsRevoked);
        builder.Ignore(t => t.WasRotated);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.FamilyId);

        builder.HasXminConcurrencyToken();
    }
}
