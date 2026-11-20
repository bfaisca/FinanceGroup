using System.Globalization;
using FinanceApp.Domain.Accounts;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Installments;

namespace FinanceApp.Domain.Transactions;

/// <summary>
/// Receita ou despesa individual. Concentra a máquina de estados descrita em <c>docs/STATE_MACHINE.md</c>:
/// o estado persistido é sempre derivado dos pagamentos e das ações explícitas (cancelar/reabrir),
/// e <c>Overdue</c> é calculado apenas na leitura (<see cref="GetPresentedStatus"/>).
/// </summary>
public sealed class Transaction
{
    public const int DescriptionMaxLength = 200;
    public const int NotesMaxLength = 1000;

    private readonly List<TransactionPayment> payments = [];
    private readonly List<TransactionAuditEntry> auditEntries = [];

    private Transaction()
    {
        Description = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Quem cadastrou a movimentação (autoria).</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>
    /// Quem efetivamente pagou/recebeu: o usuário que registrou o pagamento que liquidou a movimentação.
    /// Nulo enquanto não liquidada.
    /// </summary>
    public Guid? PaidByUserId { get; private set; }

    /// <summary>
    /// Grupo dono da movimentação (Etapa 4). Nulo = movimentação individual. Movimentações de grupo nunca têm conta, cartão,
    /// parcelamento nem recorrência (D-032); a conta do pagador fica apenas no pagamento e é privada (D-033).
    /// </summary>
    public Guid? GroupId { get; private set; }

    public bool IsGroupTransaction => GroupId is not null;

    public Guid? AccountId { get; private set; }

    /// <summary>Cartão de crédito da compra (opcional, imutável depois de criada — D-025). Só despesas.</summary>
    public Guid? CreditCardId { get; private set; }

    /// <summary>Parcelamento de origem (parcelas) — nulo para movimentações comuns.</summary>
    public Guid? InstallmentPlanId { get; private set; }

    /// <summary>Número da parcela (1..N) dentro do parcelamento.</summary>
    public int? InstallmentNumber { get; private set; }

    /// <summary>Recorrência de origem (ocorrências) — nulo para movimentações comuns.</summary>
    public Guid? RecurrenceRuleId { get; private set; }

    /// <summary>Data do cronograma que originou a ocorrência (chave de idempotência junto com a regra).</summary>
    public DateOnly? OccurrenceDate { get; private set; }

    /// <summary>Ocorrência editada à mão: é preservada nas regenerações da recorrência (D-027).</summary>
    public bool IsManuallyEdited { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Description { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Soma dos pagamentos (mantida pelo domínio; sempre 0 ≤ valor ≤ <see cref="Amount"/>).</summary>
    public decimal PaidAmount { get; private set; }

    public TransactionType Type { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    /// <summary>Estado persistido (nunca <c>Overdue</c>).</summary>
    public TransactionStatus Status { get; private set; }

    public DateOnly TransactionDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    /// <summary>Data do pagamento que liquidou a movimentação; nulo se não liquidada.</summary>
    public DateOnly? PaidAt { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Category? Category { get; private set; }

    public Account? Account { get; private set; }

    public CreditCard? CreditCard { get; private set; }

    public InstallmentPlan? InstallmentPlan { get; private set; }

    public IReadOnlyCollection<TransactionPayment> Payments => payments;

    public IReadOnlyCollection<TransactionAuditEntry> AuditEntries => auditEntries;

    public decimal RemainingAmount => Amount - PaidAmount;

    public bool IsSettled => Status is TransactionStatus.Paid or TransactionStatus.Received;

    public bool IsInstallment => InstallmentPlanId is not null;

    public bool IsOccurrence => RecurrenceRuleId is not null;

    // ------------------------------------------------------------------ criação

    public static Transaction Create(
        Guid createdByUserId,
        string description,
        TransactionType type,
        decimal amount,
        Category category,
        Account? account,
        PaymentMethod? paymentMethod,
        TransactionStatus status,
        DateOnly transactionDate,
        DateOnly? dueDate,
        string? notes,
        DateTimeOffset now,
        CreditCard? creditCard = null,
        TransactionOrigin? origin = null)
    {
        ArgumentNullException.ThrowIfNull(category);
        Guard.NotEmpty(createdByUserId, "O usuário é obrigatório.");
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Tipo de movimentação inválido.");
        }

        if (status is not (TransactionStatus.Planned or TransactionStatus.Pending))
        {
            throw new DomainException("Uma movimentação nova só pode ser criada como Planejada (Planned) ou Pendente (Pending).");
        }

        var normalizedDescription = NormalizeDescription(description);
        EnsureAmount(amount);
        EnsureCategory(category, type);
        account?.EnsureActive("A conta selecionada está desativada.");

        // Compra no cartão (D-025): só despesa, cartão ativo do próprio usuário, sem conta/vencimento informados;
        // a forma de pagamento é sempre CreditCard e o vencimento é o da fatura que recebe a compra.
        if (creditCard is not null)
        {
            if (type != TransactionType.Expense)
            {
                throw new DomainException("Somente despesas podem ser lançadas no cartão de crédito.");
            }

            if (creditCard.UserId != createdByUserId)
            {
                throw new DomainException("O cartão selecionado não pertence ao usuário.");
            }

            creditCard.EnsureActive("O cartão selecionado está desativado.");
            if (account is not null)
            {
                throw new DomainException("Compras no cartão não aceitam conta: a conta é a associada ao cartão.");
            }

            if (dueDate is not null)
            {
                throw new DomainException("O vencimento de compras no cartão é calculado automaticamente.");
            }

            if (paymentMethod is not null and not Domain.Transactions.PaymentMethod.CreditCard)
            {
                throw new DomainException("A forma de pagamento de compras no cartão deve ser cartão de crédito.");
            }

            paymentMethod = Domain.Transactions.PaymentMethod.CreditCard;
            dueDate = creditCard.CalculateDueDate(transactionDate);
        }

        EnsureDates(transactionDate, dueDate);
        var normalizedNotes = NormalizeNotes(notes);
        var utcNow = Guard.Utc(now);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = createdByUserId,
            AccountId = account?.Id,
            CreditCardId = creditCard?.Id,
            InstallmentPlanId = origin?.InstallmentPlanId,
            InstallmentNumber = origin?.InstallmentNumber,
            RecurrenceRuleId = origin?.RecurrenceRuleId,
            OccurrenceDate = origin?.OccurrenceDate,
            CategoryId = category.Id,
            Description = normalizedDescription,
            Amount = amount,
            PaidAmount = 0m,
            Type = type,
            PaymentMethod = EnsureMethod(paymentMethod),
            Status = status,
            TransactionDate = transactionDate,
            DueDate = dueDate,
            Notes = normalizedNotes,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
        transaction.AddAudit(createdByUserId, TransactionAuditAction.Created, null, null, null, utcNow);
        return transaction;
    }

    /// <summary>
    /// Cria uma movimentação de um grupo (D-032): sem conta/cartão/parcelamento/recorrência. A categoria deve ser padrão
    /// do sistema ou do próprio criador. A permissão de criar (membro ativo de grupo ativo) é verificada por quem chama.
    /// </summary>
    public static Transaction CreateForGroup(
        Guid groupId,
        Guid createdByUserId,
        string description,
        TransactionType type,
        decimal amount,
        Category category,
        PaymentMethod? paymentMethod,
        TransactionStatus status,
        DateOnly transactionDate,
        DateOnly? dueDate,
        string? notes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);
        Guard.NotEmpty(groupId, "O grupo é obrigatório.");
        EnsureGroupCategory(category, createdByUserId);
        var transaction = Create(
            createdByUserId, description, type, amount, category, null, paymentMethod, status, transactionDate, dueDate, notes, now);
        transaction.GroupId = groupId;
        return transaction;
    }

    // ------------------------------------------------------------------ leitura

    /// <summary>Estado apresentado: <c>Overdue</c> quando não liquidada, com vencimento anterior a <paramref name="today"/>.</summary>
    public TransactionStatus GetPresentedStatus(DateOnly today) =>
        ComputePresentedStatus(Status, DueDate, RemainingAmount, today);

    /// <summary>Regra de <c>Overdue</c> isolada para ser reutilizada em projeções de leitura.</summary>
    public static TransactionStatus ComputePresentedStatus(
        TransactionStatus persisted,
        DateOnly? dueDate,
        decimal remainingAmount,
        DateOnly today) =>
        persisted is TransactionStatus.Planned or TransactionStatus.Pending
        && remainingAmount > 0
        && dueDate is { } due
        && due < today
            ? TransactionStatus.Overdue
            : persisted;

    // ------------------------------------------------------------------ edição

    /// <summary>
    /// Atualiza os dados editáveis (o tipo é imutável, D-023). <paramref name="requestedStatus"/> só pode ser
    /// <c>Planned</c>/<c>Pending</c> e só altera o estado de movimentações sem pagamentos.
    /// </summary>
    public void Update(
        Guid userId,
        string description,
        decimal amount,
        Category category,
        Account? account,
        PaymentMethod? paymentMethod,
        DateOnly transactionDate,
        DateOnly? dueDate,
        string? notes,
        TransactionStatus? requestedStatus,
        DateTimeOffset now,
        CreditCard? creditCard = null)
    {
        ArgumentNullException.ThrowIfNull(category);
        EnsureGroupAuthor(userId, "Somente quem cadastrou a movimentação pode editá-la.");
        EnsureNotCanceled("Movimentações canceladas não podem ser editadas. Reabra a movimentação antes.");
        if (GroupId is not null)
        {
            if (account is not null || creditCard is not null)
            {
                throw new DomainException("Movimentações de grupo não aceitam conta nem cartão.");
            }

            EnsureGroupCategory(category, userId);
        }

        // Parcelas: valor e data são imutáveis (D-026).
        if (InstallmentPlanId is not null)
        {
            if (amount != Amount)
            {
                throw new DomainException("O valor de uma parcela não pode ser alterado.");
            }

            if (transactionDate != TransactionDate)
            {
                throw new DomainException("A data de uma parcela não pode ser alterada.");
            }
        }

        // Compra no cartão (D-025): sem conta; o vencimento é o da fatura e só é recalculado se a data da compra mudar.
        if (CreditCardId is not null)
        {
            if (creditCard is null || creditCard.Id != CreditCardId)
            {
                throw new InvalidOperationException("O cartão da compra não foi carregado.");
            }

            if (account is not null)
            {
                throw new DomainException("Compras no cartão não aceitam conta: a conta é a associada ao cartão.");
            }

            if (dueDate is not null)
            {
                throw new DomainException("O vencimento de compras no cartão é calculado automaticamente.");
            }

            if (paymentMethod is not null and not Domain.Transactions.PaymentMethod.CreditCard)
            {
                throw new DomainException("A forma de pagamento de compras no cartão deve ser cartão de crédito.");
            }

            paymentMethod = Domain.Transactions.PaymentMethod.CreditCard;
            dueDate = transactionDate == TransactionDate ? DueDate : creditCard.CalculateDueDate(transactionDate);
        }

        if (requestedStatus is not null and not (TransactionStatus.Planned or TransactionStatus.Pending))
        {
            throw new DomainException("O estado informado só pode ser Planejada (Planned) ou Pendente (Pending).");
        }

        var normalizedDescription = NormalizeDescription(description);
        EnsureAmount(amount);
        if (amount < PaidAmount)
        {
            throw new DomainException(
                $"O valor não pode ser menor que o total já pago ({MoneyRules.Format(PaidAmount)}).");
        }

        EnsureDates(transactionDate, dueDate);

        // Categoria/conta só são revalidadas (ativa) quando trocadas: editar outros campos continua possível.
        var categoryChanged = category.Id != CategoryId;
        if (categoryChanged)
        {
            EnsureCategory(category, Type);
        }
        else if (!category.Accepts(Type))
        {
            throw new DomainException("A categoria deve ser do mesmo tipo da movimentação.");
        }

        var accountChanged = account?.Id != AccountId;
        if (accountChanged)
        {
            account?.EnsureActive("A conta selecionada está desativada.");
        }

        var normalizedNotes = NormalizeNotes(notes);
        var method = EnsureMethod(paymentMethod);
        var utcNow = Guard.Utc(now);

        // Valida o estado resultante antes de alterar qualquer campo (sem mutação parcial).
        var wasSettled = IsSettled;
        var newStatus = ResolveStatusAfterAmountChange(amount, requestedStatus);
        var becomesSettled = newStatus is TransactionStatus.Paid or TransactionStatus.Received && !wasSettled;
        if (becomesSettled)
        {
            EnsurePaymentsLoaded();
        }

        var previousCategoryName = Category?.Name ?? CategoryId.ToString();
        var previousAccountName = Account?.Name ?? AccountId?.ToString();
        var oldStatus = Status;

        RecordChange(userId, "description", Description, normalizedDescription, utcNow);
        RecordChange(userId, "amount", FormatAmount(Amount), FormatAmount(amount), utcNow);
        if (categoryChanged)
        {
            RecordChange(userId, "category", previousCategoryName, category.Name, utcNow);
        }

        if (accountChanged)
        {
            RecordChange(userId, "account", previousAccountName, account?.Name, utcNow);
        }

        RecordChange(userId, "paymentMethod", PaymentMethod?.ToString(), method?.ToString(), utcNow);
        RecordChange(userId, "transactionDate", FormatDate(TransactionDate), FormatDate(transactionDate), utcNow);
        RecordChange(userId, "dueDate", FormatDate(DueDate), FormatDate(dueDate), utcNow);
        RecordChange(userId, "notes", Notes, normalizedNotes, utcNow);

        Description = normalizedDescription;
        Amount = amount;
        CategoryId = category.Id;
        AccountId = account?.Id;
        PaymentMethod = method;
        TransactionDate = transactionDate;
        DueDate = dueDate;
        Notes = normalizedNotes;

        Status = newStatus;
        if (becomesSettled)
        {
            RefreshSettlementMarkers();
        }
        else if (!IsSettled)
        {
            PaidAt = null;
            PaidByUserId = null;
        }

        RecordChange(userId, "status", oldStatus.ToString(), Status.ToString(), utcNow);

        // Editar uma ocorrência a protege de regenerações da recorrência (D-027).
        if (RecurrenceRuleId is not null)
        {
            IsManuallyEdited = true;
        }

        UpdatedAt = utcNow;
    }

    // ------------------------------------------------------------------ pagamentos

    /// <summary>
    /// Registra um pagamento (despesa) ou recebimento (receita). Liquida a movimentação quando o total
    /// pago alcança o valor; caso contrário o estado passa a <c>Pending</c>.
    /// </summary>
    public TransactionPayment AddPayment(
        Guid userId,
        decimal amount,
        DateOnly paymentDate,
        DateOnly today,
        Account? account,
        PaymentMethod? paymentMethod,
        DateTimeOffset now)
    {
        EnsureNotCanceled("Movimentações canceladas não aceitam pagamentos. Reabra a movimentação antes.");
        if (amount <= 0)
        {
            throw new DomainException("O valor do pagamento deve ser maior que zero.");
        }

        if (!MoneyRules.HasValidScale(amount) || !MoneyRules.IsWithinRange(amount))
        {
            throw new DomainException("O valor do pagamento é inválido (máximo de 2 casas decimais).");
        }

        if (paymentDate > today)
        {
            throw new DomainException("A data do pagamento não pode ser futura.");
        }

        if (amount > RemainingAmount)
        {
            throw new DomainException(
                $"O valor do pagamento ({MoneyRules.Format(amount)}) excede o valor restante da movimentação ({MoneyRules.Format(RemainingAmount)}).");
        }

        account?.EnsureActive("A conta selecionada está desativada.");
        if (GroupId is not null && account is not null && account.UserId != userId)
        {
            throw new DomainException("A conta do pagamento deve ser do próprio pagador.");
        }

        var utcNow = Guard.Utc(now);
        var payment = TransactionPayment.Create(Id, amount, paymentDate, userId, account?.Id, EnsureMethod(paymentMethod), utcNow);
        payments.Add(payment);
        PaidAmount += amount;

        if (PaidAmount == Amount)
        {
            Status = Type == TransactionType.Expense ? TransactionStatus.Paid : TransactionStatus.Received;
            PaidAt = paymentDate;
            PaidByUserId = userId;
        }
        else
        {
            Status = TransactionStatus.Pending;
            PaidAt = null;
            PaidByUserId = null;
        }

        UpdatedAt = utcNow;
        AddAudit(userId, TransactionAuditAction.PaymentAdded, "payment", null, FormatPayment(payment), utcNow);
        return payment;
    }

    public void RemovePayment(Guid userId, Guid paymentId, DateTimeOffset now)
    {
        EnsureNotCanceled("Não é possível remover pagamentos de uma movimentação cancelada. Reabra a movimentação antes.");
        EnsurePaymentsLoaded();
        var payment = payments.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new DomainException("Pagamento não encontrado nesta movimentação.");
        if (GroupId is not null && payment.CreatedByUserId != userId)
        {
            throw new PermissionDeniedException("Somente quem registrou o pagamento pode removê-lo.");
        }

        var utcNow = Guard.Utc(now);
        payments.Remove(payment);
        PaidAmount -= payment.Amount;
        Status = TransactionStatus.Pending;
        PaidAt = null;
        PaidByUserId = null;
        UpdatedAt = utcNow;
        AddAudit(userId, TransactionAuditAction.PaymentRemoved, "payment", FormatPayment(payment), null, utcNow);
    }

    // ------------------------------------------------------------------ cancelar / reabrir / excluir

    /// <summary>Cancela preservando os pagamentos; a movimentação sai de totais e do saldo projetado.</summary>
    public void Cancel(Guid userId, DateTimeOffset now)
    {
        EnsureGroupAuthor(userId, "Somente quem cadastrou a movimentação pode cancelá-la.");
        if (Status == TransactionStatus.Canceled)
        {
            throw new DomainException("A movimentação já está cancelada.");
        }

        var utcNow = Guard.Utc(now);
        var old = Status;
        Status = TransactionStatus.Canceled;
        PaidAt = null;
        PaidByUserId = null;
        UpdatedAt = utcNow;
        AddAudit(userId, TransactionAuditAction.Canceled, "status", old.ToString(), Status.ToString(), utcNow);
    }

    /// <summary>Reabre uma movimentação cancelada; o estado é derivado dos pagamentos (sem pagamentos → Pending).</summary>
    public void Reopen(Guid userId, DateTimeOffset now)
    {
        EnsureGroupAuthor(userId, "Somente quem cadastrou a movimentação pode reabri-la.");
        if (Status != TransactionStatus.Canceled)
        {
            throw new DomainException("Somente movimentações canceladas podem ser reabertas.");
        }

        EnsurePaymentsLoaded();
        var utcNow = Guard.Utc(now);
        if (PaidAmount == Amount)
        {
            Status = Type == TransactionType.Expense ? TransactionStatus.Paid : TransactionStatus.Received;
            RefreshSettlementMarkers();
        }
        else
        {
            Status = TransactionStatus.Pending;
            PaidAt = null;
            PaidByUserId = null;
        }

        UpdatedAt = utcNow;
        AddAudit(userId, TransactionAuditAction.Reopened, "status", TransactionStatus.Canceled.ToString(), Status.ToString(), utcNow);
    }

    /// <summary>Exclusão por um usuário específico: em movimentações de grupo, somente o criador (D-032); depois, as regras de D-020.</summary>
    public void EnsureCanBeDeleted(Guid userId)
    {
        EnsureGroupAuthor(userId, "Somente quem cadastrou a movimentação pode excluí-la.");
        EnsureCanBeDeleted();
    }

    /// <summary>Movimentações com pagamentos só podem ser canceladas, nunca excluídas (D-020).</summary>
    public void EnsureCanBeDeleted()
    {
        if (InstallmentPlanId is not null)
        {
            throw new DomainException(
                "Parcelas não podem ser excluídas individualmente. Cancele a parcela ou exclua o parcelamento.");
        }

        if (RecurrenceRuleId is not null)
        {
            throw new DomainException(
                "Ocorrências de recorrência não podem ser excluídas individualmente. Cancele a ocorrência ou desative a recorrência.");
        }

        if (PaidAmount > 0)
        {
            throw new DomainException(
                "Movimentações com pagamentos não podem ser excluídas. Remova os pagamentos ou cancele a movimentação.");
        }
    }

    // ------------------------------------------------------------------ internos

    /// <summary>
    /// Em movimentações de grupo, somente quem as cadastrou pode editar, cancelar, reabrir ou excluir (CLAUDE.md §6, D-032);
    /// o papel de administrador não concede essa permissão. Permite ao chamador falhar com 403 antes de outras verificações.
    /// </summary>
    public void EnsureCanBeChangedBy(Guid userId) =>
        EnsureGroupAuthor(userId, "Somente quem cadastrou a movimentação pode alterá-la.");

    /// <summary>Movimentações de grupo só são alteradas por quem as cadastrou (CLAUDE.md §6, D-032).</summary>
    private void EnsureGroupAuthor(Guid userId, string message)
    {
        if (GroupId is not null && userId != CreatedByUserId)
        {
            throw new PermissionDeniedException(message);
        }
    }

    private static void EnsureGroupCategory(Category category, Guid creatorUserId)
    {
        if (!category.IsDefault && category.UserId != creatorUserId)
        {
            throw new DomainException("A categoria deve ser padrão do sistema ou do próprio usuário.");
        }
    }

    private TransactionStatus ResolveStatusAfterAmountChange(decimal newAmount, TransactionStatus? requestedStatus)
    {
        if (PaidAmount == newAmount)
        {
            return Type == TransactionType.Expense ? TransactionStatus.Paid : TransactionStatus.Received;
        }

        if (PaidAmount > 0)
        {
            if (requestedStatus == TransactionStatus.Planned)
            {
                throw new DomainException("Movimentações com pagamentos não podem voltar para Planejada.");
            }

            return TransactionStatus.Pending;
        }

        return requestedStatus ?? Status;
    }

    /// <summary>Define <c>PaidAt</c>/<c>PaidByUserId</c> a partir do pagamento mais recente registrado.</summary>
    private void RefreshSettlementMarkers()
    {
        var completing = payments
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.PaymentDate)
            .LastOrDefault();
        PaidAt = completing?.PaymentDate;
        PaidByUserId = completing?.CreatedByUserId;
    }

    private void EnsurePaymentsLoaded()
    {
        if (payments.Sum(p => p.Amount) != PaidAmount)
        {
            throw new InvalidOperationException("Os pagamentos da movimentação não foram carregados.");
        }
    }

    private void EnsureNotCanceled(string message)
    {
        if (Status == TransactionStatus.Canceled)
        {
            throw new DomainException(message);
        }
    }

    private void RecordChange(Guid userId, string field, string? oldValue, string? newValue, DateTimeOffset now)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        AddAudit(userId, TransactionAuditAction.Updated, field, oldValue, newValue, now);
    }

    private void AddAudit(
        Guid userId,
        TransactionAuditAction action,
        string? field,
        string? oldValue,
        string? newValue,
        DateTimeOffset now) =>
        auditEntries.Add(TransactionAuditEntry.Create(Id, userId, action, field, oldValue, newValue, now));

    private static string NormalizeDescription(string description)
    {
        var trimmed = Guard.NotBlank(description, "A descrição é obrigatória.").Trim();
        if (trimmed.Length > DescriptionMaxLength)
        {
            throw new DomainException($"A descrição deve ter no máximo {DescriptionMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeNotes(string? notes)
    {
        var trimmed = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmed is { Length: > NotesMaxLength })
        {
            throw new DomainException($"As observações devem ter no máximo {NotesMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static void EnsureAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("O valor deve ser maior que zero.");
        }

        if (!MoneyRules.HasValidScale(amount))
        {
            throw new DomainException("O valor deve ter no máximo 2 casas decimais.");
        }

        if (!MoneyRules.IsWithinRange(amount))
        {
            throw new DomainException("O valor excede o limite permitido.");
        }
    }

    private static void EnsureDates(DateOnly transactionDate, DateOnly? dueDate)
    {
        if (dueDate is { } due && due < transactionDate)
        {
            throw new DomainException("O vencimento não pode ser anterior à data da movimentação.");
        }
    }

    private static void EnsureCategory(Category category, TransactionType type)
    {
        if (!category.Accepts(type))
        {
            throw new DomainException("A categoria deve ser do mesmo tipo da movimentação.");
        }

        if (!category.IsActive)
        {
            throw new DomainException("A categoria selecionada está inativa.");
        }
    }

    private static PaymentMethod? EnsureMethod(PaymentMethod? method) =>
        method is { } m && !Enum.IsDefined(m) ? throw new DomainException("Forma de pagamento inválida.") : method;

    private static string FormatAmount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string? FormatDate(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatPayment(TransactionPayment payment) =>
        $"{FormatAmount(payment.Amount)} ({FormatDate(payment.PaymentDate)})";
}
