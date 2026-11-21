using System.Net;
using System.Text.Json;
using FinanceApp.Application.Common;
using FinanceApp.Application.Groups;
using FinanceApp.Application.Notifications;
using FinanceApp.Domain.Groups;
using FinanceApp.Domain.Notifications;
using FinanceApp.Infrastructure.Background;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>Etapa 4b (contrato §17, D-041 a D-044): notificações dentro do app sobre a API real com PostgreSQL.</summary>
[Collection(ApiCollection.Name)]
public class NotificationsApiTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    private static Task<PagedResult<NotificationDto>> ListAsync(ApiUser user, string query = "") =>
        user.GetOk<PagedResult<NotificationDto>>($"/api/v1/notifications{query}");

    private static async Task<int> UnreadAsync(ApiUser user) =>
        (await user.GetOk<UnreadCountDto>("/api/v1/notifications/unread-count")).Count;

    /// <summary>Notificações de movimentação/pagamento (o convite de entrada, já aceito, permanece na lista como lido).</summary>
    private static List<NotificationDto> GroupEvents(PagedResult<NotificationDto> page) =>
        page.Items.Where(n => n.Type != NotificationType.GroupInvitationReceived).ToList();

    private async Task<(ApiUser Owner, ApiUser Ana, ApiUser Bia, Guid GroupId)> ThreeMemberGroupAsync()
    {
        var owner = await h.NewUserAsync("Maria Dona");
        var ana = await h.NewUserAsync("Ana");
        var bia = await h.NewUserAsync("Bia");
        var group = await h.CreateGroupAsync(owner, "Casa");
        await h.JoinAsync(owner, group.Id, ana);
        await h.JoinAsync(owner, group.Id, bia);
        return (owner, ana, bia, group.Id);
    }

    [Fact]
    public async Task GroupFlow_TransactionAndPayment_NotifyOthersButNotTheActor_AndReadingWorks()
    {
        var (owner, ana, bia, groupId) = await ThreeMemberGroupAsync();
        // Os convites de entrada já foram aceitos (e portanto lidos); nada pendente.
        Assert.Equal((0, 0, 0), (await UnreadAsync(owner), await UnreadAsync(ana), await UnreadAsync(bia)));

        var tx = await h.CreateGroupTxAsync(ana, groupId, "Mercado", 150m);

        Assert.Equal(0, await UnreadAsync(ana));
        foreach (var user in new[] { owner, bia })
        {
            Assert.Equal(1, await UnreadAsync(user));
            var item = Assert.Single((await ListAsync(user, "?unreadOnly=true")).Items);
            Assert.Equal(NotificationType.GroupTransactionCreated, item.Type);
            Assert.False(item.IsRead);
            Assert.Null(item.ReadAt);
            Assert.Equal(("Casa", groupId), (item.Group.Name, item.Group.Id));
            Assert.Equal((ana.Id, "Ana"), (item.Actor!.UserId, item.Actor.Name));
            Assert.Equal((tx.Id, "Mercado", FinanceApp.Domain.Transactions.TransactionType.Expense, 150m), (item.Transaction!.Id, item.Transaction.Description, item.Transaction.Type, item.Transaction.Amount));
            Assert.Null(item.Invitation);
        }

        // Pagamento parcial da Ana: o valor da notificação é o do PAGAMENTO; o pagador não recebe.
        await h.PayGroupTxAsync(ana, groupId, tx.Id, 40m);
        Assert.Equal(0, await UnreadAsync(ana));
        var payment = (await ListAsync(owner)).Items.First();
        Assert.Equal(NotificationType.GroupPaymentRegistered, payment.Type);
        Assert.Equal(40m, payment.Transaction!.Amount);
        Assert.Equal("Ana", payment.Actor!.Name);
        Assert.Equal(2, await UnreadAsync(bia));

        // Marcar uma como lida (idempotente) e depois todas.
        var first = (await ListAsync(bia)).Items[0];
        Assert.Equal(HttpStatusCode.NoContent, (await bia.Post($"/api/v1/notifications/{first.Id}/read")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await bia.Post($"/api/v1/notifications/{first.Id}/read")).StatusCode);
        Assert.Equal(1, await UnreadAsync(bia));
        Assert.Equal(3, (await ListAsync(bia)).TotalCount); // 2 eventos + o convite de entrada (aceito, lido)
        Assert.Single((await ListAsync(bia, "?unreadOnly=true")).Items);
        Assert.Equal(HttpStatusCode.NoContent, (await bia.Post("/api/v1/notifications/read-all")).StatusCode);
        Assert.Equal(0, await UnreadAsync(bia));
        Assert.All((await ListAsync(bia)).Items, n => Assert.True(n.IsRead && n.ReadAt is not null));
        // read-all do Bia não mexe nas do dono.
        Assert.Equal(2, await UnreadAsync(owner));
    }

    [Fact]
    public async Task ListIsPagedMostRecentFirst_AndValidatesPageSize()
    {
        var (owner, ana, _, groupId) = await ThreeMemberGroupAsync();
        for (var i = 1; i <= 3; i++)
        {
            await h.CreateGroupTxAsync(owner, groupId, $"Mov {i}", i);
        }

        var page1 = await ListAsync(ana, "?unreadOnly=true&page=1&pageSize=2");
        var page2 = await ListAsync(ana, "?unreadOnly=true&page=2&pageSize=2");

        Assert.Equal((3, 2, 2), (page1.TotalCount, page1.TotalPages, page1.Items.Count));
        Assert.Equal(["Mov 3", "Mov 2"], page1.Items.Select(n => n.Transaction!.Description));
        Assert.Equal(["Mov 1"], page2.Items.Select(n => n.Transaction!.Description));
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.Get("/api/v1/notifications?pageSize=51")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.Get("/api/v1/notifications?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.Get("/api/v1/notifications?page=0")).StatusCode);
        Assert.Equal(20, (await ListAsync(ana)).PageSize);
    }

    [Fact]
    public async Task EndpointsRequireAuthentication()
    {
        var anonymous = h.Client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/notifications/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);
    }

    [Fact]
    public async Task OtherUsersNotifications_Are404_ForEveryAction_AndAreNotAffected()
    {
        var (owner, ana, bia, groupId) = await ThreeMemberGroupAsync();
        await h.CreateGroupTxAsync(owner, groupId, "Privada", 10m);
        var anaItem = Assert.Single((await ListAsync(ana, "?unreadOnly=true")).Items);

        Assert.Equal(HttpStatusCode.NotFound, (await bia.Post($"/api/v1/notifications/{anaItem.Id}/read")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bia.Post($"/api/v1/notifications/{anaItem.Id}/accept-invitation")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bia.Post($"/api/v1/notifications/{anaItem.Id}/decline-invitation")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bia.Post($"/api/v1/notifications/{Guid.NewGuid()}/read")).StatusCode);
        Assert.DoesNotContain(anaItem.Id, (await ListAsync(bia)).Items.Select(n => n.Id));
        Assert.Equal(1, await UnreadAsync(ana));
        // Notificação de movimentação não é de convite, nem para o dono dela.
        Assert.Equal(HttpStatusCode.NotFound, (await ana.Post($"/api/v1/notifications/{anaItem.Id}/accept-invitation")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.Post($"/api/v1/notifications/{anaItem.Id}/decline-invitation")).StatusCode);
    }

    [Fact]
    public async Task FormerMember_NeitherSeesNorCountsGroupNotifications_AndIsNotNotifiedAgain()
    {
        var (owner, ana, bia, groupId) = await ThreeMemberGroupAsync();
        await h.CreateGroupTxAsync(owner, groupId, "Antes", 10m);
        var before = Assert.Single(GroupEvents(await ListAsync(bia)));
        Assert.Equal(1, await UnreadAsync(bia));

        Assert.Equal(HttpStatusCode.NoContent, (await bia.Post($"/api/v1/groups/{groupId}/leave")).StatusCode);

        Assert.Equal(0, await UnreadAsync(bia));
        Assert.Empty(GroupEvents(await ListAsync(bia)));
        Assert.Equal(HttpStatusCode.NotFound, (await bia.Post($"/api/v1/notifications/{before.Id}/read")).StatusCode);
        // O registro não é apagado (D-044) e ex-membros não recebem novas notificações.
        await h.CreateGroupTxAsync(owner, groupId, "Depois", 20m);
        var stored = await h.WithDbAsync(db => db.Notifications.AsNoTracking().CountAsync(n => n.RecipientUserId == bia.Id && n.GroupId == groupId));
        Assert.Equal(1 + 1, stored); // convite aceito (lido) + "Antes"
        Assert.Equal(2, GroupEvents(await ListAsync(ana)).Count);
        // Quem é removido por um administrador também deixa de ver.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.SendAsync(HttpMethod.Delete, $"/api/v1/groups/{groupId}/members/{(await h.MemberOfAsync(owner, groupId, ana)).Id}")).StatusCode);
        Assert.Equal(0, await UnreadAsync(ana));
        Assert.Empty(GroupEvents(await ListAsync(ana)));
    }

    [Fact]
    public async Task DeletingAGroupTransaction_RemovesItsNotifications()
    {
        var (owner, ana, _, groupId) = await ThreeMemberGroupAsync();
        var tx = await h.CreateGroupTxAsync(owner, groupId, "Engano", 10m);
        Assert.Equal(1, await UnreadAsync(ana));

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Delete($"/api/v1/groups/{groupId}/transactions/{tx.Id}")).StatusCode);

        Assert.Equal(0, await UnreadAsync(ana));
        Assert.Empty(GroupEvents(await ListAsync(ana)));
    }

    [Fact]
    public async Task NotificationJson_HasNoPrivateData()
    {
        var (owner, ana, bia, groupId) = await ThreeMemberGroupAsync();
        var account = await h.CreateAccountAsync(ana, "Conta secreta", balance: 100m, referenceDate: h.Today.AddDays(-3));
        var tx = await h.CreateGroupTxAsync(owner, groupId, "Compra", 80m);
        await h.PayGroupTxAsync(ana, groupId, tx.Id, 20m, accountId: account.Id);

        foreach (var user in new[] { owner, ana, bia })
        {
            var raw = await GroupSupport.RawAsync(await user.Get("/api/v1/notifications?pageSize=50"));
            GroupSupport.AssertNoPrivateData(raw, owner.Session.Email, ana.Session.Email, bia.Session.Email, "Conta secreta", account.Id.ToString());
            using var doc = JsonDocument.Parse(raw);
            foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                Assert.Contains(item.GetProperty("type").GetString(), new[] { "GroupTransactionCreated", "GroupPaymentRegistered", "GroupInvitationReceived" });
            }
        }
    }

    // ------------------------------------------------------------------ convites

    [Fact]
    public async Task Invitation_ForAnExistingAccount_CreatesANotification_AndAcceptingItCreatesTheMembership()
    {
        var owner = await h.NewUserAsync("Maria Dona");
        var ana = await h.NewUserAsync("Ana");
        var group = await h.CreateGroupAsync(owner, "Casa");

        await h.InviteAsync(owner, group.Id, ana.Session.Email.ToUpperInvariant());

        Assert.Equal(1, await UnreadAsync(ana));
        var item = Assert.Single((await ListAsync(ana)).Items);
        Assert.Equal(NotificationType.GroupInvitationReceived, item.Type);
        Assert.Equal(("Casa", "Maria Dona"), (item.Group.Name, item.Actor!.Name));
        Assert.Null(item.Transaction);
        Assert.Equal(GroupInvitationStatus.Pending, item.Invitation!.Status);
        Assert.True(item.Invitation.CanRespond);
        // O e-mail também foi enviado (D-043).
        Assert.NotEmpty(h.Factory.Emails.To(ana.Session.Email));
        // O convite não vaza o e-mail do convidado na notificação.
        GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await ana.Get("/api/v1/notifications")), ana.Session.Email);

        var accepted = await ApiUser.Read<GroupSummaryDto>(await ana.Post($"/api/v1/notifications/{item.Id}/accept-invitation"));

        Assert.Equal((group.Id, GroupMemberRole.Member), (accepted.Id, accepted.MyRole));
        Assert.Equal(group.Id, (await ana.GetOk<GroupDetailDto>($"/api/v1/groups/{group.Id}")).Id);
        var after = Assert.Single((await ListAsync(ana)).Items.Where(n => n.Type == NotificationType.GroupInvitationReceived));
        Assert.True(after.IsRead);
        Assert.Equal((GroupInvitationStatus.Accepted, false), (after.Invitation!.Status, after.Invitation.CanRespond));
        Assert.Equal(0, await UnreadAsync(ana));
        // Usar de novo: já utilizado.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ana.Post($"/api/v1/notifications/{item.Id}/accept-invitation")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ana.Post($"/api/v1/notifications/{item.Id}/decline-invitation")).StatusCode);
    }

    [Fact]
    public async Task DecliningByNotification_MarksItDeclinedAndRead_WithoutMembership()
    {
        var owner = await h.NewUserAsync("Dona");
        var ana = await h.NewUserAsync("Ana");
        var group = await h.CreateGroupAsync(owner, "Casa");
        var invitation = await h.InviteAsync(owner, group.Id, ana.Session.Email);
        var item = Assert.Single((await ListAsync(ana)).Items);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.Post($"/api/v1/notifications/{item.Id}/decline-invitation")).StatusCode);

        var after = Assert.Single((await ListAsync(ana)).Items);
        Assert.True(after.IsRead);
        Assert.Equal((GroupInvitationStatus.Declined, false), (after.Invitation!.Status, after.Invitation.CanRespond));
        Assert.Equal(HttpStatusCode.NotFound, (await ana.Get($"/api/v1/groups/{group.Id}")).StatusCode);
        var invitations = await owner.GetOk<List<GroupInvitationDto>>($"/api/v1/groups/{group.Id}/invitations");
        Assert.Equal(GroupInvitationStatus.Declined, invitations.Single(i => i.Id == invitation.Id).Status);
    }

    [Fact]
    public async Task RevokedAndExpiredInvitations_CannotBeAnswered_AndShowCanRespondFalse()
    {
        var owner = await h.NewUserAsync("Dona");
        var revokedUser = await h.NewUserAsync("Revogada");
        var expiredUser = await h.NewUserAsync("Expirada");
        var group = await h.CreateGroupAsync(owner, "Casa");
        var revoked = await h.InviteAsync(owner, group.Id, revokedUser.Session.Email);
        var expired = await h.InviteAsync(owner, group.Id, expiredUser.Session.Email);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Delete($"/api/v1/groups/{group.Id}/invitations/{revoked.Id}")).StatusCode);
        await h.ExpireInvitationAsync(expired.Id);

        var revokedItem = Assert.Single((await ListAsync(revokedUser)).Items);
        var expiredItem = Assert.Single((await ListAsync(expiredUser)).Items);

        Assert.Equal((GroupInvitationStatus.Revoked, false), (revokedItem.Invitation!.Status, revokedItem.Invitation.CanRespond));
        Assert.Equal((GroupInvitationStatus.Expired, false), (expiredItem.Invitation!.Status, expiredItem.Invitation.CanRespond));
        foreach (var (user, item) in new[] { (revokedUser, revokedItem), (expiredUser, expiredItem) })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await user.Post($"/api/v1/notifications/{item.Id}/accept-invitation")).StatusCode);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await user.Post($"/api/v1/notifications/{item.Id}/decline-invitation")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await user.Get($"/api/v1/groups/{group.Id}")).StatusCode);
        }
    }

    [Fact]
    public async Task AccountEmailDifferentFromTheInvitation_Gets403_AndCannotRespond()
    {
        var owner = await h.NewUserAsync("Dona");
        var ana = await h.NewUserAsync("Ana");
        var group = await h.CreateGroupAsync(owner, "Casa");
        await h.InviteAsync(owner, group.Id, ana.Session.Email);
        var item = Assert.Single((await ListAsync(ana)).Items);
        // Simula a conta ter mudado de e-mail depois do convite.
        var newEmail = UniqueEmail("novo");
        await h.WithDbAsync(db => db.Users.Where(u => u.Id == ana.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, newEmail)));

        var after = Assert.Single((await ListAsync(ana)).Items);
        Assert.Equal((GroupInvitationStatus.Pending, false), (after.Invitation!.Status, after.Invitation.CanRespond));
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.Post($"/api/v1/notifications/{item.Id}/accept-invitation")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.Post($"/api/v1/notifications/{item.Id}/decline-invitation")).StatusCode);
    }

    [Fact]
    public async Task Resend_ReusesTheNotification_AndMarksItUnread()
    {
        var owner = await h.NewUserAsync("Dona");
        var ana = await h.NewUserAsync("Ana");
        var group = await h.CreateGroupAsync(owner, "Casa");
        var invitation = await h.InviteAsync(owner, group.Id, ana.Session.Email);
        var original = Assert.Single((await ListAsync(ana)).Items);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.Post($"/api/v1/notifications/{original.Id}/read")).StatusCode);
        Assert.Equal(0, await UnreadAsync(ana));

        Assert.Equal(HttpStatusCode.OK, (await owner.Post($"/api/v1/groups/{group.Id}/invitations/{invitation.Id}/resend")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Post($"/api/v1/groups/{group.Id}/invitations/{invitation.Id}/resend")).StatusCode);

        var page = await ListAsync(ana);
        var item = Assert.Single(page.Items);
        Assert.Equal(original.Id, item.Id);
        Assert.False(item.IsRead);
        Assert.True(item.CreatedAt >= original.CreatedAt);
        Assert.Equal(1, await UnreadAsync(ana));
        Assert.Equal(1, await h.WithDbAsync(db => db.Notifications.CountAsync(n => n.InvitationId == invitation.Id)));
        // O reenvio de um convite vencido também volta a ser acionável.
        await h.ExpireInvitationAsync(invitation.Id);
        Assert.False((await ListAsync(ana)).Items.Single().Invitation!.CanRespond);
        Assert.Equal(HttpStatusCode.OK, (await owner.Post($"/api/v1/groups/{group.Id}/invitations/{invitation.Id}/resend")).StatusCode);
        Assert.True((await ListAsync(ana)).Items.Single().Invitation!.CanRespond);
    }

    [Fact]
    public async Task Invitation_ForAnEmailWithoutAccount_NotifiesOnRegistration_ButNotForExpiredOrRevokedOnes()
    {
        var owner = await h.NewUserAsync("Dona");
        var group = await h.CreateGroupAsync(owner, "Casa");
        var other = await h.CreateGroupAsync(owner, "Viagem");
        var expiredGroup = await h.CreateGroupAsync(owner, "Antiga");
        var revokedGroup = await h.CreateGroupAsync(owner, "Revogada");
        var email = UniqueEmail("novato");
        var withoutAccountEmail = UniqueEmail("sem.conta");
        var first = await h.InviteAsync(owner, group.Id, email.ToUpperInvariant());
        var second = await h.InviteAsync(owner, other.Id, email);
        var expired = await h.InviteAsync(owner, expiredGroup.Id, email);
        var revoked = await h.InviteAsync(owner, revokedGroup.Id, email);
        var unrelated = await h.InviteAsync(owner, group.Id, withoutAccountEmail);
        await h.ExpireInvitationAsync(expired.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Delete($"/api/v1/groups/{revokedGroup.Id}/invitations/{revoked.Id}")).StatusCode);
        var invitationIds = new[] { first.Id, second.Id, expired.Id, revoked.Id, unrelated.Id };
        Assert.Equal(0, await h.WithDbAsync(db => db.Notifications.CountAsync(n => invitationIds.Contains(n.InvitationId!.Value))));

        var session = await h.Client.RegisterAsync(email, name: "Novata");
        var newcomer = new ApiUser(h.Client, session);

        var items = (await ListAsync(newcomer)).Items;
        Assert.Equal(["Casa", "Viagem"], items.Select(n => n.Group.Name).Order());
        Assert.All(items, n =>
        {
            Assert.Equal(NotificationType.GroupInvitationReceived, n.Type);
            Assert.True(n.Invitation!.CanRespond);
            Assert.Equal("Dona", n.Actor!.Name);
        });
        Assert.Equal(2, await UnreadAsync(newcomer));

        // Aceita um deles pela notificação, já logada na conta nova.
        var viagem = items.Single(n => n.Group.Name == "Viagem");
        Assert.Equal(HttpStatusCode.OK, (await newcomer.Post($"/api/v1/notifications/{viagem.Id}/accept-invitation")).StatusCode);
        Assert.Equal(1, await UnreadAsync(newcomer));
        // O convite de outro e-mail sem conta, o expirado e o revogado continuam sem notificação.
        Assert.Equal(2, await h.WithDbAsync(db => db.Notifications.CountAsync(n => n.RecipientUserId == newcomer.Id)));
        Assert.Equal(2, await h.WithDbAsync(db => db.Notifications.CountAsync(n => invitationIds.Contains(n.InvitationId!.Value))));
    }

    [Fact]
    public async Task Registration_WithoutPendingInvitations_CreatesNoNotifications()
    {
        var user = await h.NewUserAsync("Sem convites");

        Assert.Equal(0, await UnreadAsync(user));
        Assert.Empty((await ListAsync(user)).Items);
    }

    // ------------------------------------------------------------------ exclusão de conta e retenção

    [Fact]
    public async Task DeletingTheAccount_RemovesItsNotifications_AndKeepsOthersWithARemovedActor()
    {
        var (owner, ana, bia, groupId) = await ThreeMemberGroupAsync();
        await h.CreateGroupTxAsync(owner, groupId, "Da dona", 30m);
        await h.CreateGroupTxAsync(ana, groupId, "Da Ana", 20m);
        await h.InviteAsync(owner, groupId, UniqueEmail("pendente"));
        Assert.True(await h.WithDbAsync(db => db.Notifications.CountAsync(n => n.RecipientUserId == ana.Id)) >= 2);

        var response = await ana.SendAsync(HttpMethod.Delete, "/api/v1/users/me", new { password = DefaultPassword });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(0, await h.WithDbAsync(db => db.Notifications.CountAsync(n => n.RecipientUserId == ana.Id)));
        // Notificações de terceiros cujo ator era a Ana permanecem, agora com "Usuário removido".
        var mine = await ListAsync(owner);
        var fromAna = mine.Items.Single(n => n.Transaction?.Description == "Da Ana");
        Assert.Equal("Usuário removido", fromAna.Actor!.Name);
        GroupSupport.AssertNoPrivateData(await GroupSupport.RawAsync(await bia.Get("/api/v1/notifications")), ana.Session.Email);
    }

    [Fact]
    public async Task Retention_RemovesOnlyNotificationsReadMoreThan90DaysAgo()
    {
        var (owner, ana, _, groupId) = await ThreeMemberGroupAsync();
        foreach (var description in new[] { "Lida antiga", "Lida recente", "Nao lida antiga" })
        {
            await h.CreateGroupTxAsync(owner, groupId, description, 10m);
        }

        var items = GroupEvents(await ListAsync(ana));
        Guid IdOf(string description) => items.Single(n => n.Transaction!.Description == description).Id;
        var oldRead = IdOf("Lida antiga");
        var recentRead = IdOf("Lida recente");
        var oldUnread = IdOf("Nao lida antiga");
        await ana.Post($"/api/v1/notifications/{oldRead}/read");
        await ana.Post($"/api/v1/notifications/{recentRead}/read");
        var now = DateTimeOffset.UtcNow;
        await h.WithDbAsync(async db =>
        {
            await db.Notifications.Where(n => n.Id == oldRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now.AddDays(-91)));
            await db.Notifications.Where(n => n.Id == recentRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now.AddDays(-89)));
            await db.Notifications.Where(n => n.Id == oldUnread).ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedAt, now.AddDays(-200)));
            return 0;
        });

        var removed = await h.WithDbAsync(db => NotificationRetention.PurgeReadAsync(db, now, CancellationToken.None));

        Assert.Equal(1, removed);
        var remaining = (await ListAsync(ana)).Items.Select(n => n.Transaction?.Description).ToList();
        Assert.DoesNotContain("Lida antiga", remaining);
        Assert.Contains("Lida recente", remaining);
        Assert.Contains("Nao lida antiga", remaining);
        Assert.Equal(1, await UnreadAsync(ana));
    }

    [Fact]
    public async Task MigrationCreatesTheExpectedIndexes()
    {
        var names = await h.WithDbAsync(async db =>
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "select indexname from pg_indexes where tablename = 'notifications'";
            var list = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(reader.GetString(0));
            }

            return list;
        });

        Assert.Contains("ix_notifications_recipient_user_id_created_at", names);
        Assert.Contains("ix_notifications_recipient_user_id_unread", names);
        Assert.Contains("ux_notifications_recipient_user_id_invitation_id", names);
    }
}
