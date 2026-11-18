namespace FinanceApp.API.Security;

/// <summary>
/// Cookie do refresh token (D-005): HttpOnly, Secure (exceto Development), SameSite=Strict,
/// Path=/api/v1/auth, expirando junto com o token.
/// </summary>
public sealed class RefreshTokenCookie(IHostEnvironment environment)
{
    public const string Name = "gf_refresh";
    public const string CookiePath = "/api/v1/auth";

    public string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    public void Append(HttpResponse response, string token, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Append(Name, token, BuildOptions(expiresAt));
    }

    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Name, BuildOptions(expiresAt: null));
    }

    private CookieOptions BuildOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expiresAt,
        IsEssential = true,
    };
}
