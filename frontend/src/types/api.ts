/**
 * Tipos espelhando exatamente docs/API_CONTRACTS.md (v1.2 — Etapas 1 a 3).
 * Não adicionar campos que não constem no contrato.
 */

// ---------- 1. Convenções gerais ----------

/** UUID serializado como string. */
export type Uuid = string

/** `DateOnly` → "YYYY-MM-DD". */
export type DateOnlyString = string

/** `DateTimeOffset` UTC ISO 8601 → "2026-11-18T22:40:00Z". */
export type IsoDateTimeString = string

/** 1.1 Paginação — resposta das listagens. */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** 1.1 Paginação, ordenação e filtros — query string das listagens. */
export interface PageQuery {
  /** Padrão 1. */
  page?: number
  /** Padrão 20, máximo 100. */
  pageSize?: number
  /** Campo; prefixo `-` para descendente (ex.: `-transactionDate`). */
  sort?: string
}

/** 1.2 Códigos estáveis de erro (`code`). */
export type ApiErrorCode =
  | 'validation_error'
  | 'invalid_token'
  | 'unauthorized'
  | 'invalid_credentials'
  | 'invalid_refresh_token'
  | 'invalid_password'
  | 'forbidden'
  | 'not_found'
  | 'email_already_registered'
  | 'conflict'
  | 'business_rule_violation'
  | 'too_many_requests'
  | 'internal_error'

/** 1.2 Erros — `application/problem+json` (RFC 7807 + extensões `code`, `traceId`, `errors`). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  code?: ApiErrorCode
  traceId?: string
  /** Só existe em `validation_error`. Chaves em camelCase, iguais aos campos do request. */
  errors?: Record<string, string[]>
}

// ---------- 2. Health ----------

export interface HealthResponse {
  status: string
}

// ---------- 3. Autenticação ----------

export interface UserDto {
  id: Uuid
  name: string
  email: string
  /** ISO UTC */
  createdAt: IsoDateTimeString
}

export interface AuthResponse {
  accessToken: string
  /** ISO UTC */
  accessTokenExpiresAt: IsoDateTimeString
  user: UserDto
}

