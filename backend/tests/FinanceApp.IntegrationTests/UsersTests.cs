using System.Net;
using System.Net.Http.Json;
using FinanceApp.Domain.Users;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class UsersTests(ApiFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();

    [Fact]
    public async Task GetMe_WithoutToken_Returns401ProblemDetails()
    {
        var response = await client.GetAsync("/api/v1/users/me");

        await ReadProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMe_WithInvalidToken_Returns401ProblemDetails()
    {
        var response = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", "nao.e.um.jwt"));
        await ReadProblemAsync(response, HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task GetMe_WithToken_ReturnsUser()
    {
        var session = await client.RegisterAsync();

        var response = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", session.Auth.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserBody>(Json);
        Assert.Equal(session.Auth.User, user);
    }

    [Fact]
    public async Task UpdateMe_ChangesName()
    {
        var session = await client.RegisterAsync();

        var response = await client.SendAsync(Authorized(HttpMethod.Put, "/api/v1/users/me", session.Auth.AccessToken, new { name = "  Maria S. Silva " }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserBody>(Json);
        Assert.Equal("Maria S. Silva", user!.Name);
        Assert.Equal(session.Auth.User.Email, user.Email);
    }

    [Fact]
    public async Task UpdateMe_InvalidName_Returns400()
    {
        var session = await client.RegisterAsync();
        var response = await client.SendAsync(Authorized(HttpMethod.Put, "/api/v1/users/me", session.Auth.AccessToken, new { name = "" }));
        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.True(problem.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherSessions_KeepsCurrent()
    {
        var sessionA = await client.RegisterAsync();
        var sessionB = await client.LoginAsync(sessionA.Email);

        var response = await client.SendAsync(Authorized(
            HttpMethod.Put, "/api/v1/users/me/password", sessionA.Auth.AccessToken,
            new { currentPassword = DefaultPassword, newPassword = "NovaSenha123" }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.RefreshAsync(sessionA.RefreshCookie)).StatusCode);
        await ReadProblemAsync(await client.RefreshAsync(sessionB.RefreshCookie), HttpStatusCode.Unauthorized, "invalid_refresh_token");
        await client.LoginAsync(sessionA.Email, "NovaSenha123");
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400InvalidPassword()
    {
        var session = await client.RegisterAsync();
        var response = await client.SendAsync(Authorized(
            HttpMethod.Put, "/api/v1/users/me/password", session.Auth.AccessToken,
            new { currentPassword = "Errada123", newPassword = "NovaSenha123" }));

        await ReadProblemAsync(response, HttpStatusCode.BadRequest, "invalid_password");
    }

    [Fact]
    public async Task ChangePassword_SameAsCurrent_Returns400Validation()
    {
        var session = await client.RegisterAsync();
        var response = await client.SendAsync(Authorized(
            HttpMethod.Put, "/api/v1/users/me/password", session.Auth.AccessToken,
            new { currentPassword = DefaultPassword, newPassword = DefaultPassword }));

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.Equal("A nova senha deve ser diferente da atual.", problem.GetProperty("errors").GetProperty("newPassword")[0].GetString());
    }

    [Fact]
    public async Task DeleteAccount_WrongPassword_Returns400()
    {
        var session = await client.RegisterAsync();
        var response = await client.SendAsync(Authorized(HttpMethod.Delete, "/api/v1/users/me", session.Auth.AccessToken, new { password = "Errada123" }));
        await ReadProblemAsync(response, HttpStatusCode.BadRequest, "invalid_password");
    }

    [Fact]
    public async Task DeleteAccount_AnonymizesUser_RevokesTokens_AndBlocksLogin()
    {
        var session = await client.RegisterAsync();
        var otherSession = await client.LoginAsync(session.Email);

        var response = await client.SendAsync(Authorized(HttpMethod.Delete, "/api/v1/users/me", session.Auth.AccessToken, new { password = DefaultPassword }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.StartsWith($"{CookieName}=;", GetRefreshSetCookie(response), StringComparison.Ordinal);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = session.Email, password = DefaultPassword });
        await ReadProblemAsync(login, HttpStatusCode.Unauthorized, "invalid_credentials");
        await ReadProblemAsync(await client.RefreshAsync(session.RefreshCookie), HttpStatusCode.Unauthorized, "invalid_refresh_token");
        await ReadProblemAsync(await client.RefreshAsync(otherSession.RefreshCookie), HttpStatusCode.Unauthorized, "invalid_refresh_token");

        // Access token ainda não expirado não dá mais acesso.
        var me = await client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/users/me", session.Auth.AccessToken));
        await ReadProblemAsync(me, HttpStatusCode.Unauthorized, "unauthorized");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == session.Auth.User.Id);
            Assert.False(user.IsActive);
            Assert.Equal(User.AnonymizedName, user.Name);
            Assert.NotEqual(session.Email, user.Email);
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == user.Id && t.RevokedAt == null));
        }

        // O e-mail pode ser usado em um novo cadastro.
        await client.RegisterAsync(session.Email);
    }
}
