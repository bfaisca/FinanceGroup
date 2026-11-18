using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceApp.IntegrationTests.Infrastructure;

public sealed record UserBody(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

public sealed record AuthBody(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserBody User);

public sealed record Session(AuthBody Auth, string RefreshCookie, string Email, string Password);

public static class ApiTestHelpers
{
    public const string CookieName = "gf_refresh";
    public const string DefaultPassword = "Senha123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string UniqueEmail(string prefix = "user") => $"{prefix}.{Guid.NewGuid():N}@exemplo.com";

    public static async Task<Session> RegisterAsync(this HttpClient client, string? email = null, string password = DefaultPassword, string name = "Maria Silva")
    {
        email ??= UniqueEmail();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { name, email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ToSessionAsync(response, email, password);
    }

    public static async Task<Session> LoginAsync(this HttpClient client, string email, string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ToSessionAsync(response, email, password);
    }

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? refreshCookie) =>
        client.SendAsync(WithCookie(HttpMethod.Post, "/api/v1/auth/refresh", refreshCookie));

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string? refreshCookie) =>
        client.SendAsync(WithCookie(HttpMethod.Post, "/api/v1/auth/logout", refreshCookie));

    public static HttpRequestMessage Authorized(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public static HttpRequestMessage WithCookie(HttpMethod method, string url, string? refreshCookie)
    {
        var request = new HttpRequestMessage(method, url);
        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={refreshCookie}");
        }

        return request;
    }

    /// <summary>Linha Set-Cookie do refresh token (ou null).</summary>
    public static string? GetRefreshSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    public static string GetRefreshCookieValue(HttpResponseMessage response)
    {
        var header = GetRefreshSetCookie(response) ?? throw new InvalidOperationException("Set-Cookie gf_refresh ausente.");
        var value = header[(CookieName.Length + 1)..].Split(';')[0];
        Assert.False(string.IsNullOrEmpty(value));
        return value;
    }

    public static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.Equal($"https://httpstatuses.io/{(int)expectedStatus}", problem.GetProperty("type").GetString());
        return problem;
    }

    private static async Task<Session> ToSessionAsync(HttpResponseMessage response, string email, string password)
    {
        var body = await response.Content.ReadFromJsonAsync<AuthBody>(Json) ?? throw new InvalidOperationException("Corpo vazio.");
        return new Session(body, GetRefreshCookieValue(response), email, password);
    }
}
