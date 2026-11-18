using FinanceApp.Domain.Auth;
using FinanceApp.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Common.Abstractions;

/// <summary>
/// Unidade de trabalho da aplicação. Expõe os conjuntos de entidades; repositórios
/// só serão introduzidos quando houver ganho real (ver ARCHITECTURE.md).
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    /// <exception cref="Exceptions.UniqueConstraintViolationException">Violação de índice único.</exception>
    /// <exception cref="Exceptions.ConcurrencyConflictException">Conflito de concorrência otimista.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
