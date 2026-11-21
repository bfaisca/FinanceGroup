using FinanceApp.Domain.Common;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.UnitTests.Etapa4b;

public sealed class NotificationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid recipient = Guid.NewGuid();
    private readonly Guid actor = Guid.NewGuid();
    private readonly Guid group = Guid.NewGuid();

    [Fact]
    public void ForTransactionCreated_KeepsASnapshot_AndStartsUnread()
    {
        var txId = Guid.NewGuid();

        var n = Notification.ForTransactionCreated(recipient, group, actor, txId, "  Mercado  ", TransactionType.Expense, 150.5m, Now);

        Assert.Equal(NotificationType.GroupTransactionCreated, n.Type);
        Assert.Equal((recipient, group, actor, txId), (n.RecipientUserId, n.GroupId, n.ActorUserId, n.TransactionId));
        Assert.Equal(("Mercado", TransactionType.Expense, 150.5m), (n.TransactionDescription, n.TransactionType, n.Amount));
        Assert.Null(n.InvitationId);
        Assert.False(n.IsRead);
        Assert.Null(n.ReadAt);
    }

    [Fact]
    public void ForPaymentRegistered_UsesThePaymentAmount()
    {
        var n = Notification.ForPaymentRegistered(recipient, group, actor, Guid.NewGuid(), "Aluguel", TransactionType.Expense, 40m, Now);

        Assert.Equal(NotificationType.GroupPaymentRegistered, n.Type);
        Assert.Equal(40m, n.Amount);
    }

    [Fact]
    public void TheActorNeverReceivesTheirOwnNotification()
    {
        Assert.Throws<DomainException>(() =>
            Notification.ForTransactionCreated(actor, group, actor, Guid.NewGuid(), "x", TransactionType.Expense, 1m, Now));
        Assert.Throws<DomainException>(() =>
            Notification.ForPaymentRegistered(actor, group, actor, Guid.NewGuid(), "x", TransactionType.Income, 1m, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.234)]
    public void InvalidAmounts_AreRejected(double amount) =>
        Assert.Throws<DomainException>(() =>
            Notification.ForTransactionCreated(recipient, group, actor, Guid.NewGuid(), "x", TransactionType.Expense, (decimal)amount, Now));

    [Fact]
    public void BlankOrTooLongDescription_IsRejected()
    {
        Assert.Throws<DomainException>(() =>
            Notification.ForTransactionCreated(recipient, group, actor, Guid.NewGuid(), "  ", TransactionType.Expense, 1m, Now));
        Assert.Throws<DomainException>(() =>
            Notification.ForTransactionCreated(recipient, group, actor, Guid.NewGuid(), new string('a', 201), TransactionType.Expense, 1m, Now));
    }

    [Fact]
    public void EmptyIdentifiers_AreRejected()
    {
        Assert.Throws<DomainException>(() =>
            Notification.ForTransactionCreated(Guid.Empty, group, actor, Guid.NewGuid(), "x", TransactionType.Expense, 1m, Now));
        Assert.Throws<DomainException>(() =>
            Notification.ForInvitationReceived(recipient, Guid.Empty, actor, Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() =>
            Notification.ForInvitationReceived(recipient, group, actor, Guid.Empty, Now));
    }

    [Fact]
    public void MarkAsRead_IsIdempotent_AndKeepsTheFirstReadTime()
    {
        var n = Notification.ForInvitationReceived(recipient, group, actor, Guid.NewGuid(), Now);

        n.MarkAsRead(Now.AddMinutes(1));
        n.MarkAsRead(Now.AddMinutes(9));

        Assert.True(n.IsRead);
        Assert.Equal(Now.AddMinutes(1), n.ReadAt);
    }

    [Fact]
    public void Reissue_ReopensTheInvitationNotification_AndMovesItUp()
    {
        var n = Notification.ForInvitationReceived(recipient, group, actor, Guid.NewGuid(), Now);
        n.MarkAsRead(Now.AddMinutes(1));
        var other = Guid.NewGuid();

        n.Reissue(other, Now.AddDays(2));

        Assert.False(n.IsRead);
        Assert.Equal(Now.AddDays(2), n.CreatedAt);
        Assert.Equal(other, n.ActorUserId);
    }

    [Fact]
    public void Reissue_IsOnlyForInvitationNotifications()
    {
        var n = Notification.ForTransactionCreated(recipient, group, actor, Guid.NewGuid(), "x", TransactionType.Expense, 1m, Now);

        Assert.Throws<DomainException>(() => n.Reissue(actor, Now));
    }
}
