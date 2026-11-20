using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Groups;

/// <summary>
/// Convite por e-mail (D-031). Guarda apenas o hash SHA-256 do token — nunca o token. A expiração é derivada na leitura
/// (<see cref="GetEffectiveStatus"/>) e só persistida (<see cref="MarkExpiredIfDue"/>) quando constatada.
/// </summary>
public sealed class GroupInvitation
{
    public const int ValidityDays = 7;
    public const int TokenHashMaxLength = 128;

    private GroupInvitation()
    {
        Email = string.Empty;
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    /// <summary>E-mail do convidado, normalizado (minúsculas, sem espaços nas extremidades).</summary>
    public string Email { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Estado persistido (<c>Pending</c>, <c>Accepted</c>, <c>Declined</c>, <c>Revoked</c> ou <c>Expired</c> constatado).</summary>
    public GroupInvitationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Quando foi aceito/recusado/revogado/expirado.</summary>
    public DateTimeOffset? RespondedAt { get; private set; }

    public static GroupInvitation Create(Guid groupId, Guid invitedByUserId, string email, string tokenHash, DateTimeOffset now)
    {
        var utcNow = now.ToUniversalTime();
        return new GroupInvitation
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            InvitedByUserId = invitedByUserId,
            Email = Users.User.NormalizeEmail(email),
            TokenHash = NormalizeHash(tokenHash),
            ExpiresAt = utcNow.AddDays(ValidityDays),
            Status = GroupInvitationStatus.Pending,
            CreatedAt = utcNow,
        };
    }

    public GroupInvitationStatus GetEffectiveStatus(DateTimeOffset now) =>
        Status == GroupInvitationStatus.Pending && ExpiresAt <= now ? GroupInvitationStatus.Expired : Status;

    public bool IsPendingAndValid(DateTimeOffset now) => GetEffectiveStatus(now) == GroupInvitationStatus.Pending;

    public bool EmailMatches(string userEmail) =>
        string.Equals(Email, Users.User.NormalizeEmail(userEmail), StringComparison.Ordinal);

    /// <summary>Persiste a expiração de um convite pendente já vencido (libera novo convite para o mesmo e-mail).</summary>
    public bool MarkExpiredIfDue(DateTimeOffset now)
    {
        if (Status != GroupInvitationStatus.Pending || ExpiresAt > now)
        {
            return false;
        }

        Status = GroupInvitationStatus.Expired;
        RespondedAt = now.ToUniversalTime();
        return true;
    }

    /// <summary>Reenvio: novo token e nova validade; o token anterior deixa de valer. Só convites <c>Pending</c> (inclusive vencidos).</summary>
    public void Renew(string newTokenHash, DateTimeOffset now)
    {
        if (Status != GroupInvitationStatus.Pending && Status != GroupInvitationStatus.Expired)
        {
            throw new DomainException("Somente convites pendentes ou expirados podem ser reenviados.");
        }

        TokenHash = NormalizeHash(newTokenHash);
        ExpiresAt = now.ToUniversalTime().AddDays(ValidityDays);
        Status = GroupInvitationStatus.Pending;
        RespondedAt = null;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (!IsPendingAndValid(now))
        {
            throw new DomainException("Somente convites pendentes podem ser revogados.");
        }

        Status = GroupInvitationStatus.Revoked;
        RespondedAt = now.ToUniversalTime();
    }

    /// <summary>Revogação silenciosa de convite pendente (exclusão de conta de quem convidou, D-036).</summary>
    public void RevokeIfPending(DateTimeOffset now)
    {
        if (Status == GroupInvitationStatus.Pending)
        {
            Status = GroupInvitationStatus.Revoked;
            RespondedAt = now.ToUniversalTime();
        }
    }

    /// <summary>Aceita o convite em nome do usuário com o e-mail informado (e-mail diferente → 403; convite inválido → 422).</summary>
    public void Accept(string userEmail, DateTimeOffset now)
    {
        EnsureCanRespond(userEmail, now);
        Status = GroupInvitationStatus.Accepted;
        RespondedAt = now.ToUniversalTime();
    }

    public void Decline(string userEmail, DateTimeOffset now)
    {
        EnsureCanRespond(userEmail, now);
        Status = GroupInvitationStatus.Declined;
        RespondedAt = now.ToUniversalTime();
    }

    /// <summary>Verifica (sem alterar o estado) se o usuário com este e-mail pode responder ao convite agora.</summary>
    public void EnsureCanRespond(string userEmail, DateTimeOffset now)
    {
        if (!EmailMatches(userEmail))
        {
            throw new PermissionDeniedException("Este convite foi enviado para outro e-mail. Entre com a conta convidada.");
        }

        switch (GetEffectiveStatus(now))
        {
            case GroupInvitationStatus.Pending:
                return;
            case GroupInvitationStatus.Expired:
                throw new DomainException("Este convite expirou. Peça um novo convite a um administrador do grupo.");
            case GroupInvitationStatus.Revoked:
                throw new DomainException("Este convite foi revogado.");
            default:
                throw new DomainException("Este convite já foi utilizado.");
        }
    }

    private static string NormalizeHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length > TokenHashMaxLength)
        {
            throw new DomainException("O hash do token do convite é inválido.");
        }

        return hash;
    }
}
