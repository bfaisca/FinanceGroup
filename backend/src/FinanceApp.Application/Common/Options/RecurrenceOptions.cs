namespace FinanceApp.Application.Common.Options;

/// <summary>Seção <c>Recurrence</c> (variáveis <c>Recurrence__*</c>, D-010/D-027).</summary>
public sealed class RecurrenceOptions
{
    public const string SectionName = "Recurrence";

    public const int DefaultHorizonMonths = 12;
    public const int DefaultGenerationIntervalMinutes = 360;

    /// <summary>Horizonte de geração de ocorrências, em meses a partir de "hoje".</summary>
    public int HorizonMonths { get; set; } = DefaultHorizonMonths;

    /// <summary>Intervalo entre execuções do serviço em segundo plano.</summary>
    public int GenerationIntervalMinutes { get; set; } = DefaultGenerationIntervalMinutes;

    /// <summary>Liga/desliga o serviço em segundo plano (desligado nos testes).</summary>
    public bool BackgroundEnabled { get; set; } = true;

    public IEnumerable<string> Validate()
    {
        if (HorizonMonths is < 1 or > 36)
        {
            yield return "Recurrence:HorizonMonths deve estar entre 1 e 36.";
        }

        if (GenerationIntervalMinutes is < 1 or > 10080)
        {
            yield return "Recurrence:GenerationIntervalMinutes deve estar entre 1 e 10080.";
        }
    }
}
