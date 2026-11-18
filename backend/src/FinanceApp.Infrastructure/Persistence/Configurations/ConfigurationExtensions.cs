using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Concorrência otimista usando a coluna de sistema <c>xmin</c> do PostgreSQL (não cria coluna física).
    /// </summary>
    public static void HasXminConcurrencyToken<T>(this EntityTypeBuilder<T> builder)
        where T : class =>
        builder.Property<uint>("Version").HasColumnName("xmin").IsRowVersion();
}
