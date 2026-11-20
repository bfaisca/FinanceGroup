using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Transactions;

namespace FinanceApp.Domain.Recurrences;

/// <summary>
/// Regra de recorrência (D-027). Funciona como molde: cada ocorrência gerada é uma <see cref="Transaction"/> independente.
/// <c>type</c> e <c>startDate</c> são imutáveis. Regras não são excluídas, apenas desativadas.
/// </summary>
public sealed class RecurrenceRule
{
    public const int DescriptionMaxLength = Transaction.DescriptionMaxLength;

    private RecurrenceRule()
    {
        Description = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Description { get; private set; }

    public TransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid? AccountId { get; private set; }

    public Guid? CreditCardId { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public string? Notes { get; private set; }

    public RecurrenceFrequency Frequency { get; private set; }

    public int Interval { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Category? Category { get; private set; }

    public Account? Account { get; private set; }

    public CreditCard? CreditCard { get; private set; }

    public static RecurrenceRule Create(
        Guid userId,
        string description,
        TransactionType type,
        decimal amount,
        Category category,
        Account? account,
        CreditCard? creditCard,
        PaymentMethod? paymentMethod,
        string? notes,
        RecurrenceFrequency frequency,
        int interval,
        DateOnly startDate,
        DateOnly? endDate,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);
        Guard.NotEmpty(userId, "O usuário é obrigatório.");
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Tipo de movimentação inválido.");
        }

        var utcNow = Guard.Utc(now);
        var rule = new RecurrenceRule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            StartDate = startDate,
            IsActive = true,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
        rule.Apply(description, amount, category, account, creditCard, paymentMethod, notes, frequency, interval, endDate, validateRefsActive: true);
        return rule;
    }

    /// <summary>Atualiza os dados editáveis (<c>type</c> e <c>startDate</c> são imutáveis).</summary>
    public void Update(
        string description,
        decimal amount,
        Category category,
        Account? account,
        CreditCard? creditCard,
        PaymentMethod? paymentMethod,
        string? notes,
        RecurrenceFrequency frequency,
        int interval,
        DateOnly? endDate,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);
        // A regeneração exige referências ativas; só validamos quando a regra vai continuar gerando.
        Apply(description, amount, category, account, creditCard, paymentMethod, notes, frequency, interval, endDate, validateRefsActive: IsActive);
        UpdatedAt = Guard.Utc(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = Guard.Utc(now);
    }

    /// <summary>Reativa a regra; exige categoria, conta e cartão ativos (a geração depende deles).</summary>
    public void Activate(Category category, Account? account, CreditCard? creditCard, DateTimeOffset now)
    {
        EnsureReferences(category, account, creditCard);
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = Guard.Utc(now);
    }

    /// <summary>Datas do cronograma até <paramref name="upTo"/> (inclusive), respeitando <see cref="EndDate"/>.</summary>
    public IEnumerable<DateOnly> GetOccurrenceDates(DateOnly upTo) =>
        RecurrenceSchedule.Occurrences(Frequency, Interval, StartDate, EndDate, upTo);

    /// <summary>Próxima ocorrência em aberto (≥ hoje); <c>null</c> se a regra está inativa ou encerrada.</summary>
    public DateOnly? GetNextOccurrence(DateOnly today, IReadOnlySet<DateOnly> closedDates) =>
        IsActive ? RecurrenceSchedule.NextOpenOccurrence(Frequency, Interval, StartDate, EndDate, today, closedDates) : null;

    /// <summary>Cria uma ocorrência (<c>Planned</c> se a data &gt; hoje, senão <c>Pending</c>), conforme D-027/D-025.</summary>
    public Transaction CreateOccurrence(
        DateOnly occurrenceDate,
        DateOnly today,
        Category category,
        Account? account,
        CreditCard? creditCard,
        DateTimeOffset now) =>
        Transaction.Create(
            UserId,
            Description,
            Type,
            Amount,
            category,
            account,
            creditCard is not null ? Transactions.PaymentMethod.CreditCard : PaymentMethod,
            occurrenceDate > today ? TransactionStatus.Planned : TransactionStatus.Pending,
            occurrenceDate,
            creditCard is null ? occurrenceDate : null,
            Notes,
            now,
            creditCard,
            TransactionOrigin.ForOccurrence(Id, occurrenceDate));

    private void Apply(
        string description,
        decimal amount,
        Category category,
        Account? account,
        CreditCard? creditCard,
        PaymentMethod? paymentMethod,
        string? notes,
        RecurrenceFrequency frequency,
        int interval,
        DateOnly? endDate,
        bool validateRefsActive)
    {
        var normalizedDescription = Guard.NotBlank(description, "A descrição é obrigatória.").Trim();
        if (normalizedDescription.Length > DescriptionMaxLength)
        {
            throw new DomainException($"A descrição deve ter no máximo {DescriptionMaxLength} caracteres.");
        }

        if (amount <= 0 || !MoneyRules.HasValidScale(amount) || !MoneyRules.IsWithinRange(amount))
        {
            throw new DomainException("O valor é inválido (maior que zero, no máximo 2 casas decimais).");
        }

        if (!Enum.IsDefined(frequency))
        {
            throw new DomainException("Frequência inválida.");
        }

        if (interval is < RecurrenceSchedule.MinInterval or > RecurrenceSchedule.MaxInterval)
        {
            throw new DomainException($"O intervalo deve estar entre {RecurrenceSchedule.MinInterval} e {RecurrenceSchedule.MaxInterval}.");
        }

        if (endDate is { } end && end < StartDate)
        {
            throw new DomainException("A data final não pode ser anterior à data inicial.");
        }

        if (!category.Accepts(Type))
        {
            throw new DomainException("A categoria deve ser do mesmo tipo da recorrência.");
        }

        if (creditCard is not null)
        {
            if (Type != TransactionType.Expense)
            {
                throw new DomainException("Somente despesas podem ser lançadas no cartão de crédito.");
            }

            if (account is not null)
            {
                throw new DomainException("Compras no cartão não aceitam conta: a conta é a associada ao cartão.");
            }

            if (paymentMethod is not null and not Transactions.PaymentMethod.CreditCard)
            {
                throw new DomainException("A forma de pagamento de compras no cartão deve ser cartão de crédito.");
            }
        }

        if (paymentMethod is { } method && !Enum.IsDefined(method))
        {
            throw new DomainException("Forma de pagamento inválida.");
        }

        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalizedNotes is { Length: > Transaction.NotesMaxLength })
        {
            throw new DomainException($"As observações devem ter no máximo {Transaction.NotesMaxLength} caracteres.");
        }

        if (validateRefsActive)
        {
            EnsureReferences(category, account, creditCard);
        }

        Description = normalizedDescription;
        Amount = amount;
        CategoryId = category.Id;
        Category = category;
        AccountId = account?.Id;
        Account = account;
        CreditCardId = creditCard?.Id;
        CreditCard = creditCard;
        PaymentMethod = creditCard is not null ? Transactions.PaymentMethod.CreditCard : paymentMethod;
        Notes = normalizedNotes;
        Frequency = frequency;
        Interval = interval;
        EndDate = endDate;
    }

    private static void EnsureReferences(Category category, Account? account, CreditCard? creditCard)
    {
        if (!category.IsActive)
        {
            throw new DomainException("A categoria selecionada está inativa.");
        }

        account?.EnsureActive("A conta selecionada está desativada.");
        creditCard?.EnsureActive("O cartão selecionado está desativado.");
    }
}
