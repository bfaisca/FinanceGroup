import type { DateOnlyString, PaymentMethod, TransactionInitialStatus } from '@/types/api'
import { formatCurrency, parseDateOnly } from '@/utils/format'
import { fromCents, MAX_CENTS, parseMoneyToCents } from '@/utils/money'
import { DESCRIPTION_MAX, NOTES_MAX } from '@/modules/transactions/validation'
import { RECURRENCE_MAX, RECURRENCE_MIN } from './recurrence'
import type {
  CreateGroupTransactionRequest,
  GroupPaymentRequest,
  GroupRecurrenceFrequency,
  GroupRequest,
  UpdateGroupTransactionRequest,
} from './types'

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
  /** Texto digitado (pt-BR). */
  amount: string
  categoryId: string | null
  paymentMethod: PaymentMethod | null
  /** `null` na edição = manter a situação atual. */
  status: TransactionInitialStatus | null
  transactionDate: DateOnlyString | null
  /** Livre (D-047): qualquer data, inclusive passada e anterior à data da movimentação. */
  dueDate: DateOnlyString | null
  notes: string

  // ---- Só na criação ----
  /** "Já foi paga" (D-047). */
  paid: boolean
  paymentDate: DateOnlyString | null
  /** Vazio = valor da conta. */
  paymentAmount: string
  /** Vazio = sem juros/multa. */
  paymentInterest: string
  /** Conta privada do próprio criador (opcional). */
  paymentAccountId: string | null
  paymentMethodPaid: PaymentMethod | null
  /** "Repetir" (D-049). */
  repeat: boolean
  frequency: GroupRecurrenceFrequency
  /** Texto digitado (2–60). */
  occurrences: string
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
  'paymentDate',
  'paymentAmount',
  'paymentInterest',
  'paymentAccountId',
  'paymentMethodPaid',
  'frequency',
  'occurrences',
] as const
export type GroupTransactionFormField = (typeof GROUP_TRANSACTION_FORM_FIELDS)[number]

/** Chaves do backend (camelCase do request, com ponto) → campos do formulário. */
const SERVER_FIELD_ALIASES: Record<string, GroupTransactionFormField> = {
  'payment.amount': 'paymentAmount',
  'payment.interestAmount': 'paymentInterest',
  'payment.paymentDate': 'paymentDate',
  'payment.accountId': 'paymentAccountId',
  'payment.paymentMethod': 'paymentMethodPaid',
  'recurrence.frequency': 'frequency',
  'recurrence.occurrences': 'occurrences',
}

/** Traduz os `errors` do backend para os nomes de campo do formulário (mantém os demais). */
export function mapGroupServerErrors(fieldErrors: Record<string, string[]>): Record<string, string[]> {
  const mapped: Record<string, string[]> = {}
  for (const [key, messages] of Object.entries(fieldErrors)) mapped[SERVER_FIELD_ALIASES[key] ?? key] = messages
  return mapped
}

