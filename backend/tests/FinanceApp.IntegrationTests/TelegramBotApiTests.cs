using System.Net;
using System.Net.Http.Json;
using FinanceApp.Application.Telegram;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Categories;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>Bot do Telegram (contrato §18, D-053/D-054) sobre a API real, com o cliente do Telegram simulado.</summary>
[Collection(ApiCollection.Name)]
public class TelegramBotApiTests(ApiFactory factory)
{
    private static long nextChat = 7_000_000_000;
    private readonly FinancialHarness h = new(factory);
    private readonly HttpClient raw = factory.CreateApiClient();

    private static long NewChat() => Interlocked.Increment(ref nextChat);

    private async Task<HttpResponseMessage> WebhookAsync(object update, string? secret = ApiFactory.TelegramSecret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telegram/webhook") { Content = JsonContent.Create(update) };
        if (secret is not null)
        {
            request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        }

        return await raw.SendAsync(request);
    }

    private async Task SayAsync(long chat, string text)
    {
        var response = await WebhookAsync(new { update_id = 1, message = new { message_id = 10, chat = new { id = chat, type = "private" }, text } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task PressAsync(long chat, string data)
    {
        var response = await WebhookAsync(new { update_id = 2, callback_query = new { id = "cb1", data, message = new { message_id = 11, chat = new { id = chat, type = "private" } } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<long> LinkedChatAsync(ApiUser user)
    {
        var code = await ApiUser.Read<TelegramLinkCodeDto>(await user.Post("/api/v1/telegram/link-code"));
        var chat = NewChat();
        await SayAsync(chat, "/start " + code.Code);
        Assert.Contains("vinculada", factory.Telegram.Last.Text);
        return chat;
    }

    private static string CallbackFor(SentTelegramMessage prompt, string categoryName) =>
        prompt.Keyboard!.SelectMany(r => r).Single(b => b.Text == categoryName).CallbackData;

    [Fact]
    public async Task Webhook_WithoutOrWithWrongSecret_IsNotFound_AndDoesNothing()
    {
        var before = factory.Telegram.Sent.Count;
        var update = new { update_id = 1, message = new { message_id = 1, chat = new { id = NewChat(), type = "private" }, text = "/ajuda" } };

        Assert.Equal(HttpStatusCode.NotFound, (await WebhookAsync(update, secret: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await WebhookAsync(update, secret: "outro-segredo-qualquer")).StatusCode);
        Assert.Equal(before, factory.Telegram.Sent.Count);
    }

    [Fact]
    public async Task LinkEndpoints_RequireAuthentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await raw.PostAsync("/api/v1/telegram/link-code", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await raw.GetAsync("/api/v1/telegram/link")).StatusCode);
    }

    [Fact]
    public async Task LinkFlow_CodeIsSingleUse_StatusReflectsLink_AndUnlinkWorks()
    {
        var user = await h.NewUserAsync("Ana Bot");
        var status = await user.GetOk<TelegramLinkStatusDto>("/api/v1/telegram/link");
        Assert.True(status.Configured);
        Assert.False(status.Linked);

        var code = await ApiUser.Read<TelegramLinkCodeDto>(await user.Post("/api/v1/telegram/link-code"));
        Assert.Equal(8, code.Code.Length);
        Assert.EndsWith(code.Code, code.DeepLink);

        var chat = NewChat();
        await SayAsync(chat, "/vincular " + code.Code.ToLowerInvariant());
        Assert.Contains("vinculada", factory.Telegram.Last.Text);
        Assert.True((await user.GetOk<TelegramLinkStatusDto>("/api/v1/telegram/link")).Linked);

        // O código é de uso único.
        var other = NewChat();
        await SayAsync(other, "/vincular " + code.Code);
        Assert.Contains("inválido", factory.Telegram.Last.Text);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Delete("/api/v1/telegram/link")).StatusCode);
        Assert.False((await user.GetOk<TelegramLinkStatusDto>("/api/v1/telegram/link")).Linked);
        await SayAsync(chat, "45,90 mercado");
        Assert.Contains("não está vinculado", factory.Telegram.Last.Text);
    }

    [Fact]
    public async Task NewCode_InvalidatesThePreviousOne()
    {
        var user = await h.NewUserAsync("Bia Bot");
        var first = await ApiUser.Read<TelegramLinkCodeDto>(await user.Post("/api/v1/telegram/link-code"));
        var second = await ApiUser.Read<TelegramLinkCodeDto>(await user.Post("/api/v1/telegram/link-code"));

        var chat = NewChat();
        await SayAsync(chat, "/vincular " + first.Code);
        Assert.Contains("inválido", factory.Telegram.Last.Text);
        await SayAsync(chat, "/vincular " + second.Code);
        Assert.Contains("vinculada", factory.Telegram.Last.Text);
    }

    [Fact]
    public async Task Chat_AlreadyLinkedToAnotherAccount_CannotBeLinkedAgain()
    {
        var ana = await h.NewUserAsync("Ana Um");
        var bia = await h.NewUserAsync("Bia Dois");
        var chat = await LinkedChatAsync(ana);

        var code = await ApiUser.Read<TelegramLinkCodeDto>(await bia.Post("/api/v1/telegram/link-code"));
        await SayAsync(chat, "/vincular " + code.Code);
        Assert.Contains("outra conta", factory.Telegram.Last.Text);
        Assert.False((await bia.GetOk<TelegramLinkStatusDto>("/api/v1/telegram/link")).Linked);
    }

    [Fact]
    public async Task Expense_TypedInChat_BecomesIndividualTransaction_AfterChoosingCategory()
    {
        var user = await h.NewUserAsync("Carla Bot");
        var chat = await LinkedChatAsync(user);

        await SayAsync(chat, "45,90 mercado do bairro");
        var prompt = factory.Telegram.Last;
        Assert.Contains("R$ 45,90", prompt.Text);
        await PressAsync(chat, CallbackFor(prompt, "Alimentação"));

        Assert.Contains("✅", factory.Telegram.Last.Text);
        var list = await h.ListAsync(user);
        var tx = Assert.Single(list.Items);
        Assert.Equal("mercado do bairro", tx.Description);
        Assert.Equal(45.90m, tx.Amount);
        Assert.Equal("Expense", tx.Type.ToString());
        Assert.Null(tx.AccountId);
        Assert.Equal(factory.Clock.Today, tx.TransactionDate);

        // O botão não pode ser reutilizado (a despesa pendente foi consumida).
        await PressAsync(chat, CallbackFor(prompt, "Alimentação"));
        Assert.Contains("expirou", factory.Telegram.Last.Text);
        Assert.Single((await h.ListAsync(user)).Items);
    }

    [Theory]
    [InlineData("mercado 45,90")]
    [InlineData("/despesa R$ 45,90 mercado")]
    [InlineData("45.90 mercado")]
    public async Task Expense_AcceptedFormats_AllParseTheSameAmount(string text)
    {
        var user = await h.NewUserAsync("Formato Bot");
        var chat = await LinkedChatAsync(user);
        await SayAsync(chat, text);
        Assert.Contains("R$ 45,90", factory.Telegram.Last.Text);
    }

    [Theory]
    [InlineData("oi tudo bem")]
    [InlineData("45,90")]
    [InlineData("2 pizzas 45,90")]
    public async Task Expense_Unrecognized_AsksAgain_AndCreatesNothing(string text)
    {
        var user = await h.NewUserAsync("Confuso Bot");
        var chat = await LinkedChatAsync(user);
        await SayAsync(chat, text);
        Assert.Contains("Não entendi", factory.Telegram.Last.Text);
        Assert.Empty((await h.ListAsync(user)).Items);
    }

    [Fact]
    public async Task Cancel_DiscardsThePendingExpense()
    {
        var user = await h.NewUserAsync("Cancela Bot");
        var chat = await LinkedChatAsync(user);
        await SayAsync(chat, "10 café");
        var prompt = factory.Telegram.Last;
        await PressAsync(chat, prompt.Keyboard!.SelectMany(r => r).Single(b => b.Text == "Cancelar").CallbackData);
        Assert.Contains("cancelado", factory.Telegram.Last.Text);
        Assert.Empty((await h.ListAsync(user)).Items);
    }

    [Fact]
    public async Task PendingExpense_FromAnotherUser_CannotBeConfirmedByMyChat()
    {
        var ana = await h.NewUserAsync("Ana Dona");
        var bia = await h.NewUserAsync("Bia Intrusa");
        var anaChat = await LinkedChatAsync(ana);
        var biaChat = await LinkedChatAsync(bia);

        await SayAsync(anaChat, "99 segredo");
        var anaPrompt = factory.Telegram.Last;
        await PressAsync(biaChat, CallbackFor(anaPrompt, "Alimentação"));

        Assert.Contains("expirou", factory.Telegram.Last.Text);
        Assert.Empty((await h.ListAsync(ana)).Items);
        Assert.Empty((await h.ListAsync(bia)).Items);
    }

    [Fact]
    public async Task PendingExpense_Expired_IsRejected()
    {
        var user = await h.NewUserAsync("Expira Bot");
        var chat = await LinkedChatAsync(user);
        await SayAsync(chat, "20 lanche");
        var prompt = factory.Telegram.Last;
        await h.WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE telegram_pending_expenses SET created_at = created_at - interval '1 hour'");
            return 0;
        });

        await PressAsync(chat, CallbackFor(prompt, "Alimentação"));
        Assert.Contains("expirou", factory.Telegram.Last.Text);
        Assert.Empty((await h.ListAsync(user)).Items);
    }

    [Fact]
    public async Task Categories_OfAnotherUser_AreNeverOffered_NorAccepted()
    {
        var ana = await h.NewUserAsync("Ana Cat");
        var bia = await h.NewUserAsync("Bia Cat");
        var secret = await h.CreateCategoryAsync(bia, "Categoria Privada Da Bia");
        var chat = await LinkedChatAsync(ana);

        await SayAsync(chat, "30 teste");
        var prompt = factory.Telegram.Last;
        Assert.DoesNotContain(prompt.Keyboard!.SelectMany(r => r), b => b.Text == "Categoria Privada Da Bia");

        var pendingId = prompt.Keyboard!.SelectMany(r => r).First().CallbackData.Split(':')[1];
        await PressAsync(chat, $"c:{pendingId}:{secret.Id:N}");
        Assert.Contains("indisponível", factory.Telegram.Last.Text);
        Assert.Empty((await h.ListAsync(ana)).Items);
    }

    [Fact]
    public async Task Unlink_ByCommand_RemovesTheLink()
    {
        var user = await h.NewUserAsync("Sai Bot");
        var chat = await LinkedChatAsync(user);
        await SayAsync(chat, "/desvincular");
        Assert.Contains("removido", factory.Telegram.Last.Text);
        Assert.False((await user.GetOk<TelegramLinkStatusDto>("/api/v1/telegram/link")).Linked);
    }

    [Fact]
    public async Task DeletingTheAccount_RemovesLinkCodesAndPendingExpenses()
    {
        var user = await h.NewUserAsync("Exclui Bot");
        var chat = await LinkedChatAsync(user);
        await SayAsync(chat, "15 lanche");
        await user.Post("/api/v1/telegram/link-code");

        Assert.Equal(HttpStatusCode.NoContent, (await user.SendAsync(HttpMethod.Delete, "/api/v1/users/me", new { password = DefaultPassword })).StatusCode);

        var counts = await h.WithDbAsync(async db => (
            await db.TelegramLinks.CountAsync(l => l.ChatId == chat),
            await db.TelegramPendingExpenses.CountAsync(p => p.Description == "lanche"),
            await db.TelegramLinkCodes.CountAsync()));
        Assert.Equal(0, counts.Item1);
        Assert.Equal(0, counts.Item2);
        await SayAsync(chat, "15 lanche");
        Assert.Contains("não está vinculado", factory.Telegram.Last.Text);
    }

    [Fact]
    public async Task Group_Chats_AreIgnored()
    {
        var before = factory.Telegram.Sent.Count;
        var response = await WebhookAsync(new { update_id = 3, message = new { message_id = 1, chat = new { id = NewChat(), type = "group" }, text = "/ajuda" } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, factory.Telegram.Sent.Count);
    }
}
