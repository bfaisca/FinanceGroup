using FinanceApp.Application.Auth;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Emails;
using FinanceApp.Application.Users;
using FinanceApp.Domain.Auth;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Security;
using FinanceApp.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace FinanceApp.UnitTests.Auth;

public sealed class AuthServiceTests : IDisposable
{
    private readonly AppDbContext db;
    private readonly FakeClock clock = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly CapturingEmailSender email = new();
    private readonly SecureTokenService tokens = new();
    private readonly PasswordHasher hasher = new();
    private readonly FakeCurrentUser currentUser = new();
    private readonly AuthService auth;
    private readonly UserService users;

    public AuthServiceTests()
    {
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var jwtOptions = MsOptions.Create(new JwtOptions
        {
            Secret = "unit-test-secret-with-at-least-32-characters!!",
            Issuer = "GroupFinance",
            Audience = "GroupFinance.Web",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        });
        var appOptions = MsOptions.Create(new AppOptions { FrontendBaseUrl = "http://front.test/", PasswordResetTokenMinutes = 60 });

        auth = new AuthService(
            db, hasher, new JwtTokenGenerator(jwtOptions, clock), tokens, email, new PasswordResetEmailFactory(appOptions), clock,
            jwtOptions, appOptions,
            new RegisterRequestValidator(), new LoginRequestValidator(), new ForgotPasswordRequestValidator(), new ResetPasswordRequestValidator(),
            NullLogger<AuthService>.Instance);

        users = new UserService(
            db, currentUser, hasher, clock,
            new UpdateProfileRequestValidator(), new ChangePasswordRequestValidator(), new DeleteAccountRequestValidator(),
            NullLogger<UserService>.Instance);
    }

    public void Dispose() => db.Dispose();

    private Task<AuthResult> RegisterAsync(string mail = "maria@exemplo.com", string password = "Senha123") =>
        auth.RegisterAsync(new RegisterRequest { Name = "Maria", Email = mail, Password = password }, "127.0.0.1", CancellationToken.None);

