using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Name).HasMaxLength(Group.NameMaxLength).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(Group.DescriptionMaxLength);
        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.UpdatedAt).IsRequired();
        builder.Property(g => g.IsActive).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.Members)
            .WithOne()
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(g => g.Members).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(g => g.CreatedByUserId);
        builder.HasXminConcurrencyToken();
    }
}

internal sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("group_members");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.JoinedAt).IsRequired();
        builder.Ignore(m => m.IsActive);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Uma linha por (grupo, usuário): reentrar reativa a linha existente (D-031).
        builder.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique().HasDatabaseName("ux_group_members_group_id_user_id");
        // "Meus grupos" e filtros por participantes ativos.
        builder.HasIndex(m => new { m.UserId, m.Status });
        builder.HasIndex(m => new { m.GroupId, m.Status });
        builder.HasXminConcurrencyToken();
    }
}

internal sealed class GroupInvitationConfiguration : IEntityTypeConfiguration<GroupInvitation>
{
    public void Configure(EntityTypeBuilder<GroupInvitation> builder)
    {
        builder.ToTable("group_invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(GroupInvitation.TokenHashMaxLength).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.ExpiresAt).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(i => i.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Somente o hash do token é armazenado (D-031).
        builder.HasIndex(i => i.TokenHash).IsUnique().HasDatabaseName("ux_group_invitations_token_hash");
        // Um convite pendente por (grupo, e-mail): corridas resultam em 409.
        builder.HasIndex(i => new { i.GroupId, i.Email })
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ux_group_invitations_group_id_email_pending");
        builder.HasIndex(i => new { i.GroupId, i.CreatedAt });
        builder.HasIndex(i => i.InvitedByUserId);

        // Aceites simultâneos do mesmo convite: o segundo recebe conflito.
        builder.HasXminConcurrencyToken();
    }
}
