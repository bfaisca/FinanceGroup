import type { DateOnlyString, PaymentMethod, TransactionInitialStatus, TransactionType } from '@/types/api'
import { formatCurrency, parseDateOnly } from '@/utils/format'
import { fromCents, MAX_CENTS, parseMoneyToCents } from '@/utils/money'
import { DESCRIPTION_MAX, NOTES_MAX } from '@/modules/transactions/validation'
import type { CreateGroupTransactionRequest, GroupRequest, UpdateGroupTransactionRequest } from './types'

export const GROUP_NAME_MAX = 100
export const GROUP_DESCRIPTION_MAX = 500

// ---------- Grupo ----------

export interface GroupFormValues {
  name: string
  description: string
}

export const GROUP_FORM_FIELDS = ['name', 'description'] as const
export type GroupFormField = (typeof GROUP_FORM_FIELDS)[number]

/** Espelha §16: `name` 1–100, `description` ≤ 500. O backend continua sendo a fonte definitiva. */
export function validateGroupForm(values: GroupFormValues): Partial<Record<GroupFormField, string>> {
  const errors: Partial<Record<GroupFormField, string>> = {}
  const name = values.name.trim()
  if (name.length === 0) errors.name = 'Informe o nome do grupo.'
  else if (name.length > GROUP_NAME_MAX) errors.name = `O nome deve ter até ${GROUP_NAME_MAX} caracteres.`
  if (values.description.length > GROUP_DESCRIPTION_MAX) {
    errors.description = `A descrição deve ter até ${GROUP_DESCRIPTION_MAX} caracteres.`
  }
  return errors
}

export function buildGroupRequest(values: GroupFormValues): GroupRequest {
  return { name: values.name.trim(), description: values.description.trim() || null }
}

// ---------- Movimentação do grupo (sem conta/cartão: nada privado) ----------

export interface GroupTransactionFormValues {
  description: string
  type: TransactionType
  /** Texto digitado (pt-BR). */
  amount: string
  categoryId: string | null
  paymentMethod: PaymentMethod | null
  /** `null` na edição = manter a situação atual. */
  status: TransactionInitialStatus | null
  transactionDate: DateOnlyString | null
  dueDate: DateOnlyString | null
  notes: string
}

export const GROUP_TRANSACTION_FORM_FIELDS = [
  'description',
  'amount',
  'categoryId',
  'paymentMethod',
  'status',
  'transactionDate',
  'dueDate',
  'notes',
] as const
export type GroupTransactionFormField = (typeof GROUP_TRANSACTION_FORM_FIELDS)[number]

/** Espelha §8/§16.2. `minAmountCents` = total já pago (o valor não pode ficar abaixo dele). */
export function validateGroupTransactionForm(
  values: GroupTransactionFormValues,
  options: { minAmountCents?: number } = {},
): Partial<Record<GroupTransactionFormField, string>> {
  const errors: Partial<Record<GroupTransactionFormField, string>> = {}

  const description = values.description.trim()
  if (description.length === 0) errors.description = 'Informe a descrição.'
  else if (description.length > DESCRIPTION_MAX) {
    errors.description = `A descrição deve ter até ${DESCRIPTION_MAX} caracteres.`
  }

  if (values.amount.trim().length === 0) {
    errors.amount = 'Informe o valor.'
  } else {
    const cents = parseMoneyToCents(values.amount)
    if (cents === null) errors.amount = 'Informe um valor válido, com até 2 casas decimais.'
    else if (cents <= 0) errors.amount = 'O valor deve ser maior que zero.'
    else if (cents > MAX_CENTS) errors.amount = 'Valor acima do limite permitido.'
    else if (options.minAmountCents && cents < options.minAmountCents) {
      errors.amount = `O valor não pode ser menor que o já pago (${formatCurrency(fromCents(options.minAmountCents))}).`
    }
  }

  if (!values.categoryId) errors.categoryId = 'Escolha uma categoria.'

  if (!values.transactionDate || !parseDateOnly(values.transactionDate)) {
    errors.transactionDate = 'Informe a data da movimentação.'
  }
  if (values.dueDate) {
    if (!parseDateOnly(values.dueDate)) errors.dueDate = 'Informe um vencimento válido.'
    else if (values.transactionDate && values.dueDate < values.transactionDate) {
      errors.dueDate = 'O vencimento deve ser igual ou posterior à data da movimentação.'
    }
  }

  if (values.notes.length > NOTES_MAX) errors.notes = `As observações devem ter até ${NOTES_MAX} caracteres.`
  return errors
}

function sharedFields(values: GroupTransactionFormValues) {
  const cents = parseMoneyToCents(values.amount)
  return {
    description: values.description.trim(),
    amount: fromCents(cents ?? 0),
    categoryId: values.categoryId ?? '',
    paymentMethod: values.paymentMethod,
    transactionDate: values.transactionDate ?? '',
    dueDate: values.dueDate,
    notes: values.notes.trim() || null,
  }
}

export function buildGroupCreateRequest(values: GroupTransactionFormValues): CreateGroupTransactionRequest {
  return { ...sharedFields(values), type: values.type, status: values.status ?? 'Pending' }
}

/** `type` é imutável: não é enviado. `status` só vai se o usuário escolheu um. */
export function buildGroupUpdateRequest(values: GroupTransactionFormValues): UpdateGroupTransactionRequest {
  const request: UpdateGroupTransactionRequest = sharedFields(values)
  if (values.status) request.status = values.status
  return request
}

// ---------- Convite ----------

/** Mensagem do `409` ao convidar quem já tem convite pendente (§16.1). */
export const INVITE_CONFLICT_MESSAGE =
  'Já existe um convite pendente para este e-mail. Reenvie ou revogue o convite existente.'
