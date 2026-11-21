using FinanceApp.Application.Auth;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Groups;
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
        await LeaveGroupsAsync(user.Id, now, cancellationToken).ConfigureAwait(false);
        await RemoveIndividualFinancialDataAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await RemoveNotificationsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        user.Anonymize(now);
        await db.RevokeActiveRefreshTokensAsync(user.Id, now, RefreshTokenRevocationReason.AccountDeleted, cancellationToken).ConfigureAwait(false);
        var resetTokens = await db.PasswordResetTokens.Where(t => t.UserId == user.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.PasswordResetTokens.RemoveRange(resetTokens);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogAccountDeleted(user.Id);
    }

    /// <summary>
    /// D-036: as participações ativas passam a <c>Left</c> (com transferência da propriedade quando o usuário era Owner, ou
    /// arquivamento do grupo se não restar ninguém) e os convites pendentes enviados por ele são revogados. As movimentações e
    /// os pagamentos de grupo permanecem no histórico (autor "Usuário removido").
    /// </summary>
    private async Task LeaveGroupsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var groups = await db.Groups.AsTracking()
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.UserId == userId && m.Status == GroupMemberStatus.Active))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var group in groups)
        {
            group.DepartBecauseAccountDeleted(userId, now);
        }

        var invitations = await db.GroupInvitations.AsTracking()
            .Where(i => i.InvitedByUserId == userId && i.Status == GroupInvitationStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var invitation in invitations)
        {
            invitation.RevokeIfPending(now);
        }
    }

    /// <summary>
    /// D-044/D-014: as notificações RECEBIDAS pelo usuário são apagadas. As notificações de terceiros em que ele é o ator
    /// permanecem (são do histórico do grupo) e passam a exibir "Usuário removido", pois o nome é lido da conta anonimizada.
    /// </summary>
    private async Task RemoveNotificationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var notifications = await db.Notifications.Where(n => n.RecipientUserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.Notifications.RemoveRange(notifications);
    }

    /// <summary>
    /// Marca para exclusão todos os dados individuais do usuário (persistidos junto com a anonimização).
    /// Pagamentos e histórico das movimentações e os saldos das contas saem por cascata no banco.
    /// A Etapa 3 estendeu esta rotina com cartões, parcelamentos e recorrências (D-029). Etapa 4 (D-014/D-036): somente
    /// movimentações SEM grupo são apagadas; as de grupo permanecem (autor anonimizado), a conta dos pagamentos de grupo é
    /// desvinculada e as categorias personalizadas usadas por elas são preservadas (inativas) para não corromper o histórico do grupo.
    /// </summary>
    private async Task RemoveIndividualFinancialDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Pagamentos de grupo feitos com contas do usuário: a conta é privada e será apagada; o pagamento fica sem conta.
        var groupPayments = await (
                from p in db.TransactionPayments.AsTracking()
                join t in db.Transactions on p.TransactionId equals t.Id
                where p.CreatedByUserId == userId && p.AccountId != null && t.GroupId != null
                select p)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var payment in groupPayments)
        {
            payment.DetachAccount();
        }

        var transactions = await db.Transactions.Where(t => t.CreatedByUserId == userId && t.GroupId == null).ToListAsync(cancellationToken).ConfigureAwait(false);
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

        // Categorias personalizadas: apaga as que não classificam movimentações de grupo (e nem são pai de uma que classifica).
        var categories = await db.Categories.Where(c => c.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var usedByGroups = (await db.Transactions.AsNoTracking()
                .Where(t => t.CreatedByUserId == userId && t.GroupId != null)
                .Select(t => t.CategoryId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();
        var keep = categories.Where(c => usedByGroups.Contains(c.Id)).ToList();
        var keepIds = keep.Select(c => c.Id).Concat(keep.Where(c => c.ParentCategoryId is not null).Select(c => c.ParentCategoryId!.Value)).ToHashSet();
        foreach (var category in categories)
        {
            if (keepIds.Contains(category.Id))
            {
                category.Deactivate();
            }
            else
            {
                db.Categories.Remove(category);
            }
        }
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
