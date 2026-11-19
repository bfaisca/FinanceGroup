using FinanceApp.Domain.Transactions;

namespace FinanceApp.Application.Accounts;

/// <summary>Saldo informado (snapshot) usado como referência do saldo projetado.</summary>
public sealed record SnapshotReference(decimal Balance, DateOnly ReferenceDate, DateTimeOffset CreatedAt);

/// <summary>Movimentação reduzida ao que importa para a projeção. <see cref="EffectiveDate"/> = <c>dueDate ?? transactionDate</c>.</summary>
public sealed record ProjectionMovement(TransactionType Type, decimal Amount, DateOnly EffectiveDate, bool IsCanceled);

/// <summary>
/// Cálculo puro do saldo projetado (D-019):
/// <c>projetado = saldo do snapshot de referência + Σ receitas − Σ despesas</c>, considerando movimentações
/// da conta, não canceladas, com data efetiva estritamente posterior à referência e menor ou igual a <c>asOf</c>.
/// Pagamentos parciais não alteram o valor (conta o valor total). Sem snapshot → indisponível.
/// </summary>
public static class ProjectedBalanceCalculator
{
    /// <summary>Snapshot de referência: maior <c>referenceDate</c>; em empate, o criado por último.</summary>
    public static SnapshotReference? SelectReference(IEnumerable<SnapshotReference> snapshots) =>
        snapshots
            .OrderByDescending(s => s.ReferenceDate)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefault();

    public static bool Counts(ProjectionMovement movement, DateOnly referenceDate, DateOnly asOf) =>
        !movement.IsCanceled
        && movement.EffectiveDate > referenceDate
        && movement.EffectiveDate <= asOf;

    /// <summary>Aritmética final, a partir dos totais já filtrados. <c>asOf ≤ referência</c> → saldo informado.</summary>
    public static decimal Project(decimal referenceBalance, DateOnly referenceDate, DateOnly asOf, decimal income, decimal expense) =>
        asOf <= referenceDate ? referenceBalance : referenceBalance + income - expense;

    public static decimal? Project(
        IEnumerable<SnapshotReference> snapshots,
        IEnumerable<ProjectionMovement> movements,
        DateOnly asOf)
    {
        var reference = SelectReference(snapshots);
        if (reference is null)
        {
            return null;
        }

        var counted = movements.Where(m => Counts(m, reference.ReferenceDate, asOf)).ToList();
        var income = counted.Where(m => m.Type == TransactionType.Income).Sum(m => m.Amount);
        var expense = counted.Where(m => m.Type == TransactionType.Expense).Sum(m => m.Amount);
        return Project(reference.Balance, reference.ReferenceDate, asOf, income, expense);
    }
}
