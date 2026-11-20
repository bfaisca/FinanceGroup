import type {
  CreateRecurrenceRequest,
  DateOnlyString,
  PaymentMethod,
  RecurrenceDto,
  RecurrenceFrequency,
  TransactionType,
  UpdateRecurrenceRequest,
} from '@/types/api'
import { parseDateOnly } from '@/utils/format'
import { formatMoneyInput, fromCents, MAX_CENTS, parseMoneyToCents, toCents } from '@/utils/money'

export const RECURRENCE_DESCRIPTION_MAX = 200
export const RECURRENCE_NOTES_MAX = 1000
export const INTERVAL_MIN = 1
export const INTERVAL_MAX = 365

export interface RecurrenceFormValues {
  description: string
  type: TransactionType
  /** Texto pt-BR. */
  amount: string
  categoryId: string | null
  payWith: 'account' | 'card'
  creditCardId: string | null
  accountId: string | null
  paymentMethod: PaymentMethod | null
  notes: string
  frequency: RecurrenceFrequency
  interval: number | null
  startDate: DateOnlyString | null
  endDate: DateOnlyString | null
}

export const RECURRENCE_FORM_FIELDS = [
  'description',
  'amount',
  'categoryId',
  'creditCardId',
  'accountId',
  'paymentMethod',
  'notes',
  'frequency',
  'interval',
  'startDate',
  'endDate',
] as const
export type RecurrenceFormField = (typeof RECURRENCE_FORM_FIELDS)[number]

export function emptyRecurrenceForm(today: DateOnlyString): RecurrenceFormValues {
  return {
    description: '',
    type: 'Expense',
    amount: '',
    categoryId: null,
    payWith: 'account',
    creditCardId: null,
    accountId: null,
    paymentMethod: null,
    notes: '',
    frequency: 'Monthly',
    interval: 1,
    startDate: today,
    endDate: null,
  }
}

export function formValuesFromRecurrence(rule: RecurrenceDto): RecurrenceFormValues {
  return {
    description: rule.description,
    type: rule.type,
    amount: formatMoneyInput(toCents(rule.amount)),
    categoryId: rule.categoryId,
    payWith: rule.creditCardId ? 'card' : 'account',
    creditCardId: rule.creditCardId,
    accountId: rule.accountId,
    paymentMethod: rule.paymentMethod,
    notes: rule.notes ?? '',
    frequency: rule.frequency,
    interval: rule.interval,
    startDate: rule.startDate,
    endDate: rule.endDate,
  }
}

/** Cartão só vale para despesas (§13). */
export function usesCard(values: Pick<RecurrenceFormValues, 'type' | 'payWith'>): boolean {
  return values.type === 'Expense' && values.payWith === 'card'
}

/** Validação de UX espelhando docs/API_CONTRACTS.md §13; a geração das ocorrências é do backend (D-027). */
export function validateRecurrenceForm(
  values: RecurrenceFormValues,
  options: { editing: boolean },
): Partial<Record<RecurrenceFormField, string>> {
  const errors: Partial<Record<RecurrenceFormField, string>> = {}

  const description = values.description.trim()
  if (description.length === 0) errors.description = 'Informe a descrição.'
  else if (description.length > RECURRENCE_DESCRIPTION_MAX)
    errors.description = `A descrição deve ter até ${RECURRENCE_DESCRIPTION_MAX} caracteres.`

  if (values.amount.trim().length === 0) {
    errors.amount = 'Informe o valor.'
  } else {
    const cents = parseMoneyToCents(values.amount)
    if (cents === null) errors.amount = 'Informe um valor válido, com até 2 casas decimais.'
    else if (cents <= 0) errors.amount = 'O valor deve ser maior que zero.'
    else if (cents > MAX_CENTS) errors.amount = 'Valor acima do limite permitido.'
  }

  if (!values.categoryId) errors.categoryId = 'Escolha uma categoria.'
  if (usesCard(values) && !values.creditCardId) errors.creditCardId = 'Escolha o cartão de crédito.'

  const interval = values.interval
  if (interval === null || interval === undefined) {
    errors.interval = 'Informe o intervalo.'
  } else if (!Number.isInteger(interval) || interval < INTERVAL_MIN || interval > INTERVAL_MAX) {
    errors.interval = `O intervalo deve ser um inteiro de ${INTERVAL_MIN} a ${INTERVAL_MAX}.`
  }

  if (!options.editing) {
    if (!values.startDate || !parseDateOnly(values.startDate)) errors.startDate = 'Informe a data de início.'
  }
  if (values.endDate) {
    if (!parseDateOnly(values.endDate)) errors.endDate = 'Informe uma data de término válida.'
    else if (values.startDate && values.endDate < values.startDate)
      errors.endDate = 'O término deve ser igual ou posterior ao início.'
  }

  if (values.notes.length > RECURRENCE_NOTES_MAX)
    errors.notes = `As observações devem ter até ${RECURRENCE_NOTES_MAX} caracteres.`

  return errors
}

function sharedFields(values: RecurrenceFormValues) {
  const cents = parseMoneyToCents(values.amount)
  const common = {
    description: values.description.trim(),
    amount: fromCents(cents ?? 0),
    categoryId: values.categoryId ?? '',
    notes: values.notes.trim() || null,
    frequency: values.frequency,
    interval: values.interval ?? 1,
    endDate: values.endDate,
  }
  if (usesCard(values)) {
    // §13/§10: com cartão, sem `accountId`; método fixo `CreditCard`.
    return { ...common, creditCardId: values.creditCardId, paymentMethod: 'CreditCard' as const }
  }
  return { ...common, accountId: values.accountId, paymentMethod: values.paymentMethod }
}

export function buildCreateRecurrenceRequest(values: RecurrenceFormValues): CreateRecurrenceRequest {
  return { ...sharedFields(values), type: values.type, startDate: values.startDate ?? '' }
}

/** `type` e `startDate` são imutáveis: não são enviados. */
export function buildUpdateRecurrenceRequest(values: RecurrenceFormValues): UpdateRecurrenceRequest {
  return sharedFields(values)
}
