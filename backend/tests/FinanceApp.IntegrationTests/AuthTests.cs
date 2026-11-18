using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceApp.Domain.Auth;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();

    [Fact]
    public async Task Register_Returns201_WithBodyAndSecureRefreshCookie()
    {
        var email = UniqueEmail("Maria");
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "  Maria Silva ", email = $"  {email.ToUpperInvariant()} ", password = DefaultPassword });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/users/me", response.Headers.Location?.OriginalString);

        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<AuthBody>(raw, Json)!;
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
        Assert.Equal(email.ToLowerInvariant(), body.User.Email);
        Assert.Equal("Maria Silva", body.User.Name);
        Assert.True(body.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(14));
        Assert.DoesNotContain("refresh", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("\"createdAt\":\"[0-9T:.\\-]+Z\"", raw);

        var cookie = GetRefreshSetCookie(response)!.ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/v1/auth", cookie);
        Assert.Contains("expires=", cookie);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var session = await client.RegisterAsync();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "Outra", email = session.Email.ToUpperInvariant(), password = DefaultPassword });

        await ReadProblemAsync(response, HttpStatusCode.Conflict, "email_already_registered");
    }

    [Fact]
    public async Task Register_InvalidData_Returns400WithFieldErrors()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "A", email = "invalido", password = "curta" });

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        var errors = problem.GetProperty("errors");
        Assert.Equal("O nome deve ter entre 2 e 100 caracteres.", errors.GetProperty("name")[0].GetString());
        Assert.Equal("E-mail inválido.", errors.GetProperty("email")[0].GetString());
        Assert.Equal("A senha deve ter ao menos 8 caracteres.", errors.GetProperty("password")[0].GetString());
    }

    [Fact]
    public async Task Register_MalformedJson_Returns400ValidationError()
    {
        using var content = new StringContent("{\"email\":", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/auth/register", content);

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithCookie()
    {
        var registered = await client.RegisterAsync();

        var session = await client.LoginAsync($" {registered.Email.ToUpperInvariant()} ");

        Assert.Equal(registered.Auth.User.Id, session.Auth.User.Id);
        Assert.NotEqual(registered.RefreshCookie, session.RefreshCookie);
    }

    [Fact]
    public async Task Login_InvalidCredentials_Returns401Generic()
    {
        var registered = await client.RegisterAsync();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = registered.Email, password = "Errada123" });
        var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = UniqueEmail(), password = DefaultPassword });

        var p1 = await ReadProblemAsync(wrongPassword, HttpStatusCode.Unauthorized, "invalid_credentials");
        var p2 = await ReadProblemAsync(unknownEmail, HttpStatusCode.Unauthorized, "invalid_credentials");
        Assert.Equal(p1.GetProperty("title").GetString(), p2.GetProperty("title").GetString());
        Assert.Null(GetRefreshSetCookie(wrongPassword));
    }

    [Fact]
    public async Task Login_MissingFields_Returns400()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { });
        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(problem.GetProperty("errors").TryGetProperty("email", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndNewAccessTokenWorks()
    {
        var session = await client.RegisterAsync();

        var response = await client.RefreshAsync(session.RefreshCookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthBody>(Json);
        var newCookie = GetRefreshCookieValue(response);
        Assert.NotEqual(session.RefreshCookie, newCookie);
        Assert.Equal(session.Auth.User.Id, body!.User.Id);

        var me = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", body.AccessToken));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReuseOfRotatedToken_Returns401AndRevokesFamily()
    {
        var session = await client.RegisterAsync();
        var rotated = await client.RefreshAsync(session.RefreshCookie);
        var currentCookie = GetRefreshCookieValue(rotated);

        var reuse = await client.RefreshAsync(session.RefreshCookie);

        await ReadProblemAsync(reuse, HttpStatusCode.Unauthorized, "invalid_refresh_token");
        AssertCookieDeleted(reuse);

        // O token legítimo mais recente da mesma família também foi revogado.
        var afterReuse = await client.RefreshAsync(currentCookie);
        await ReadProblemAsync(afterReuse, HttpStatusCode.Unauthorized, "invalid_refresh_token");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = await db.RefreshTokens.Where(t => t.UserId == session.Auth.User.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
        Assert.Contains(tokens, t => t.RevocationReason == RefreshTokenRevocationReason.ReuseDetected);
        Assert.Single(tokens.Select(t => t.FamilyId).Distinct());
    }

    [Fact]
    public async Task Refresh_WithoutCookieOrWithGarbage_Returns401()
    {
        await ReadProblemAsync(await client.RefreshAsync(null), HttpStatusCode.Unauthorized, "invalid_refresh_token");
        await ReadProblemAsync(await client.RefreshAsync("lixo"), HttpStatusCode.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_AndClearsCookie()
    {
        var session = await client.RegisterAsync();

        var logout = await client.LogoutAsync(session.RefreshCookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        AssertCookieDeleted(logout);
        await ReadProblemAsync(await client.RefreshAsync(session.RefreshCookie), HttpStatusCode.Unauthorized, "invalid_refresh_token");
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns204()
    {
        var logout = await client.LogoutAsync(null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns202WithoutSendingEmail()
    {
        var email = UniqueEmail();
        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(factory.Emails.To(email));
    }

    [Fact]
    public async Task ForgotPassword_InvalidEmail_Returns400()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "x" });
        await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task ForgotAndReset_ChangesPassword_RevokesSessions_TokenIsSingleUse()
    {
        var session = await client.RegisterAsync();

        var forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = session.Email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var message = Assert.Single(factory.Emails.To(session.Email));
        Assert.Contains($"{ApiFactory.FrontendBaseUrl}/reset-password?token=", message.TextBody, StringComparison.Ordinal);
        var token = factory.Emails.LastResetTokenFor(session.Email);

        // Somente o hash é armazenado.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.PasswordResetTokens.AnyAsync(t => t.TokenHash == token));
        }

        var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { token, newPassword = "NovaSenha123" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        await ReadProblemAsync(await client.RefreshAsync(session.RefreshCookie), HttpStatusCode.Unauthorized, "invalid_refresh_token");
        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = session.Email, password = DefaultPassword });
        await ReadProblemAsync(oldLogin, HttpStatusCode.Unauthorized, "invalid_credentials");
        await client.LoginAsync(session.Email, "NovaSenha123");

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { token, newPassword = "OutraSenha123" });
        await ReadProblemAsync(reuse, HttpStatusCode.BadRequest, "invalid_token");
    }

    [Fact]
    public async Task ForgotPassword_NewRequestInvalidatesPreviousToken()
    {
        var session = await client.RegisterAsync();
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = session.Email });
        var first = factory.Emails.LastResetTokenFor(session.Email);
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = session.Email });
        var second = factory.Emails.LastResetTokenFor(session.Email);

        var withFirst = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { token = first, newPassword = "NovaSenha123" });
        await ReadProblemAsync(withFirst, HttpStatusCode.BadRequest, "invalid_token");

        var withSecond = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { token = second, newPassword = "NovaSenha123" });
        Assert.Equal(HttpStatusCode.NoContent, withSecond.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns400InvalidToken()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { token = "token-inexistente", newPassword = "NovaSenha123" });
        await ReadProblemAsync(response, HttpStatusCode.BadRequest, "invalid_token");
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_Returns400ValidationError()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { token = "qualquer", newPassword = "fraca" });
        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(problem.GetProperty("errors").TryGetProperty("newPassword", out _));
    }

    private static void AssertCookieDeleted(HttpResponseMessage response)
    {
        var header = GetRefreshSetCookie(response);
        Assert.NotNull(header);
        Assert.StartsWith($"{CookieName}=;", header, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", header, StringComparison.OrdinalIgnoreCase);
    }
}
