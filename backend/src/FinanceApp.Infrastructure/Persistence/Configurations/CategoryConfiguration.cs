using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", t =>
            t.HasCheckConstraint("ck_categories_default_without_user", "(NOT is_default) OR user_id IS NULL"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.Icon).HasMaxLength(Category.IconMaxLength);
        builder.Property(c => c.Color).HasMaxLength(Category.ColorLength);
        builder.Property(c => c.IsDefault).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ParentCategory)
            .WithMany()
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.ParentCategoryId);

        // O índice único por (user_id, type, parent_category_id, lower(name)) tratando NULLs é criado
        // por SQL na migration Etapa2_FinancialCore (ux_categories_scope_type_parent_lower_name).

        // Seed idempotente (D-022): IDs fixos; a migration insere uma única vez.
        builder.HasData(DefaultCategories.All.Select(d => new
        {
            d.Id,
            d.Name,
            d.Type,
            d.Icon,
            d.Color,
            IsDefault = true,
            IsActive = true,
        }));
    }
}
