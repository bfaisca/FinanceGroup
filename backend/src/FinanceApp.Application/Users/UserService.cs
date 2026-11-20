using FinanceApp.Application.Auth;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceApp.Application.Users;

/// <summary>Casos de uso do usuário autenticado (perfil, senha e exclusão de conta).</summary>
public sealed partial class UserService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock,
    IValidator<UpdateProfileRequest> updateProfileValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    IValidator<DeleteAccountRequest> deleteAccountValidator,
    ILogger<UserService> logger)
{
    // Campo exigido pelo gerador de LoggerMessage (não reconhece parâmetros do construtor primário).
    private readonly ILogger<UserService> logger = logger;

    public async Task<UserDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var user = await GetActiveCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        return UserDto.From(user);
    }

    public async Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        await updateProfileValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var user = await GetActiveCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        user.Rename(request.Name, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return UserDto.From(user);
    }

    /// <summary>
    /// Troca a senha e revoga todos os refresh tokens do usuário, exceto os da sessão atual
    /// (família indicada pelo claim <c>sid</c> do access token).
    /// </summary>
    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await changePasswordValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var user = await GetActiveCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordVerificationOutcome.Failed)
        {
            throw new BadRequestException(ErrorCodes.InvalidPassword, "Senha atual incorreta.");
        }

        var now = clock.UtcNow;
        user.ChangePasswordHash(passwordHasher.Hash(request.NewPassword), now);
        var revoked = await db.RevokeActiveRefreshTokensAsync(
                user.Id, now, RefreshTokenRevocationReason.PasswordChanged, cancellationToken, exceptFamilyId: currentUser.SessionId)
            .ConfigureAwait(false);
        await db.RemoveUnusedPasswordResetTokensAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogPasswordChanged(user.Id, revoked);
    }

    /// <summary>
    /// Exclui a conta (D-014): apaga definitivamente os dados individuais e, na MESMA transação
    /// (um único <c>SaveChanges</c>), anonimiza e desativa o usuário e revoga todos os tokens.
    /// </summary>
    public async Task DeleteAccountAsync(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        await deleteAccountValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var user = await GetActiveCurrentUserAsync(cancellationToken).ConfigureAwait(false);
        if (passwordHasher.Verify(user.PasswordHash, request.Password) == PasswordVerificationOutcome.Failed)
        {
            throw new BadRequestException(ErrorCodes.InvalidPassword, "Senha incorreta.");
        }

        var now = clock.UtcNow;
        await RemoveIndividualFinancialDataAsync(user.Id, cancellationToken).ConfigureAwait(false);
        user.Anonymize(now);
        await db.RevokeActiveRefreshTokensAsync(user.Id, now, RefreshTokenRevocationReason.AccountDeleted, cancellationToken).ConfigureAwait(false);
        var resetTokens = await db.PasswordResetTokens.Where(t => t.UserId == user.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.PasswordResetTokens.RemoveRange(resetTokens);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogAccountDeleted(user.Id);
    }

    /// <summary>
    /// Marca para exclusão todos os dados individuais do usuário (persistidos junto com a anonimização).
    /// Pagamentos e histórico das movimentações e os saldos das contas saem por cascata no banco.
    /// A Etapa 3 estendeu esta rotina com cartões, parcelamentos e recorrências (D-029); a Etapa 4 deve tratar grupos: movimentações de grupo permanecem
    /// (autor exibido como "Usuário removido", conta/cartão desvinculados).
    /// </summary>
    private async Task RemoveIndividualFinancialDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var transactions = await db.Transactions.Where(t => t.CreatedByUserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.Transactions.RemoveRange(transactions);

        // Etapa 3 (D-029): parcelamentos, recorrências e cartões também são dados individuais. As movimentações geradas por eles
        // já estão incluídas acima; tudo é removido na mesma transação (o EF ordena as exclusões pelas chaves estrangeiras).
        var plans = await db.InstallmentPlans.Where(p => p.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.InstallmentPlans.RemoveRange(plans);

        var rules = await db.RecurrenceRules.Where(r => r.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.RecurrenceRules.RemoveRange(rules);

        var cards = await db.CreditCards.Where(c => c.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.CreditCards.RemoveRange(cards);

        var accounts = await db.Accounts.Where(a => a.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.Accounts.RemoveRange(accounts);

        var categories = await db.Categories.Where(c => c.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.Categories.RemoveRange(categories);
    }

    /// <summary>Usuário inexistente ou inativo com JWT ainda válido é tratado como não autenticado.</summary>
    private async Task<User> GetActiveCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        return user is { IsActive: true } ? user : throw UnauthorizedException.NotAuthenticated();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Senha alterada pelo usuário {UserId}; {RevokedCount} refresh tokens de outras sessões revogados.")]
    private partial void LogPasswordChanged(Guid userId, int revokedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Conta do usuário {UserId} anonimizada e desativada.")]
    private partial void LogAccountDeleted(Guid userId);
}
