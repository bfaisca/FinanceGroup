using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Groups;
using FinanceApp.Application.Notifications;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Notifications;
using FinanceApp.Domain.Transactions;
using FinanceApp.Domain.Users;
using FinanceApp.UnitTests.Etapa4;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.UnitTests.Etapa4b;

public sealed class NotificationServiceTests : IDisposable
{
    private readonly GroupTestKit kit = new();

    public void Dispose() => kit.Dispose();

    private static readonly NotificationListQuery All = new();

    /// <summary>Descarta as notificações geradas pela montagem do cenário (o provedor em memória não tem ExecuteDelete).</summary>
    private async Task ClearNotificationsAsync()
    {
        kit.Db.Notifications.RemoveRange(await kit.Db.Notifications.ToListAsync());
        await kit.Db.SaveChangesAsync();
    }

    /// <summary>Grupo "Casa" com três participantes: Maria (Owner), Ana e Bia.</summary>
    private async Task<(User Maria, User Ana, User Bia, Guid GroupId)> ThreeMembersAsync()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana");
        var bia = await kit.NewUserAsync("Bia");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.JoinAsync(group.Id, maria, ana);
        await kit.JoinAsync(group.Id, maria, bia);
        // O convite de entrada gerou notificações de convite; começamos de uma lista limpa.
        await ClearNotificationsAsync();
        return (maria, ana, bia, group.Id);
    }

    private async Task<PagedResultView> ListAsAsync(User user, NotificationListQuery? query = null)
    {
        kit.As(user);
        var page = await kit.Notifications.ListAsync(query ?? All, CancellationToken.None);
        return new PagedResultView(page.Items, page.TotalCount);
    }

    private sealed record PagedResultView(IReadOnlyList<NotificationDto> Items, int Total);

    [Fact]
    public async Task NewGroupTransaction_NotifiesEveryActiveMemberExceptTheAuthor()
    {
        var (maria, ana, bia, groupId) = await ThreeMembersAsync();
        kit.As(ana);

        var tx = await kit.CreateTxAsync(groupId, "Mercado", 150m);

        Assert.Empty((await ListAsAsync(ana)).Items);
        foreach (var recipient in new[] { maria, bia })
        {
            var item = Assert.Single((await ListAsAsync(recipient)).Items);
            Assert.Equal(NotificationType.GroupTransactionCreated, item.Type);
            Assert.False(item.IsRead);
            Assert.Equal("Casa", item.Group.Name);
            Assert.Equal(("Ana", ana.Id), (item.Actor!.Name, item.Actor.UserId));
            Assert.Equal((tx.Id, "Mercado", TransactionType.Expense, 150m), (item.Transaction!.Id, item.Transaction.Description, item.Transaction.Type, item.Transaction.Amount));
            Assert.Null(item.Invitation);
        }
    }

    [Fact]
    public async Task Payment_NotifiesTheOthersExceptThePayer_WithThePaymentAmount()
    {
        var (maria, ana, bia, groupId) = await ThreeMembersAsync();
        kit.As(maria);
        var tx = await kit.CreateTxAsync(groupId, "Aluguel", 1000m);
        await ClearNotificationsAsync();
        kit.As(bia);

        await kit.PayAsync(groupId, tx.Id, 300m);

        Assert.Empty((await ListAsAsync(bia)).Items);
        foreach (var recipient in new[] { maria, ana })
        {
            var item = Assert.Single((await ListAsAsync(recipient)).Items);
            Assert.Equal(NotificationType.GroupPaymentRegistered, item.Type);
            Assert.Equal(300m, item.Transaction!.Amount);
            Assert.Equal("Bia", item.Actor!.Name);
        }
    }

    [Fact]
    public async Task PaymentWithInterest_NotifiesTheTotalAmount_PrincipalPlusInterest()
    {
        var (maria, ana, _, groupId) = await ThreeMembersAsync();
        kit.As(maria);
        var tx = await kit.CreateTxAsync(groupId, "Aluguel", 500m);
        await ClearNotificationsAsync();
        kit.As(maria);

        await kit.PayAsync(groupId, tx.Id, 500m, interest: 23.50m);

        var item = Assert.Single((await ListAsAsync(ana)).Items);
        Assert.Equal(NotificationType.GroupPaymentRegistered, item.Type);
        Assert.Equal((TransactionType.Expense, 523.50m, (int?)null), (item.Transaction!.Type, item.Transaction.Amount, item.Transaction.SeriesOccurrences));
    }

    [Fact]
    public async Task RecurrentCreation_NotifiesOncePerSeries_WithTheOccurrences()
    {
        var (maria, ana, bia, groupId) = await ThreeMembersAsync();
        kit.As(maria);

        await kit.CreateTxAsync(
            groupId, "Internet", 100m,
            recurrence: new FinanceApp.Application.Groups.GroupRecurrenceRequest { Frequency = FinanceApp.Domain.Recurrences.RecurrenceFrequency.Monthly, Occurrences = 6 });

        foreach (var recipient in new[] { ana, bia })
        {
            var item = Assert.Single((await ListAsAsync(recipient)).Items);
            Assert.Equal((NotificationType.GroupTransactionCreated, 100m, 6), (item.Type, item.Transaction!.Amount, item.Transaction.SeriesOccurrences));
        }

        Assert.Empty((await ListAsAsync(maria)).Items);
    }

    [Fact]
    public async Task FormerMembers_AreNotNotified_AndTheirOldNotificationsDisappear_WithoutBeingDeleted()
    {
        var (maria, ana, bia, groupId) = await ThreeMembersAsync();
        kit.As(maria);
        await kit.CreateTxAsync(groupId, "Antes de sair", 10m);
        Assert.Single((await ListAsAsync(bia)).Items);

        kit.As(bia);
        await kit.Groups.LeaveAsync(groupId, CancellationToken.None);

        Assert.Empty((await ListAsAsync(bia)).Items);
        kit.As(bia);
        Assert.Equal(0, (await kit.Notifications.CountUnreadAsync(CancellationToken.None)).Count);
        Assert.Equal(1, await kit.Db.Notifications.CountAsync(n => n.RecipientUserId == bia.Id));

        kit.As(maria);
        await kit.CreateTxAsync(groupId, "Depois de sair", 20m);
        Assert.Equal(1, await kit.Db.Notifications.CountAsync(n => n.RecipientUserId == bia.Id));
        Assert.Equal(2, (await ListAsAsync(ana)).Items.Count);
    }

    [Fact]
    public async Task ListOrdering_PagingAndUnreadFilter()
    {
        var (maria, ana, _, groupId) = await ThreeMembersAsync();
        kit.As(maria);
        for (var i = 1; i <= 3; i++)
        {
            kit.Clock.Advance(TimeSpan.FromMinutes(1));
            await kit.CreateTxAsync(groupId, $"Mov {i}", i * 10m);
        }

        var first = await ListAsAsync(ana, new NotificationListQuery { Page = 1, PageSize = 2 });
        Assert.Equal(3, first.Total);
        Assert.Equal(["Mov 3", "Mov 2"], first.Items.Select(n => n.Transaction!.Description));

        await kit.Notifications.MarkAsReadAsync(first.Items[0].Id, CancellationToken.None);
        var unread = await ListAsAsync(ana, new NotificationListQuery { UnreadOnly = true });
        Assert.Equal(["Mov 2", "Mov 1"], unread.Items.Select(n => n.Transaction!.Description));
        Assert.Equal(2, (await kit.Notifications.CountUnreadAsync(CancellationToken.None)).Count);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task InvalidPaging_IsAValidationError(int page, int pageSize)
    {
        await kit.NewUserAsync("Solo");

        await Assert.ThrowsAsync<ValidationException>(() =>
            kit.Notifications.ListAsync(new NotificationListQuery { Page = page, PageSize = pageSize }, CancellationToken.None));
    }

    [Fact]
    public async Task MarkAsRead_IsIdempotent_MarkAll_And_OtherUsersNotificationIs404()
    {
        var (maria, ana, bia, groupId) = await ThreeMembersAsync();
        kit.As(maria);
        await kit.CreateTxAsync(groupId, "Um", 1m);
        await kit.CreateTxAsync(groupId, "Dois", 2m);
        var anaItems = (await ListAsAsync(ana)).Items;

        // Bia não consegue ler/marcar a notificação da Ana.
        kit.As(bia);
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Notifications.MarkAsReadAsync(anaItems[0].Id, CancellationToken.None));
        Assert.Equal(2, (await kit.Notifications.CountUnreadAsync(CancellationToken.None)).Count);

        kit.As(ana);
        await kit.Notifications.MarkAsReadAsync(anaItems[0].Id, CancellationToken.None);
        var readAt = (await ListAsAsync(ana)).Items.Single(n => n.Id == anaItems[0].Id).ReadAt;
        kit.Clock.Advance(TimeSpan.FromHours(1));
        await kit.Notifications.MarkAsReadAsync(anaItems[0].Id, CancellationToken.None);
        Assert.Equal(readAt, (await ListAsAsync(ana)).Items.Single(n => n.Id == anaItems[0].Id).ReadAt);
        Assert.Equal(1, (await kit.Notifications.CountUnreadAsync(CancellationToken.None)).Count);

        await kit.Notifications.MarkAllAsReadAsync(CancellationToken.None);
        Assert.Equal(0, (await kit.Notifications.CountUnreadAsync(CancellationToken.None)).Count);
        // Marcar todas só afeta o próprio usuário.
        kit.As(bia);
        Assert.Equal(2, (await kit.Notifications.CountUnreadAsync(CancellationToken.None)).Count);
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Notifications.MarkAsReadAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ActorOfADeletedAccount_ShowsAsRemovedUser()
    {
        var (maria, ana, _, groupId) = await ThreeMembersAsync();
        kit.As(maria);
        await kit.CreateTxAsync(groupId, "Da Maria", 5m);
        var author = await kit.Db.Users.AsTracking().SingleAsync(u => u.Id == maria.Id);
        author.Anonymize(kit.Clock.UtcNow);
        await kit.Db.SaveChangesAsync();

        var item = Assert.Single((await ListAsAsync(ana)).Items);

        Assert.Equal("Usuário removido", item.Actor!.Name);
    }

    // ------------------------------------------------------------------ convites

    [Fact]
    public async Task Invitation_ForAnExistingAccount_NotifiesCaseInsensitively_WithCanRespond()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana", "ana@exemplo.com");
        var stranger = await kit.NewUserAsync("Zé", "ze@exemplo.com");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);

        var invitation = await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "  ANA@Exemplo.COM " }, CancellationToken.None);

        var item = Assert.Single((await ListAsAsync(ana)).Items);
        Assert.Equal(NotificationType.GroupInvitationReceived, item.Type);
        Assert.Equal(("Casa", "Maria"), (item.Group.Name, item.Actor!.Name));
        Assert.Null(item.Transaction);
        Assert.Equal(invitation.Id, item.Invitation!.Id);
        Assert.Equal(GroupInvitationStatus.Pending, item.Invitation.Status);
        Assert.True(item.Invitation.CanRespond);
        Assert.Equal(invitation.ExpiresAt, item.Invitation.ExpiresAt);
        Assert.Empty((await ListAsAsync(stranger)).Items);
        Assert.Empty((await ListAsAsync(maria)).Items);
        // O e-mail continua sendo enviado (D-043).
        Assert.Single(kit.Emails.Sent);
    }

    [Fact]
    public async Task Invitation_ForAnEmailWithoutAccount_DoesNotNotify()
    {
        await kit.NewUserAsync("Maria");
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);

        await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "ninguem@exemplo.com" }, CancellationToken.None);

        Assert.Equal(0, await kit.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Invitation_ForADeactivatedAccount_DoesNotNotify()
    {
        var maria = await kit.NewUserAsync("Maria");
        var gone = await kit.NewUserAsync("Gone", "gone@exemplo.com");
        var row = await kit.Db.Users.AsTracking().SingleAsync(u => u.Id == gone.Id);
        row.Anonymize(kit.Clock.UtcNow);
        await kit.Db.SaveChangesAsync();
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);

        await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "gone@exemplo.com" }, CancellationToken.None);

        Assert.Equal(0, await kit.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Resend_ReusesTheNotification_MarksItUnread_AndDoesNotDuplicate()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana", "ana@exemplo.com");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        var invitation = await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "ana@exemplo.com" }, CancellationToken.None);
        var original = Assert.Single((await ListAsAsync(ana)).Items);
        await kit.Notifications.MarkAsReadAsync(original.Id, CancellationToken.None);
        kit.Clock.Advance(TimeSpan.FromDays(2));

        kit.As(maria);
        await kit.Invitations.ResendAsync(group.Id, invitation.Id, CancellationToken.None);

        var item = Assert.Single((await ListAsAsync(ana)).Items);
        Assert.Equal(original.Id, item.Id);
        Assert.False(item.IsRead);
        Assert.True(item.CreatedAt > original.CreatedAt);
        Assert.Equal(1, await kit.Db.Notifications.CountAsync());
        kit.As(ana);
        Assert.Equal(1, (await kit.Notifications.CountUnreadAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task AcceptByNotification_CreatesTheMembership_AndMarksItRead()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana", "ana@exemplo.com");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "ana@exemplo.com" }, CancellationToken.None);
        var notification = Assert.Single((await ListAsAsync(ana)).Items);

        var summary = await kit.Notifications.AcceptInvitationAsync(notification.Id, CancellationToken.None);

        Assert.Equal((group.Id, GroupMemberRole.Member), (summary.Id, summary.MyRole));
        Assert.True(await kit.Db.GroupMembers.AnyAsync(m => m.GroupId == group.Id && m.UserId == ana.Id && m.Status == GroupMemberStatus.Active));
        var after = Assert.Single((await ListAsAsync(ana)).Items);
        Assert.True(after.IsRead);
        Assert.Equal(GroupInvitationStatus.Accepted, after.Invitation!.Status);
        Assert.False(after.Invitation.CanRespond);
        // Responder de novo: o convite já foi usado.
        kit.As(ana);
        await Assert.ThrowsAsync<DomainException>(() => kit.Notifications.AcceptInvitationAsync(notification.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeclineByNotification_MarksDeclined_AndRead_WithoutMembership()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana", "ana@exemplo.com");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "ana@exemplo.com" }, CancellationToken.None);
        var notification = Assert.Single((await ListAsAsync(ana)).Items);

        await kit.Notifications.DeclineInvitationAsync(notification.Id, CancellationToken.None);

        var after = Assert.Single((await ListAsAsync(ana)).Items);
        Assert.True(after.IsRead);
        Assert.Equal(GroupInvitationStatus.Declined, after.Invitation!.Status);
        Assert.False(after.Invitation.CanRespond);
        Assert.False(await kit.Db.GroupMembers.AnyAsync(m => m.GroupId == group.Id && m.UserId == ana.Id));
    }

    [Fact]
    public async Task AcceptByNotification_OfARevokedInvitation_IsRejected_AndCannotRespond()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana", "ana@exemplo.com");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        var invitation = await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "ana@exemplo.com" }, CancellationToken.None);
        await kit.Invitations.RevokeAsync(group.Id, invitation.Id, CancellationToken.None);

        var item = Assert.Single((await ListAsAsync(ana)).Items);
        Assert.Equal(GroupInvitationStatus.Revoked, item.Invitation!.Status);
        Assert.False(item.Invitation.CanRespond);
        kit.As(ana);
        await Assert.ThrowsAsync<DomainException>(() => kit.Notifications.AcceptInvitationAsync(item.Id, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => kit.Notifications.DeclineInvitationAsync(item.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiredInvitation_IsDerivedAsExpired_AndCannotBeAccepted()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana", "ana@exemplo.com");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "ana@exemplo.com" }, CancellationToken.None);
        kit.Clock.Advance(TimeSpan.FromDays(8));

        var item = Assert.Single((await ListAsAsync(ana)).Items);

        Assert.Equal(GroupInvitationStatus.Expired, item.Invitation!.Status);
        Assert.False(item.Invitation.CanRespond);
        kit.As(ana);
        await Assert.ThrowsAsync<DomainException>(() => kit.Notifications.AcceptInvitationAsync(item.Id, CancellationToken.None));
    }

    [Fact]
    public async Task InvitationAcceptedByEmailToken_IsDerivedAsAccepted_AndTheNotificationBecomesRead()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana", "ana@exemplo.com");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "ana@exemplo.com" }, CancellationToken.None);
        var token = kit.LastToken("ana@exemplo.com");
        kit.As(ana);
        await kit.Invitations.AcceptAsync(token, CancellationToken.None);

        var item = Assert.Single((await ListAsAsync(ana)).Items);

        Assert.True(item.IsRead);
        Assert.Equal(GroupInvitationStatus.Accepted, item.Invitation!.Status);
    }

    [Fact]
    public async Task InvitationNotifications_DoNotDependOnMembership_AndOtherUsersCannotRespond()
    {
        var maria = await kit.NewUserAsync("Maria");
        var ana = await kit.NewUserAsync("Ana", "ana@exemplo.com");
        var bia = await kit.NewUserAsync("Bia");
        kit.As(maria);
        var group = await kit.Groups.CreateAsync(new CreateGroupRequest { Name = "Casa" }, CancellationToken.None);
        await kit.Invitations.CreateAsync(group.Id, new CreateInvitationRequest { Email = "ana@exemplo.com" }, CancellationToken.None);
        var item = Assert.Single((await ListAsAsync(ana)).Items);

        // Ana nunca foi membro e mesmo assim vê o convite; Bia recebe 404 ao tentar usá-lo.
        kit.As(bia);
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Notifications.AcceptInvitationAsync(item.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Notifications.DeclineInvitationAsync(item.Id, CancellationToken.None));
    }

    [Fact]
    public async Task AcceptOrDecline_OfANonInvitationNotification_Is404()
    {
        var (maria, ana, _, groupId) = await ThreeMembersAsync();
        kit.As(maria);
        await kit.CreateTxAsync(groupId, "Mov", 1m);
        var item = Assert.Single((await ListAsAsync(ana)).Items);

        kit.As(ana);
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Notifications.AcceptInvitationAsync(item.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => kit.Notifications.DeclineInvitationAsync(item.Id, CancellationToken.None));
    }

    // ------------------------------------------------------------------ derivação do estado do convite

    [Fact]
    public void InvitationState_CanRespondOnlyWhenPendingValidSameEmailAndNotMember()
    {
        var now = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        var invitation = GroupInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), "ana@exemplo.com", "hash", now);

        Assert.True(NotificationService.ToInvitationDto(invitation, "ANA@exemplo.com", false, now).CanRespond);
        Assert.False(NotificationService.ToInvitationDto(invitation, "outra@exemplo.com", false, now).CanRespond);
        Assert.False(NotificationService.ToInvitationDto(invitation, "ana@exemplo.com", true, now).CanRespond);

        var expired = NotificationService.ToInvitationDto(invitation, "ana@exemplo.com", false, now.AddDays(7));
        Assert.Equal((GroupInvitationStatus.Expired, false), (expired.Status, expired.CanRespond));

        invitation.Decline("ana@exemplo.com", now);
        var declined = NotificationService.ToInvitationDto(invitation, "ana@exemplo.com", false, now);
        Assert.Equal((GroupInvitationStatus.Declined, false), (declined.Status, declined.CanRespond));
    }
}
