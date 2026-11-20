import type {
  CreatePaymentRequest,
  CreateTransactionRequest,
  DateOnlyString,
  PaymentMethod,
  TransactionInitialStatus,
  TransactionType,
  UpdateTransactionRequest,
} from '@/types/api'
import { formatCurrency, parseDateOnly } from '@/utils/format'
import { formatMoneyInput, fromCents, MAX_CENTS, parseMoneyToCents } from '@/utils/money'

export const DESCRIPTION_MAX = 200
export const NOTES_MAX = 1000

export interface TransactionFormValues {
  description: string
  type: TransactionType
  /** Texto digitado (pt-BR). */
  amount: string
  categoryId: string | null
  accountId: string | null
  paymentMethod: PaymentMethod | null
  /** Etapa 3: "Pago com" — conta/dinheiro ou cartão de crédito (só despesas). */
  payWith: 'account' | 'card'
  creditCardId: string | null
  /** `null` na edição = manter a situação atual. */
  status: TransactionInitialStatus | null
  transactionDate: DateOnlyString | null
  dueDate: DateOnlyString | null
  notes: string
}

export const TRANSACTION_FORM_FIELDS = [
  'description',
  'amount',
  'categoryId',
  'accountId',
  'creditCardId',
  'paymentMethod',
  'status',
  'transactionDate',
  'dueDate',
  'notes',
] as const

export type TransactionFormField = (typeof TRANSACTION_FORM_FIELDS)[number]

/**
 * Validação de UX espelhando docs/API_CONTRACTS.md §8. O backend continua sendo a fonte definitiva.
 * `minAmountCents` = total já pago (o valor não pode ficar abaixo dele — D-023).
 */
export function validateTransactionForm(
  values: TransactionFormValues,
  options: { minAmountCents?: number } = {},
): Partial<Record<TransactionFormField, string>> {
  const errors: Partial<Record<TransactionFormField, string>> = {}

  const description = values.description.trim()
  if (description.length === 0) errors.description = 'Informe a descrição.'
  else if (description.length > DESCRIPTION_MAX)
    errors.description = `A descrição deve ter até ${DESCRIPTION_MAX} caracteres.`

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

  const withCard = usesCard(values)
  if (withCard && !values.creditCardId) errors.creditCardId = 'Escolha o cartão de crédito.'

  if (!values.transactionDate || !parseDateOnly(values.transactionDate)) {
    errors.transactionDate = 'Informe a data da movimentação.'
  }
  // Com cartão o vencimento é calculado pela fatura (backend): não é digitado nem validado aqui.
  if (!withCard && values.dueDate) {
    if (!parseDateOnly(values.dueDate)) errors.dueDate = 'Informe um vencimento válido.'
    else if (values.transactionDate && values.dueDate < values.transactionDate) {
      errors.dueDate = 'O vencimento deve ser igual ou posterior à data da movimentação.'
    }
  }

  if (values.notes.length > NOTES_MAX) errors.notes = `As observações devem ter até ${NOTES_MAX} caracteres.`

  return errors
}

/** Compra no cartão: só despesas (§10). */
export function usesCard(values: Pick<TransactionFormValues, 'type' | 'payWith'>): boolean {
  return values.type === 'Expense' && values.payWith === 'card'
}

function sharedFields(values: TransactionFormValues) {
  const cents = parseMoneyToCents(values.amount)
  const common = {
    description: values.description.trim(),
    amount: fromCents(cents ?? 0),
    categoryId: values.categoryId ?? '',
    transactionDate: values.transactionDate ?? '',
    notes: values.notes.trim() || null,
  }
  if (usesCard(values)) {
    // §10/D-025: com cartão `accountId` e `dueDate` NÃO podem ser enviados; método fixo `CreditCard`.
    return { ...common, creditCardId: values.creditCardId, paymentMethod: 'CreditCard' as const }
  }
  return {
    ...common,
    accountId: values.accountId,
    paymentMethod: values.paymentMethod,
    dueDate: values.dueDate,
  }
}

export function buildCreateRequest(values: TransactionFormValues): CreateTransactionRequest {
  return { ...sharedFields(values), type: values.type, status: values.status ?? 'Pending' }
}

/** `type` é imutável: não é enviado. `status` só vai se o usuário escolheu um. */
export function buildUpdateRequest(values: TransactionFormValues): UpdateTransactionRequest {
  const request: UpdateTransactionRequest = sharedFields(values)
  if (values.status) request.status = values.status
  return request
}

// ---------- Pagamento ----------

export interface PaymentFormValues {
  amount: string
  paymentDate: DateOnlyString | null
  accountId: string | null
  paymentMethod: PaymentMethod | null
}

export const PAYMENT_FORM_FIELDS = ['amount', 'paymentDate', 'accountId', 'paymentMethod'] as const
export type PaymentFormField = (typeof PAYMENT_FORM_FIELDS)[number]

/** Valor padrão do pagamento: o restante (em texto pt-BR). */
export function defaultPaymentAmountText(remainingAmount: number): string {
  return formatMoneyInput(Math.round(remainingAmount * 100))
}

/** `remainingCents`: o limite do pagamento é UX; o backend valida (`422`). `today`: data máxima. */
export function validatePaymentForm(
  values: PaymentFormValues,
  options: { remainingCents: number; today: DateOnlyString },
): Partial<Record<PaymentFormField, string>> {
  const errors: Partial<Record<PaymentFormField, string>> = {}

  if (values.amount.trim().length === 0) {
    errors.amount = 'Informe o valor.'
  } else {
    const cents = parseMoneyToCents(values.amount)
    if (cents === null) errors.amount = 'Informe um valor válido, com até 2 casas decimais.'
    else if (cents <= 0) errors.amount = 'O valor deve ser maior que zero.'
    else if (cents > options.remainingCents) {
      errors.amount = `O valor não pode ser maior que o restante (${formatCurrency(fromCents(options.remainingCents))}).`
    }
  }

  if (!values.paymentDate || !parseDateOnly(values.paymentDate)) errors.paymentDate = 'Informe a data.'
  else if (values.paymentDate > options.today) errors.paymentDate = 'A data não pode ser futura.'

  return errors
}

export function buildPaymentRequest(values: PaymentFormValues): CreatePaymentRequest {
  const cents = parseMoneyToCents(values.amount)
  return {
    amount: fromCents(cents ?? 0),
    paymentDate: values.paymentDate ?? '',
    accountId: values.accountId,
    paymentMethod: values.paymentMethod,
  }
}
