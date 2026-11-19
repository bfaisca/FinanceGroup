/**
 * Tipos espelhando exatamente docs/API_CONTRACTS.md (v1.0 — Etapa 1).
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
  'Created' | 'Updated' | 'PaymentAdded' | 'PaymentRemoved' | 'Canceled' | 'Reopened'

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
}

export interface DashboardQuery {
  from: DateOnlyString
  to: DateOnlyString
}
