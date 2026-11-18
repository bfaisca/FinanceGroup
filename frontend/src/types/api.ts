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
