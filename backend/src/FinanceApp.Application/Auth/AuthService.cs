using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Emails;
using FinanceApp.Application.Notifications;
using FinanceApp.Application.Users;
using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceApp.Application.Auth;

/// <summary>Casos de uso de autenticação: cadastro, login, refresh, logout e redefinição de senha.</summary>
public sealed partial class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    ISecureTokenService secureTokens,
    IEmailSender emailSender,
    PasswordResetEmailFactory resetEmailFactory,
    IDateTimeProvider clock,
    IOptions<JwtOptions> jwtOptions,
    IOptions<AppOptions> appOptions,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator,
    ILogger<AuthService> logger)
{
    public const int PasswordResetTokenBytes = 32;
    private const string UsersEmailIndex = "ix_users_email";

    // Campo exigido pelo gerador de LoggerMessage (não reconhece parâmetros do construtor primário).
    private readonly ILogger<AuthService> logger = logger;

    // Hash usado para igualar o tempo de resposta quando o e-mail não existe (evita enumeração).
    private static string? dummyPasswordHash;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await registerValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken).ConfigureAwait(false))
        {
            throw EmailAlreadyRegistered();
        }

        var now = clock.UtcNow;
        var user = User.Create(request.Name, email, passwordHasher.Hash(request.Password), now);
        db.Users.Add(user);
        // D-043d: convites pendentes (não expirados) para este e-mail viram notificações, na mesma transação do cadastro.
        await NotificationRecorder.AddForNewUserAsync(db, user, now, cancellationToken).ConfigureAwait(false);
        var result = IssueSession(user, Guid.NewGuid(), ipAddress, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == UsersEmailIndex)
        {
            throw EmailAlreadyRegistered();
        }

        LogUserRegistered(user.Id);
        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await loginValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken).ConfigureAwait(false);

        if (user is null || !user.IsActive)
        {
            dummyPasswordHash ??= passwordHasher.Hash("timing-equalization-only-1");
            _ = passwordHasher.Verify(dummyPasswordHash, request.Password);
            throw InvalidCredentials();
        }

        var verification = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (verification == PasswordVerificationOutcome.Failed)
        {
            LogLoginFailed(user.Id);
            throw InvalidCredentials();
        }

        var now = clock.UtcNow;
        if (verification == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(request.Password), now);
        }

        var result = IssueSession(user, Guid.NewGuid(), ipAddress, now);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Rotaciona o refresh token. Reutilizar um token já rotacionado revoga toda a família (D-005).
    /// </summary>
    public async Task<AuthResult> RefreshAsync(string? refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw InvalidRefreshToken();
        }

        var hash = secureTokens.Hash(refreshToken);
        var current = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            throw InvalidRefreshToken();
        }

        var now = clock.UtcNow;
        if (current.IsRevoked)
        {
            if (current.WasRotated)
            {
                var revoked = await RevokeFamilyAsync(current.FamilyId, now, cancellationToken).ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                LogRefreshReuseDetected(current.UserId, current.FamilyId, revoked);
            }

            throw InvalidRefreshToken();
        }

        if (current.IsExpired(now))
        {
            throw InvalidRefreshToken();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            throw InvalidRefreshToken();
        }

        var result = IssueSession(user, current.FamilyId, ipAddress, now, out var replacement);
        current.Revoke(now, RefreshTokenRevocationReason.Rotated, replacement.Id);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            // Outra requisição rotacionou o mesmo token simultaneamente.
            throw InvalidRefreshToken();
        }

        return result;
    }

    /// <summary>Revoga o refresh token informado, se ativo. Nunca falha.</summary>
    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = secureTokens.Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        if (token is null || !token.IsActive(now))
        {
            return;
        }

        token.Revoke(now, RefreshTokenRevocationReason.Logout);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            // Token alterado concorrentemente (rotação/revogação): nada a fazer no logout.
        }
    }

    /// <summary>Sempre conclui sem revelar se o e-mail existe.</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await forgotPasswordValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        var now = clock.UtcNow;
        await db.RemoveUnusedPasswordResetTokensAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var rawToken = secureTokens.GenerateToken(PasswordResetTokenBytes);
        var expiresAt = now.AddMinutes(appOptions.Value.PasswordResetTokenMinutes);
        db.PasswordResetTokens.Add(PasswordResetToken.Create(user.Id, secureTokens.Hash(rawToken), expiresAt, now));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await emailSender.SendAsync(resetEmailFactory.Create(user.Email, user.Name, rawToken), cancellationToken).ConfigureAwait(false);
            LogResetEmailSent(user.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Não propaga: a resposta não pode revelar a existência do e-mail.
            LogResetEmailFailed(ex, user.Id);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await resetPasswordValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        var hash = secureTokens.Hash(request.Token.Trim());
        var token = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken).ConfigureAwait(false);
        if (token is null || !token.IsValid(now))
        {
            throw InvalidResetToken();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            throw InvalidResetToken();
        }

        token.MarkAsUsed(now);
        user.ChangePasswordHash(passwordHasher.Hash(request.NewPassword), now);
        await db.RevokeActiveRefreshTokensAsync(user.Id, now, RefreshTokenRevocationReason.PasswordReset, cancellationToken).ConfigureAwait(false);
        await db.RemoveUnusedPasswordResetTokensAsync(user.Id, cancellationToken, exceptTokenId: token.Id).ConfigureAwait(false);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            // Uso simultâneo do mesmo token: apenas uma requisição vence.
            throw InvalidResetToken();
        }

        LogPasswordReset(user.Id);
    }

    private AuthResult IssueSession(User user, Guid familyId, string? ipAddress, DateTimeOffset now) =>
        IssueSession(user, familyId, ipAddress, now, out _);

    private AuthResult IssueSession(User user, Guid familyId, string? ipAddress, DateTimeOffset now, out RefreshToken entity)
    {
        var rawToken = secureTokens.GenerateToken();
        var expiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays);
        entity = RefreshToken.Create(user.Id, secureTokens.Hash(rawToken), familyId, expiresAt, now, ipAddress);
        db.RefreshTokens.Add(entity);

        var access = jwtTokenGenerator.Generate(user, familyId);
        return new AuthResult(new AuthResponse(access.Token, access.ExpiresAt, UserDto.From(user)), rawToken, entity.ExpiresAt);
    }

    private async Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var token in tokens)
        {
            token.Revoke(now, RefreshTokenRevocationReason.ReuseDetected);
        }

        return tokens.Count;
    }

    private static ConflictException EmailAlreadyRegistered() =>
        new(ErrorCodes.EmailAlreadyRegistered, "E-mail já cadastrado.");

    private static UnauthorizedException InvalidCredentials() =>
        new(ErrorCodes.InvalidCredentials, "E-mail ou senha inválidos.");

    private static UnauthorizedException InvalidRefreshToken() =>
        new(ErrorCodes.InvalidRefreshToken, "Sessão inválida ou expirada. Faça login novamente.");

    private static BadRequestException InvalidResetToken() =>
        new(ErrorCodes.InvalidToken, "Token de redefinição inválido ou expirado.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} cadastrado.")]
    private partial void LogUserRegistered(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Falha de login para o usuário {UserId}: senha incorreta.")]
    private partial void LogLoginFailed(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reutilização de refresh token detectada para o usuário {UserId}; família {FamilyId} revogada ({RevokedCount} tokens ativos).")]
    private partial void LogRefreshReuseDetected(Guid userId, Guid familyId, int revokedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "E-mail de redefinição de senha enviado ao usuário {UserId}.")]
    private partial void LogResetEmailSent(Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao enviar e-mail de redefinição de senha ao usuário {UserId}.")]
    private partial void LogResetEmailFailed(Exception exception, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Senha redefinida para o usuário {UserId}; refresh tokens revogados.")]
    private partial void LogPasswordReset(Guid userId);
}
