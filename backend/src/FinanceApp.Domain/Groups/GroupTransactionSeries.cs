using FinanceApp.Domain.Common;
using FinanceApp.Domain.Recurrences;

namespace FinanceApp.Domain.Groups;

/// <summary>
/// Série de uma despesa recorrente de grupo (D-049): as N ocorrências são geradas de uma vez, cada uma uma
/// <c>Transaction</c> independente. A série guarda só a configuração original (frequência e total de ocorrências).
/// </summary>
public sealed class GroupTransactionSeries
{
    public const int MinOccurrences = 2;
    public const int MaxOccurrences = 60;

    private GroupTransactionSeries()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }

    /// <summary>Total original de ocorrências (não diminui quando alguma é excluída).</summary>
    public int Occurrences { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Frequências aceitas em grupos: sem diária nem personalizada.</summary>
    public static bool IsSupported(RecurrenceFrequency frequency) =>
        frequency is RecurrenceFrequency.Weekly or RecurrenceFrequency.Biweekly or RecurrenceFrequency.Monthly
            or RecurrenceFrequency.Bimonthly or RecurrenceFrequency.Quarterly or RecurrenceFrequency.Semiannual
            or RecurrenceFrequency.Annual;

    public static GroupTransactionSeries Create(Guid groupId, Guid createdByUserId, RecurrenceFrequency frequency, int occurrences, DateTimeOffset now)
    {
        Guard.NotEmpty(groupId, "O grupo é obrigatório.");
        Guard.NotEmpty(createdByUserId, "O usuário é obrigatório.");
        if (!IsSupported(frequency))
        {
            throw new DomainException("Frequência não suportada em grupos. Use semanal, quinzenal, mensal, bimestral, trimestral, semestral ou anual.");
        }

        if (occurrences is < MinOccurrences or > MaxOccurrences)
        {
            throw new DomainException($"O número de ocorrências deve estar entre {MinOccurrences} e {MaxOccurrences}.");
        }

        return new GroupTransactionSeries
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            CreatedByUserId = createdByUserId,
            Frequency = frequency,
            Occurrences = occurrences,
            CreatedAt = Guard.Utc(now),
        };
    }

    /// <summary>Data da ocorrência de índice <paramref name="index"/> (0 = primeira): <c>início + i·passo</c> sempre a partir do início (fim de mês, D-026).</summary>
    public DateOnly DateOf(DateOnly start, int index) =>
        RecurrenceSchedule.Nth(Frequency, 1, start, index)
        ?? throw new DomainException("As datas da recorrência excedem o intervalo permitido.");
}
