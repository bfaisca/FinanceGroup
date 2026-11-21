using System.Net;
using System.Text.Json;
using FinanceApp.Application.Groups;
using FinanceApp.Domain.Categories;

namespace FinanceApp.IntegrationTests.Infrastructure;

/// <summary>Helpers de cenário da Etapa 4 (grupos, convites e movimentações de grupo) sobre a API real.</summary>
public static class GroupSupport
{
    public static async Task<GroupDetailDto> CreateGroupAsync(this FinancialHarness h, ApiUser owner, string name = "Casa", string? description = null)
    {
        var response = await owner.Post("/api/v1/groups", new { name, description });
        return await ApiUser.Read<GroupDetailDto>(response, HttpStatusCode.Created);
    }

    public static async Task<GroupInvitationDto> InviteAsync(this FinancialHarness h, ApiUser admin, Guid groupId, string email)
    {
        var response = await admin.Post($"/api/v1/groups/{groupId}/invitations", new { email });
        return await ApiUser.Read<GroupInvitationDto>(response, HttpStatusCode.Created);
    }

    /// <summary>Convida o usuário pelo e-mail dele e o faz aceitar (fluxo completo, com o token do e-mail).</summary>
    public static async Task<GroupSummaryDto> JoinAsync(this FinancialHarness h, ApiUser admin, Guid groupId, ApiUser invitee)
    {
        await h.InviteAsync(admin, groupId, invitee.Session.Email);
        var token = h.Factory.Emails.LastInvitationTokenFor(invitee.Session.Email);
        var response = await invitee.Post($"/api/v1/invitations/{token}/accept");
        return await ApiUser.Read<GroupSummaryDto>(response);
    }

    public static async Task<GroupMemberDto> MemberOfAsync(this FinancialHarness h, ApiUser viewer, Guid groupId, ApiUser who, bool includeFormer = false)
    {
        var members = await viewer.GetOk<List<GroupMemberDto>>($"/api/v1/groups/{groupId}/members?includeFormer={includeFormer}");
        return members.Single(m => m.UserId == who.Id);
    }

    public static object GroupTxBody(
        string description,
        decimal amount,
        Guid categoryId,
        DateOnly date,
        DateOnly? due = null,
        string? status = null,
        string? notes = null,
        object? payment = null,
        object? recurrence = null) =>
        new { description, amount, categoryId, transactionDate = date, dueDate = due, status, notes, payment, recurrence };

    public static async Task<GroupTransactionDetailDto> CreateGroupTxAsync(
        this FinancialHarness h,
        ApiUser user,
        Guid groupId,
        string description,
        decimal amount,
        DateOnly? date = null,
        Guid? categoryId = null,
        DateOnly? due = null,
        object? payment = null,
        object? recurrence = null)
    {
        var category = categoryId ?? await h.CategoryIdAsync(user, "Alimentação", CategoryType.Expense);
        var response = await user.Post(
            $"/api/v1/groups/{groupId}/transactions",
            GroupTxBody(description, amount, category, date ?? h.Today, due, payment: payment, recurrence: recurrence));
        return await ApiUser.Read<GroupTransactionDetailDto>(response, HttpStatusCode.Created);
    }

    public static async Task<GroupTransactionDetailDto> PayGroupTxAsync(
        this FinancialHarness h,
        ApiUser user,
        Guid groupId,
        Guid transactionId,
        decimal amount,
        DateOnly? date = null,
        Guid? accountId = null,
        decimal? interest = null)
    {
        var response = await user.Post(
            $"/api/v1/groups/{groupId}/transactions/{transactionId}/payments",
            new { amount, interestAmount = interest, paymentDate = date ?? h.Today, accountId });
        return await ApiUser.Read<GroupTransactionDetailDto>(response, HttpStatusCode.Created);
    }

    /// <summary>Todas as ocorrências/despesas do grupo, da mais antiga para a mais nova.</summary>
    public static async Task<List<GroupTransactionDto>> ListGroupTxAsync(this FinancialHarness h, ApiUser user, Guid groupId, string query = "")
    {
        var page = await user.GetOk<FinanceApp.Application.Common.PagedResult<GroupTransactionDto>>(
            $"/api/v1/groups/{groupId}/transactions?sort=transactionDate&pageSize=100{(query.Length > 0 ? "&" + query : string.Empty)}");
        return page.Items.ToList();
    }

    /// <summary>Altera o relógio de uma regra direto no banco (ex.: forçar a expiração de um convite).</summary>
    public static async Task ExpireInvitationAsync(this FinancialHarness h, Guid invitationId)
    {
        await h.WithDbAsync(async db =>
        {
            await Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.ExecuteUpdateAsync(
                db.GroupInvitations.Where(i => i.Id == invitationId),
                s => s.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
            return 0;
        });
    }

    /// <summary>Verifica que o JSON bruto não contém dados privados de conta/cartão nem e-mails.</summary>
    public static void AssertNoPrivateData(string rawJson, params string[] forbiddenValues)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var names = new List<string>();
        Collect(doc.RootElement, names);
        Assert.DoesNotContain(names, n => n.Equals("accountId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Equals("accountName", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Equals("creditCardId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Equals("creditCardName", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Equals("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("balance", StringComparison.OrdinalIgnoreCase) && !n.Equals("balance", StringComparison.Ordinal));
        foreach (var value in forbiddenValues)
        {
            Assert.DoesNotContain(value, rawJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void Collect(JsonElement element, List<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    Collect(property.Value, names);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, names);
                }

                break;
        }
    }

    public static async Task<string> RawAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Esperado {(int)expected} mas recebeu {(int)response.StatusCode}: {raw}");
        return raw;
    }
}