export interface RegisterRequest {
  name: string
  email: string
  password: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface ForgotPasswordRequest {
  email: string
}

export interface ResetPasswordRequest {
  token: string
  newPassword: string
}

// ---------- 4. Usuário autenticado ----------

export interface UpdateProfileRequest {
  name: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

export interface DeleteAccountRequest {
  password: string
}

// ---------- 5. Enumerações compartilhadas ----------

export type TransactionType = 'Expense' | 'Income'
export type TransactionStatus = 'Planned' | 'Pending' | 'Paid' | 'Received' | 'Overdue' | 'Canceled'
export type PaymentMethod = 'Cash' | 'DebitCard' | 'CreditCard' | 'Pix' | 'BankTransfer' | 'BankSlip' | 'Other'
export type AccountType = 'Checking' | 'Savings' | 'Wallet' | 'Investment' | 'Other'
export type CategoryType = 'Expense' | 'Income'
export type RecurrenceFrequency =
  'Daily' | 'Weekly' | 'Biweekly' | 'Monthly' | 'Bimonthly' | 'Quarterly' | 'Semiannual' | 'Annual' | 'Custom'
export type GroupMemberRole = 'Owner' | 'Admin' | 'Member'
export type GroupMemberStatus = 'Active' | 'Left' | 'Removed'
export type GroupInvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked' | 'Expired'

// ---------- 6. Contas ----------

export interface AccountSnapshotSummary {
  balance: number
  referenceDate: DateOnlyString
}

export interface AccountDto {
  id: Uuid
  name: string
  institution: string | null
  type: AccountType
  currency: 'BRL'
  isActive: boolean
  /** Snapshot de referência (D-019). */
  latestSnapshot: AccountSnapshotSummary | null
  /** PROJETADO, não é saldo bancário; `null` se não há snapshot. */
  projectedBalance: number | null
  /** Data-alvo usada na projeção. */
  projectedAsOf: DateOnlyString
  createdAt: IsoDateTimeString
  updatedAt: IsoDateTimeString
}

export interface BalanceSnapshotDto {
  id: Uuid
  balance: number
  referenceDate: DateOnlyString
  createdAt: IsoDateTimeString
}

export interface CreateAccountRequest {
  name: string
  institution?: string | null
  type: AccountType
  initialBalance?: number
  balanceReferenceDate?: DateOnlyString
}

export interface UpdateAccountRequest {
  name: string
  institution?: string | null
  type: AccountType
}

export interface CreateBalanceSnapshotRequest {
  balance: number
  referenceDate: DateOnlyString
}

// ---------- 7. Categorias ----------

export interface CategoryDto {
  id: Uuid
  name: string
  type: CategoryType
  parentCategoryId: Uuid | null
  /** Nome de ícone PrimeIcons, ex.: "pi-shopping-cart". */
  icon: string | null
  /** "#RRGGBB" */
  color: string | null
  /** Padrão do sistema (somente leitura). */
  isDefault: boolean
  isActive: boolean
}

export interface CreateCategoryRequest {
  name: string
  type: CategoryType
  parentCategoryId?: Uuid | null
  icon?: string | null
  color?: string | null
}

export interface UpdateCategoryRequest {
  name: string
  icon?: string | null
  color?: string | null
}

// ---------- 8. Movimentações ----------

export interface TransactionDto {
  id: Uuid
  description: string
  type: TransactionType
  amount: number
  /** Soma dos pagamentos. */
  paidAmount: number
  /** amount - paidAmount */
  remainingAmount: number
  /** Apresentado: inclui `Overdue` (docs/STATE_MACHINE.md). */
  status: TransactionStatus
  categoryId: Uuid
  categoryName: string
  parentCategoryName: string | null
  categoryColor: string | null
  categoryIcon: string | null
  accountId: Uuid | null
  accountName: string | null
  paymentMethod: PaymentMethod | null
  transactionDate: DateOnlyString
  dueDate: DateOnlyString | null
  paidAt: DateOnlyString | null
  notes: string | null
  createdAt: IsoDateTimeString
  updatedAt: IsoDateTimeString
  // ---- Etapa 3 (§10, aditivo) ----
  creditCardId: Uuid | null
  creditCardName: string | null
  installment: TransactionInstallment | null
  /** Não nulo = ocorrência de recorrência. */
  recurrenceRuleId: Uuid | null
  /**
   * D-060: preenchido quando a linha é um pagamento de grupo feito com uma conta sua (somente leitura). O `id` da
   * linha é o do pagamento; para abrir a movimentação, vá ao grupo (`groupTransactionId`).
   */
  groupPayment?: GroupPaymentRef | null
}

export interface GroupPaymentRef {
  groupId: Uuid
  groupName: string
  groupTransactionId: Uuid
}

/** §10: parcela de um plano de parcelamento. */
export interface TransactionInstallment {
  planId: Uuid
  number: number
  count: number
}

export interface TransactionPaymentDto {
  id: Uuid
  amount: number
  paymentDate: DateOnlyString
  accountId: Uuid | null
  accountName: string | null
  paymentMethod: PaymentMethod | null
  createdAt: IsoDateTimeString
}

export interface TransactionDetailDto extends TransactionDto {
  payments: TransactionPaymentDto[]
}

export type TransactionHistoryAction =
  'Created' | 'Updated' | 'PaymentAdded' | 'PaymentUpdated' | 'PaymentRemoved' | 'Canceled' | 'Reopened'

export interface TransactionHistoryEntryDto {
  id: Uuid
  action: TransactionHistoryAction
  /** Ex.: "amount", "dueDate" (uma linha por campo alterado). */
  field: string | null
  oldValue: string | null
  newValue: string | null
  createdAt: IsoDateTimeString
}

/** Situações aceitas na criação/edição. */
export type TransactionInitialStatus = 'Planned' | 'Pending'

export interface CreateTransactionRequest {
  description: string
  type: TransactionType
  amount: number
  categoryId: Uuid
  accountId?: Uuid | null
  paymentMethod?: PaymentMethod | null
  /** Padrão `Pending`. */
  status?: TransactionInitialStatus
  transactionDate: DateOnlyString
  dueDate?: DateOnlyString | null
  notes?: string | null
  /** §10: só em despesas; com cartão, `accountId` e `dueDate` não são enviados. Imutável após criar. */
  creditCardId?: Uuid | null
  /** D-059: lançamento retroativo — já nasce quitado com um pagamento/recebimento total nesta data (não futura). */
  paymentDate?: DateOnlyString | null
}

/** Mesmos campos da criação, exceto `type` (imutável); `status` opcional (Planned | Pending). */
export type UpdateTransactionRequest = Omit<CreateTransactionRequest, 'type'>

export interface CreatePaymentRequest {
  amount: number
  paymentDate: DateOnlyString
  accountId?: Uuid | null
  paymentMethod?: PaymentMethod | null
}

export type TransactionSortField = 'transactionDate' | 'dueDate' | 'amount' | 'description' | 'createdAt'

/** Query de `GET /transactions`. */
export interface TransactionListQuery extends PageQuery {
  from?: DateOnlyString
  to?: DateOnlyString
  dueFrom?: DateOnlyString
  dueTo?: DateOnlyString
  type?: TransactionType
  /** Inclui subcategorias. */
  categoryId?: Uuid
  accountId?: Uuid
  /** Status apresentado; aceita `Overdue`. */
  status?: TransactionStatus
  search?: string
  // ---- Etapa 3 (§10) ----
  creditCardId?: Uuid
  installmentPlanId?: Uuid
  recurrenceRuleId?: Uuid
}

// ---------- 9. Dashboard ----------

export interface DashboardDto {
  period: { from: DateOnlyString; to: DateOnlyString }
  balance: {
    reportedTotal: number | null
    projectedTotal: number | null
    projectedAsOf: DateOnlyString
    accountsWithBalance: number
    accountsWithoutBalance: number
  }
  totals: {
    income: number
    expense: number
    result: number
    receivedIncome: number
    pendingIncome: number
    paidExpense: number
    pendingExpense: number
  }
  expensesByCategory: {
    categoryId: Uuid
    categoryName: string
    color: string | null
    total: number
    percentage: number
  }[]
  upcomingDues: TransactionDto[]
  monthlyHistory: { month: string; income: number; expense: number; result: number }[]
  today: DateOnlyString
  /** §15 (Etapa 3): cartões ATIVOS, por nome. */
  creditCards: DashboardCreditCard[]
}

export interface DashboardCreditCard {
  id: Uuid
  name: string
  creditLimit: number
  usedLimit: number
  availableLimit: number
  openInvoice: InvoiceSummary
}

export interface DashboardQuery {
  from: DateOnlyString
  to: DateOnlyString
}

// ---------- 11. Cartões de crédito (Etapa 3) ----------

/** Resumo simplificado da fatura que recebe compras feitas hoje (D-025); não é gestão de faturas. */
export interface InvoiceSummary {
  closingDate: DateOnlyString
  dueDate: DateOnlyString
  amount: number
}

export interface CreditCardDto {
  id: Uuid
  name: string
  institution: string | null
  creditLimit: number
  /** 1–31 */
  closingDay: number
  /** 1–31 */
  dueDay: number
  accountId: Uuid | null
  accountName: string | null
  isActive: boolean
  /** D-025: soma do restante de tudo que está em aberto. */
  usedLimit: number
  /** `creditLimit - usedLimit`; negativo = limite excedido. */
  availableLimit: number
  openInvoice: InvoiceSummary
  createdAt: IsoDateTimeString
  updatedAt: IsoDateTimeString
}

/** Fatura com valor em aberto (`GET /credit-cards/{id}/invoices`, D-057). */
export interface CardInvoiceDto {
  dueDate: DateOnlyString
  totalAmount: number
  paidAmount: number
  remainingAmount: number
  purchaseCount: number
  isOverdue: boolean
}

export interface PayInvoiceResult {
  dueDate: DateOnlyString
  paymentDate: DateOnlyString
  paidAmount: number
  purchasesPaid: number
}

/** POST e PUT têm o mesmo corpo. */
export interface CreditCardRequest {
  name: string
  institution?: string | null
  creditLimit: number
  closingDay: number
  dueDay: number
  accountId?: Uuid | null
}

// ---------- 12. Parcelamentos (Etapa 3) ----------

export type InstallmentPlanStatus = 'Open' | 'Completed' | 'Canceled'

export interface InstallmentPlanDto {
  id: Uuid
  description: string
  totalAmount: number
  installmentCount: number
  firstDate: DateOnlyString
  categoryId: Uuid
  categoryName: string
  accountId: Uuid | null
  accountName: string | null
  creditCardId: Uuid | null
  creditCardName: string | null
  paymentMethod: PaymentMethod | null
  status: InstallmentPlanStatus
  paidCount: number
  openCount: number
  canceledCount: number
  /** Somas de parcelas não canceladas. */
  paidAmount: number
  remainingAmount: number
  /** Menor data efetiva (`dueDate ?? transactionDate`) de parcela em aberto. */
  nextDueDate: DateOnlyString | null
  createdAt: IsoDateTimeString
}

/** Parcelas em ordem por número. */
export interface InstallmentPlanDetailDto extends InstallmentPlanDto {
  installments: TransactionDto[]
}

export interface CreateInstallmentPlanRequest {
  description: string
  totalAmount: number
  installmentCount: number
  firstDate: DateOnlyString
  categoryId: Uuid
  accountId?: Uuid | null
  creditCardId?: Uuid | null
  paymentMethod?: PaymentMethod | null
  notes?: string | null
}

export type InstallmentPlanSortField = 'firstDate' | 'description' | 'totalAmount' | 'createdAt'

export interface InstallmentPlanListQuery extends PageQuery {
  status?: InstallmentPlanStatus
}

// ---------- 13. Recorrências (Etapa 3) ----------

export interface RecurrenceDto {
  id: Uuid
  description: string
  type: TransactionType
  amount: number
  categoryId: Uuid
  categoryName: string
  accountId: Uuid | null
  accountName: string | null
  creditCardId: Uuid | null
  creditCardName: string | null
  paymentMethod: PaymentMethod | null
  notes: string | null
  frequency: RecurrenceFrequency
  /** D-027; para `Custom` = dias. */
  interval: number
  startDate: DateOnlyString
  endDate: DateOnlyString | null
  isActive: boolean
  /** Próxima ocorrência futura (>= hoje); `null` se encerrada/inativa. */
  nextOccurrenceDate: DateOnlyString | null
  /** Ocorrências já geradas (inclui canceladas). */
  generatedCount: number
  createdAt: IsoDateTimeString
  updatedAt: IsoDateTimeString
}

export interface CreateRecurrenceRequest {
  description: string
  type: TransactionType
  amount: number
  categoryId: Uuid
  accountId?: Uuid | null
  creditCardId?: Uuid | null
  paymentMethod?: PaymentMethod | null
  notes?: string | null
  frequency: RecurrenceFrequency
  /** Padrão 1. */
  interval?: number
  startDate: DateOnlyString
  endDate?: DateOnlyString | null
}

/** Mesmos campos, exceto `type` e `startDate` (imutáveis). */
export type UpdateRecurrenceRequest = Omit<CreateRecurrenceRequest, 'type' | 'startDate'>

// ---------- 14. Relatórios (Etapa 3) ----------

export interface ReportPeriod {
  from: DateOnlyString
  to: DateOnlyString
}

export interface ReportTotals {
  income: number
  expense: number
  result: number
  receivedIncome: number
  pendingIncome: number
  paidExpense: number
  pendingExpense: number
}

export type ReportGroupBy = 'day' | 'month'

export interface ReportSummaryDto {
  period: ReportPeriod
  totals: ReportTotals
  /** `key` = `YYYY-MM-DD` ou `YYYY-MM`, cronológico. */
  series: { key: string; income: number; expense: number; result: number }[]
}

export interface ReportByCategoryDto {
  period: ReportPeriod
  type: TransactionType
  total: number
  items: {
    categoryId: Uuid
    categoryName: string
    color: string | null
    total: number
    percentage: number
    /** Percentuais relativos ao total da categoria RAIZ. */
    subcategories: { categoryId: Uuid; categoryName: string; total: number; percentage: number }[]
  }[]
}

export interface ReportByAccountDto {
  period: ReportPeriod
  type: TransactionType
  total: number
  /** Sem conta → `accountId: null`, `accountName: "Sem conta"`. */
  items: { accountId: Uuid | null; accountName: string; total: number; percentage: number }[]
}

export interface ReportByPaymentMethodDto {
  period: ReportPeriod
  type: TransactionType
  total: number
  /** `null` = "Não informado". */
  items: { paymentMethod: PaymentMethod | null; total: number; percentage: number }[]
}

export interface ReportMonthlyEvolutionDto {
  items: { month: string; income: number; expense: number; result: number; cumulativeResult: number }[]
}

export interface ReportInstallmentsDto {
  openPlans: number
  totalRemaining: number
  commitmentByMonth: { month: string; amount: number; installmentsCount: number }[]
  /** Planos `Open` por `nextDueDate`. */
  plans: InstallmentPlanDto[]
}

export interface ReportRecurrencesDto {
  activeRules: number
  upcomingByMonth: { month: string; income: number; expense: number }[]
  rules: {
    id: Uuid
    description: string
    type: TransactionType
    amount: number
    frequency: RecurrenceFrequency
    interval: number
    nextOccurrenceDate: DateOnlyString | null
    upcomingTotal12m: number
  }[]
}
