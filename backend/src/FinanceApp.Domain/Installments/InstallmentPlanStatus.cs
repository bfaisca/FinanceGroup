namespace FinanceApp.Domain.Installments;

public enum InstallmentPlanStatus
{
    Open,
    Completed,
    Canceled,
}

public static class InstallmentPlanStatusRules
{
    /// <summary>
    /// Estado derivado do plano (D-026): <c>Canceled</c> se todas as parcelas estão canceladas; <c>Completed</c> se não há
    /// parcela em aberto (restante &gt; 0 e não cancelada); senão <c>Open</c>.
    /// </summary>
    public static InstallmentPlanStatus Derive(int openCount, int paidCount, int canceledCount)
    {
        if (openCount > 0)
        {
            return InstallmentPlanStatus.Open;
        }

        return paidCount == 0 && canceledCount > 0 ? InstallmentPlanStatus.Canceled : InstallmentPlanStatus.Completed;
    }
}