    [Fact]
    public async Task Register_PersistsUserWithHashedPasswordAndHashedRefreshToken()
    {
        var result = await RegisterAsync(" Maria@Exemplo.com ");

        var user = await db.Users.SingleAsync();
        Assert.Equal("maria@exemplo.com", user.Email);
        Assert.NotEqual("Senha123", user.PasswordHash);

        var stored = await db.RefreshTokens.SingleAsync();
        Assert.Equal(tokens.Hash(result.RefreshToken), stored.TokenHash);
        Assert.NotEqual(result.RefreshToken, stored.TokenHash);
        Assert.Equal(clock.UtcNow.AddDays(7), result.RefreshTokenExpiresAt);
        Assert.Equal(clock.UtcNow.AddMinutes(15), result.Response.AccessTokenExpiresAt);
        Assert.Equal(user.Id, result.Response.User.Id);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        await RegisterAsync();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => RegisterAsync("MARIA@exemplo.com"));
        Assert.Equal(ErrorCodes.EmailAlreadyRegistered, ex.Code);
    }

    [Fact]
    public async Task Login_WrongPasswordOrUnknownEmail_ThrowsSameGenericError()
    {
        await RegisterAsync();

        var wrong = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginRequest { Email = "maria@exemplo.com", Password = "Errada123" }, null, CancellationToken.None));
        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginRequest { Email = "ninguem@exemplo.com", Password = "Senha123" }, null, CancellationToken.None));

        Assert.Equal(ErrorCodes.InvalidCredentials, wrong.Code);
        Assert.Equal(wrong.Code, unknown.Code);
        Assert.Equal(wrong.Title, unknown.Title);
    }

    [Fact]
    public async Task Refresh_RotatesWithinSameFamily()
    {
        var first = await RegisterAsync();

        var second = await auth.RefreshAsync(first.RefreshToken, null, CancellationToken.None);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        var old = await db.RefreshTokens.SingleAsync(t => t.TokenHash == tokens.Hash(first.RefreshToken));
        var current = await db.RefreshTokens.SingleAsync(t => t.TokenHash == tokens.Hash(second.RefreshToken));
        Assert.Equal(RefreshTokenRevocationReason.Rotated, old.RevocationReason);
        Assert.Equal(current.Id, old.ReplacedByTokenId);
        Assert.Equal(old.FamilyId, current.FamilyId);
        Assert.True(current.IsActive(clock.UtcNow));
    }

    [Fact]
    public async Task Refresh_ReuseOfRotatedToken_RevokesWholeFamily()
    {
        var first = await RegisterAsync();
        var second = await auth.RefreshAsync(first.RefreshToken, null, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync(first.RefreshToken, null, CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidRefreshToken, ex.Code);

        var current = await db.RefreshTokens.SingleAsync(t => t.TokenHash == tokens.Hash(second.RefreshToken));
        Assert.Equal(RefreshTokenRevocationReason.ReuseDetected, current.RevocationReason);
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync(second.RefreshToken, null, CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_ReuseDoesNotAffectOtherSessions()
    {
        var sessionA = await RegisterAsync();
        var sessionB = await auth.LoginAsync(new LoginRequest { Email = "maria@exemplo.com", Password = "Senha123" }, null, CancellationToken.None);
        await auth.RefreshAsync(sessionA.RefreshToken, null, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync(sessionA.RefreshToken, null, CancellationToken.None));

        var stillValid = await auth.RefreshAsync(sessionB.RefreshToken, null, CancellationToken.None);
        Assert.NotNull(stillValid.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ExpiredOrUnknown_Throws()
    {
        var first = await RegisterAsync();
        clock.Advance(TimeSpan.FromDays(7));

        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync(first.RefreshToken, null, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync("desconhecido", null, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync(null, null, CancellationToken.None));
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_SendsNothing()
    {
        await auth.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "ninguem@exemplo.com" }, CancellationToken.None);
        Assert.Empty(email.Sent);
        Assert.Empty(db.PasswordResetTokens);
    }

    [Fact]
    public async Task ForgotPassword_SmtpFailure_IsNotPropagated()
    {
        await RegisterAsync();
        email.Fail = true;
        await auth.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "maria@exemplo.com" }, CancellationToken.None);
        Assert.Single(db.PasswordResetTokens);
    }

    [Fact]
    public async Task ForgotAndReset_ChangesPasswordRevokesSessionsAndTokenIsSingleUse()
    {
        var session = await RegisterAsync();
        await auth.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "maria@exemplo.com" }, CancellationToken.None);
        var token = ExtractToken(Assert.Single(email.Sent).TextBody);

        var stored = await db.PasswordResetTokens.SingleAsync();
        Assert.Equal(tokens.Hash(token), stored.TokenHash);
        Assert.Equal(clock.UtcNow.AddHours(1), stored.ExpiresAt);

        await auth.ResetPasswordAsync(new ResetPasswordRequest { Token = token, NewPassword = "NovaSenha123" }, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync(session.RefreshToken, null, CancellationToken.None));
        await auth.LoginAsync(new LoginRequest { Email = "maria@exemplo.com", Password = "NovaSenha123" }, null, CancellationToken.None);
        var reuse = await Assert.ThrowsAsync<BadRequestException>(() =>
            auth.ResetPasswordAsync(new ResetPasswordRequest { Token = token, NewPassword = "OutraSenha123" }, CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidToken, reuse.Code);
    }

    [Fact]
    public async Task ForgotPassword_InvalidatesPreviousUnusedTokens()
    {
        await RegisterAsync();
        await auth.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "maria@exemplo.com" }, CancellationToken.None);
        await auth.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "maria@exemplo.com" }, CancellationToken.None);
        var firstToken = ExtractToken(email.Sent[0].TextBody);
        var secondToken = ExtractToken(email.Sent[1].TextBody);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            auth.ResetPasswordAsync(new ResetPasswordRequest { Token = firstToken, NewPassword = "NovaSenha123" }, CancellationToken.None));
        await auth.ResetPasswordAsync(new ResetPasswordRequest { Token = secondToken, NewPassword = "NovaSenha123" }, CancellationToken.None);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_Throws()
    {
        await RegisterAsync();
        await auth.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "maria@exemplo.com" }, CancellationToken.None);
        var token = ExtractToken(email.Sent[0].TextBody);
        clock.Advance(TimeSpan.FromMinutes(61));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            auth.ResetPasswordAsync(new ResetPasswordRequest { Token = token, NewPassword = "NovaSenha123" }, CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidToken, ex.Code);
    }

    [Fact]
    public async Task ResetEmail_ContainsEncodedFrontendLink()
    {
        await RegisterAsync();
        await auth.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "maria@exemplo.com" }, CancellationToken.None);
        var message = Assert.Single(email.Sent);

        Assert.Equal("maria@exemplo.com", message.ToAddress);
        Assert.Contains("http://front.test/reset-password?token=", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("Redefinir senha", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangePassword_KeepsCurrentSessionOnly()
    {
        var sessionA = await RegisterAsync();
        var sessionB = await auth.LoginAsync(new LoginRequest { Email = "maria@exemplo.com", Password = "Senha123" }, null, CancellationToken.None);
        var tokenA = await db.RefreshTokens.SingleAsync(t => t.TokenHash == tokens.Hash(sessionA.RefreshToken));
        currentUser.UserId = sessionA.Response.User.Id;
        currentUser.SessionId = tokenA.FamilyId;

        await users.ChangePasswordAsync(new ChangePasswordRequest { CurrentPassword = "Senha123", NewPassword = "NovaSenha123" }, CancellationToken.None);

        await auth.RefreshAsync(sessionA.RefreshToken, null, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedException>(() => auth.RefreshAsync(sessionB.RefreshToken, null, CancellationToken.None));
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_ThrowsInvalidPassword()
    {
        var session = await RegisterAsync();
        currentUser.UserId = session.Response.User.Id;

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            users.ChangePasswordAsync(new ChangePasswordRequest { CurrentPassword = "Errada123", NewPassword = "NovaSenha123" }, CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidPassword, ex.Code);
    }

    [Fact]
    public async Task DeleteAccount_AnonymizesRevokesAndBlocksLogin()
    {
        var session = await RegisterAsync();
        currentUser.UserId = session.Response.User.Id;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            users.DeleteAccountAsync(new DeleteAccountRequest { Password = "Errada123" }, CancellationToken.None));
        await users.DeleteAccountAsync(new DeleteAccountRequest { Password = "Senha123" }, CancellationToken.None);

        var user = await db.Users.SingleAsync();
        Assert.False(user.IsActive);
        Assert.NotEqual("maria@exemplo.com", user.Email);
        Assert.All(db.RefreshTokens, t => Assert.Equal(RefreshTokenRevocationReason.AccountDeleted, t.RevocationReason));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginRequest { Email = "maria@exemplo.com", Password = "Senha123" }, null, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() => users.GetCurrentAsync(CancellationToken.None));

        // O e-mail fica livre para um novo cadastro.
        await RegisterAsync();
    }

    private static string ExtractToken(string body)
    {
        var match = System.Text.RegularExpressions.Regex.Match(body, @"token=([A-Za-z0-9_\-%]+)");
        Assert.True(match.Success);
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }
}