/** Espelha §8/§16.2. `minAmountCents` = principal já pago (o valor não pode ficar abaixo dele). */
export function validateGroupTransactionForm(
  values: GroupTransactionFormValues,
  options: { minAmountCents?: number; today?: DateOnlyString; creating?: boolean } = {},
): Partial<Record<GroupTransactionFormField, string>> {
  const errors: Partial<Record<GroupTransactionFormField, string>> = {}

  const description = values.description.trim()
  if (description.length === 0) errors.description = 'Informe a descrição.'
  else if (description.length > DESCRIPTION_MAX) {
    errors.description = `A descrição deve ter até ${DESCRIPTION_MAX} caracteres.`
  }

  let amountCents: number | null = null
  if (values.amount.trim().length === 0) {
    errors.amount = 'Informe o valor.'
  } else {
    const cents = parseMoneyToCents(values.amount)
    if (cents === null) errors.amount = 'Informe um valor válido, com até 2 casas decimais.'
    else if (cents <= 0) errors.amount = 'O valor deve ser maior que zero.'
    else if (cents > MAX_CENTS) errors.amount = 'Valor acima do limite permitido.'
    else if (options.minAmountCents && cents < options.minAmountCents) {
      errors.amount = `O valor não pode ser menor que o já pago (${formatCurrency(fromCents(options.minAmountCents))}).`
    } else amountCents = cents
  }

  if (!values.categoryId) errors.categoryId = 'Escolha uma categoria.'

  if (!values.transactionDate || !parseDateOnly(values.transactionDate)) {
    errors.transactionDate = 'Informe a data da movimentação.'
  }
  // Vencimento livre (D-047): só o formato é validado.
  if (values.dueDate && !parseDateOnly(values.dueDate)) errors.dueDate = 'Informe um vencimento válido.'

  if (values.notes.length > NOTES_MAX) errors.notes = `As observações devem ter até ${NOTES_MAX} caracteres.`

  if (options.creating) {
    if (values.paid) {
      if (!values.paymentDate || !parseDateOnly(values.paymentDate)) errors.paymentDate = 'Informe a data do pagamento.'
      else if (options.today && values.paymentDate > options.today) {
        errors.paymentDate = 'A data do pagamento não pode ser futura.'
      }

      if (values.paymentAmount.trim().length > 0) {
        const cents = parseMoneyToCents(values.paymentAmount)
        if (cents === null) errors.paymentAmount = 'Informe um valor válido, com até 2 casas decimais.'
        else if (cents <= 0) errors.paymentAmount = 'O valor pago deve ser maior que zero.'
        else if (amountCents !== null && cents > amountCents) {
          errors.paymentAmount = `O valor da conta paga não pode ser maior que o valor da conta (${formatCurrency(fromCents(amountCents))}).`
        }
      }
      if (values.paymentInterest.trim().length > 0) {
        const cents = parseMoneyToCents(values.paymentInterest)
        if (cents === null) errors.paymentInterest = 'Informe um valor válido, com até 2 casas decimais.'
        else if (cents > MAX_CENTS) errors.paymentInterest = 'Valor acima do limite permitido.'
      }
    }

    if (values.repeat) {
      const text = values.occurrences.trim()
      const count = /^\d+$/.test(text) ? Number(text) : Number.NaN
      if (!Number.isInteger(count) || count < RECURRENCE_MIN || count > RECURRENCE_MAX) {
        errors.occurrences = `Informe uma quantidade de ${RECURRENCE_MIN} a ${RECURRENCE_MAX} vezes.`
      }
    }
  }
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

/** Pagamento enviado junto da criação ("já paga"); valor padrão = valor da conta. */
export function buildInitialPayment(values: GroupTransactionFormValues): GroupPaymentRequest {
  const amountCents = parseMoneyToCents(values.amount) ?? 0
  const paidCents = values.paymentAmount.trim() ? (parseMoneyToCents(values.paymentAmount) ?? 0) : amountCents
  const interestCents = values.paymentInterest.trim() ? (parseMoneyToCents(values.paymentInterest) ?? 0) : 0
  return {
    amount: fromCents(paidCents),
    interestAmount: fromCents(interestCents),
    paymentDate: values.paymentDate ?? '',
    accountId: values.paymentAccountId,
    paymentMethod: values.paymentMethodPaid,
  }
}

export function buildGroupCreateRequest(values: GroupTransactionFormValues): CreateGroupTransactionRequest {
  const request: CreateGroupTransactionRequest = { ...sharedFields(values), status: values.status ?? 'Pending' }
  if (values.paid) request.payment = buildInitialPayment(values)
  if (values.repeat) {
    request.recurrence = { frequency: values.frequency, occurrences: Number(values.occurrences.trim()) }
  }
  return request
}

/** Edita só ESTA ocorrência: `payment`/`recurrence` nunca vão. `status` só se o usuário escolheu um. */
export function buildGroupUpdateRequest(values: GroupTransactionFormValues): UpdateGroupTransactionRequest {
  const request: UpdateGroupTransactionRequest = sharedFields(values)
  if (values.status) request.status = values.status
  return request
}

// ---------- Pagamento do grupo (valor da conta + juros/multa, D-048) ----------

export interface GroupPaymentFormValues {
  /** Principal (valor da conta). */
  amount: string
  /** Juros/multa; vazio = 0. */
  interest: string
  paymentDate: DateOnlyString | null
  accountId: string | null
  /** Só na edição: desvincular a conta privada atual (sem escolher outra). */
  clearAccount?: boolean
  paymentMethod: PaymentMethod | null
}

export const GROUP_PAYMENT_FORM_FIELDS = [
  'amount',
  'interestAmount',
  'paymentDate',
  'accountId',
  'paymentMethod',
] as const
export type GroupPaymentFormField = (typeof GROUP_PAYMENT_FORM_FIELDS)[number]

/** `remainingCents`: o que ainda cabe como principal (ao editar, já soma o próprio pagamento). */
export function validateGroupPaymentForm(
  values: GroupPaymentFormValues,
  options: { remainingCents: number; today: DateOnlyString },
): Partial<Record<GroupPaymentFormField, string>> {
  const errors: Partial<Record<GroupPaymentFormField, string>> = {}

  if (values.amount.trim().length === 0) {
    errors.amount = 'Informe o valor da conta.'
  } else {
    const cents = parseMoneyToCents(values.amount)
    if (cents === null) errors.amount = 'Informe um valor válido, com até 2 casas decimais.'
    else if (cents <= 0) errors.amount = 'O valor deve ser maior que zero.'
    else if (cents > options.remainingCents) {
      errors.amount = `O valor da conta não pode ser maior que o restante (${formatCurrency(fromCents(options.remainingCents))}).`
    }
  }

  if (values.interest.trim().length > 0) {
    const cents = parseMoneyToCents(values.interest, { allowNegative: true })
    if (cents === null) errors.interestAmount = 'Informe um valor válido, com até 2 casas decimais.'
    else if (cents < 0) errors.interestAmount = 'Os juros/multa não podem ser negativos.'
    else if (cents > MAX_CENTS) errors.interestAmount = 'Valor acima do limite permitido.'
  }

  if (!values.paymentDate || !parseDateOnly(values.paymentDate)) errors.paymentDate = 'Informe a data.'
  else if (values.paymentDate > options.today) errors.paymentDate = 'A data não pode ser futura.'

  return errors
}

export function buildGroupPaymentRequest(values: GroupPaymentFormValues): GroupPaymentRequest {
  const cents = parseMoneyToCents(values.amount)
  const interest = values.interest.trim() ? (parseMoneyToCents(values.interest) ?? 0) : 0
  return {
    amount: fromCents(cents ?? 0),
    interestAmount: fromCents(interest),
    paymentDate: values.paymentDate ?? '',
    accountId: values.accountId,
    ...(values.clearAccount && !values.accountId ? { clearAccount: true } : {}),
    paymentMethod: values.paymentMethod,
  }
}

// ---------- Convite ----------

/** Mensagem do `409` ao convidar quem já tem convite pendente (§16.1). */
export const INVITE_CONFLICT_MESSAGE =
  'Já existe um convite pendente para este e-mail. Reenvie ou revogue o convite existente.'
