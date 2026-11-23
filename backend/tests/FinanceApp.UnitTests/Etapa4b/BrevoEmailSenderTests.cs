using System.Net;
using System.Text.Json;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace FinanceApp.UnitTests.Etapa4b;

public class BrevoEmailSenderTests
{
    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{\"message\":\"segredo-do-destinatario\"}") };
        }
    }

    private static BrevoEmailSender Create(CapturingHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new BrevoOptions { ApiKey = "chave-secreta" }),
        Options.Create(new SmtpOptions { From = "no-reply@exemplo.com", FromName = "GroupFinance" }));

    private static readonly EmailMessage Message = new("ana@exemplo.com", "Ana", "Assunto", "texto", "<p>html</p>");

    [Fact]
    public async Task Send_PostsToBrevoApi_WithKeyHeaderAndPayload()
    {
        var handler = new CapturingHandler(HttpStatusCode.Created);
        await Create(handler).SendAsync(Message);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(BrevoEmailSender.Endpoint, handler.Request.RequestUri!.ToString());
        Assert.Equal("chave-secreta", handler.Request.Headers.GetValues("api-key").Single());
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("no-reply@exemplo.com", json.RootElement.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("ana@exemplo.com", json.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Assunto", json.RootElement.GetProperty("subject").GetString());
        Assert.Equal("<p>html</p>", json.RootElement.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task Send_OnFailure_ThrowsWithoutLeakingKeyOrResponseBody()
    {
        var handler = new CapturingHandler(HttpStatusCode.Unauthorized);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).SendAsync(Message));
        Assert.Contains("401", ex.Message);
        Assert.DoesNotContain("chave-secreta", ex.Message);
        Assert.DoesNotContain("segredo-do-destinatario", ex.Message);
    }

    [Fact]
    public void Options_Enabled_OnlyWithApiKey()
    {
        Assert.False(new BrevoOptions().Enabled);
        Assert.False(new BrevoOptions { ApiKey = "  " }.Enabled);
        Assert.True(new BrevoOptions { ApiKey = "k" }.Enabled);
    }
}
