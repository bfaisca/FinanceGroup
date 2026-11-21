using FinanceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Infrastructure.Background;

/// <summary>Retenção de notificações (D-044): remove as LIDAS há mais de 90 dias; as não lidas nunca são removidas por tempo.</summary>
public static class NotificationRetention
{
    public const int RetentionDays = 90;

    /// <returns>Quantidade de notificações removidas.</returns>
    public static Task<int> PurgeReadAsync(AppDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var limit = now.ToUniversalTime().AddDays(-RetentionDays);
        return db.Notifications.Where(n => n.ReadAt != null && n.ReadAt < limit).ExecuteDeleteAsync(cancellationToken);
    }
}
