using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Telegram;

/// <summary>
/// Grupo escolhido no bot para a PRÓXIMA despesa (D-056). Uso único e curta duração: depois de lançada (ou expirada),
/// o bot volta ao lançamento individual, para que ninguém publique no grupo sem querer.
/// </summary>
public sealed class TelegramGroupSelection
{
    private TelegramGroupSelection()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid GroupId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TelegramGroupSelection Create(Guid userId, Guid groupId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guard.NotEmpty(userId, "O usuário é obrigatório."),
        GroupId = Guard.NotEmpty(groupId, "O grupo é obrigatório."),
        CreatedAt = Guard.Utc(now),
    };
}
