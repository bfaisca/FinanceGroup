namespace FinanceApp.Domain.Transactions;

/// <summary>Origem de uma movimentação gerada: parcela de um parcelamento ou ocorrência de uma recorrência (Etapa 3).</summary>
public sealed record TransactionOrigin
{
    private TransactionOrigin()
    {
    }

    public Guid? InstallmentPlanId { get; private init; }

    public int? InstallmentNumber { get; private init; }

    public Guid? RecurrenceRuleId { get; private init; }

    public DateOnly? OccurrenceDate { get; private init; }

    public static TransactionOrigin ForInstallment(Guid planId, int number) =>
        new() { InstallmentPlanId = planId, InstallmentNumber = number };

    public static TransactionOrigin ForOccurrence(Guid ruleId, DateOnly occurrenceDate) =>
        new() { RecurrenceRuleId = ruleId, OccurrenceDate = occurrenceDate };
}
