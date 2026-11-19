import type {
  AccountType,
  CreateAccountRequest,
  CreateBalanceSnapshotRequest,
  DateOnlyString,
  UpdateAccountRequest,
} from '@/types/api'
import { parseDateOnly } from '@/utils/format'
import { fromCents, MAX_CENTS, parseMoneyToCents } from '@/utils/money'

export const ACCOUNT_NAME_MAX = 100
export const INSTITUTION_MAX = 100

export interface AccountFormValues {
  name: string
  institution: string
  type: AccountType | null
  /** Texto pt-BR; vazio = conta sem snapshot inicial. Só na criação. */
  initialBalance: string
  /** Só na criação; vazio com saldo informado = hoje (backend). */
  balanceReferenceDate: DateOnlyString | null
}

export const ACCOUNT_FORM_FIELDS = ['name', 'institution', 'type', 'initialBalance', 'balanceReferenceDate'] as const
export type AccountFormField = (typeof ACCOUNT_FORM_FIELDS)[number]

/** Validação de UX do saldo (aceita negativo). Retorna a mensagem ou `undefined`. */
export function validateBalanceText(text: string): string | undefined {
  if (text.trim().length === 0) return 'Informe o saldo.'
  const cents = parseMoneyToCents(text, { allowNegative: true })
  if (cents === null) return 'Informe um valor válido, com até 2 casas decimais.'
  if (Math.abs(cents) > MAX_CENTS) return 'Valor acima do limite permitido.'
  return undefined
}

export function validateAccountForm(
  values: AccountFormValues,
  options: { today: DateOnlyString; creating: boolean },
): Partial<Record<AccountFormField, string>> {
  const errors: Partial<Record<AccountFormField, string>> = {}

  const name = values.name.trim()
  if (name.length === 0) errors.name = 'Informe o nome da conta.'
  else if (name.length > ACCOUNT_NAME_MAX) errors.name = `O nome deve ter até ${ACCOUNT_NAME_MAX} caracteres.`

  if (values.institution.trim().length > INSTITUTION_MAX) {
    errors.institution = `A instituição deve ter até ${INSTITUTION_MAX} caracteres.`
  }
  if (!values.type) errors.type = 'Escolha o tipo da conta.'

  if (options.creating) {
    const hasBalance = values.initialBalance.trim().length > 0
    if (hasBalance) {
      const message = validateBalanceText(values.initialBalance)
      if (message) errors.initialBalance = message
    }
    if (values.balanceReferenceDate) {
      if (!hasBalance) errors.initialBalance = 'Informe o saldo inicial ou limpe a data de referência.'
      if (!parseDateOnly(values.balanceReferenceDate)) errors.balanceReferenceDate = 'Informe uma data válida.'
      else if (values.balanceReferenceDate > options.today) errors.balanceReferenceDate = 'A data não pode ser futura.'
    }
  }
  return errors
}

export function buildCreateAccountRequest(values: AccountFormValues): CreateAccountRequest {
  const request: CreateAccountRequest = {
    name: values.name.trim(),
    institution: values.institution.trim() || null,
    type: values.type as AccountType,
  }
  const cents = values.initialBalance.trim() ? parseMoneyToCents(values.initialBalance, { allowNegative: true }) : null
  if (cents !== null) {
    request.initialBalance = fromCents(cents)
    if (values.balanceReferenceDate) request.balanceReferenceDate = values.balanceReferenceDate
  }
  return request
}

export function buildUpdateAccountRequest(values: AccountFormValues): UpdateAccountRequest {
  return {
    name: values.name.trim(),
    institution: values.institution.trim() || null,
    type: values.type as AccountType,
  }
}

// ---------- Novo saldo ----------

export interface SnapshotFormValues {
  balance: string
  referenceDate: DateOnlyString | null
}

export const SNAPSHOT_FORM_FIELDS = ['balance', 'referenceDate'] as const
export type SnapshotFormField = (typeof SNAPSHOT_FORM_FIELDS)[number]

export function validateSnapshotForm(
  values: SnapshotFormValues,
  options: { today: DateOnlyString },
): Partial<Record<SnapshotFormField, string>> {
  const errors: Partial<Record<SnapshotFormField, string>> = {}
  const balance = validateBalanceText(values.balance)
  if (balance) errors.balance = balance
  if (!values.referenceDate || !parseDateOnly(values.referenceDate))
    errors.referenceDate = 'Informe a data de referência.'
  else if (values.referenceDate > options.today) errors.referenceDate = 'A data não pode ser futura.'
  return errors
}

export function buildSnapshotRequest(values: SnapshotFormValues): CreateBalanceSnapshotRequest {
  const cents = parseMoneyToCents(values.balance, { allowNegative: true })
  return { balance: fromCents(cents ?? 0), referenceDate: values.referenceDate ?? '' }
}
