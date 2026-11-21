using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceApp.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", t =>
        {
            // Coerência por tipo (D-042/D-043): movimentação/pagamento carregam o instantâneo; convite carrega o convite.
            t.HasCheckConstraint(
                "ck_notifications_shape",
                "(type IN ('GroupTransactionCreated','GroupPaymentRegistered') " +
                "AND transaction_id IS NOT NULL AND transaction_description IS NOT NULL " +
                "AND amount IS NOT NULL AND amount > 0 AND invitation_id IS NULL " +
                "AND (actor_user_id IS NULL OR actor_user_id <> recipient_user_id)) " +
                "OR (type = 'GroupInvitationReceived' " +
                "AND invitation_id IS NOT NULL AND transaction_id IS NULL AND transaction_description IS NULL " +
                "AND amount IS NULL AND series_occurrences IS NULL)");
            t.HasCheckConstraint(
                "ck_notifications_series_occurrences",
                "series_occurrences IS NULL OR (type = 'GroupTransactionCreated' AND series_occurrences BETWEEN 2 AND 60)");
        });
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(n => n.TransactionDescription).HasMaxLength(Transaction.DescriptionMaxLength);
        builder.Property(n => n.Amount).HasPrecision(18, 2);
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Ignore(n => n.IsRead);

        // Notificações pertencem ao destinatário: somem com a exclusão dele (D-044). Contas são anonimizadas, não apagadas;
        // a rotina de exclusão remove as notificações explicitamente e a cascata é só uma rede de segurança.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);
        // Quem executou a ação: a conta é anonimizada (nome "Usuário removido"), nunca apagada; se um dia for, o vínculo é solto.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(n => n.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        // Excluir uma movimentação de grupo (sem pagamentos) remove também as notificações dela.
        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(n => n.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GroupInvitation>()
            .WithMany()
            .HasForeignKey(n => n.InvitationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Listagem do destinatário, mais recentes primeiro.
        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_notifications_recipient_user_id_created_at");
        // Contador de não lidas.
        builder.HasIndex(n => n.RecipientUserId)
            .HasFilter("read_at IS NULL")
            .HasDatabaseName("ix_notifications_recipient_user_id_unread");
        // D-043: uma notificação de convite por (destinatário, convite); reenvio reaproveita.
        builder.HasIndex(n => new { n.RecipientUserId, n.InvitationId })
            .IsUnique()
            .HasFilter("invitation_id IS NOT NULL")
            .HasDatabaseName("ux_notifications_recipient_user_id_invitation_id");
        // Retenção (D-044): lidas há mais de 90 dias.
        builder.HasIndex(n => n.ReadAt)
            .HasFilter("read_at IS NOT NULL")
            .HasDatabaseName("ix_notifications_read_at");
        builder.HasIndex(n => n.GroupId);
        builder.HasIndex(n => n.TransactionId);
        builder.HasIndex(n => n.ActorUserId);
        builder.HasXminConcurrencyToken();
    }
}
