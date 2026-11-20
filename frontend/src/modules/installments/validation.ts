import type { CreateInstallmentPlanRequest, DateOnlyString, PaymentMethod } from '@/types/api'
import { parseDateOnly } from '@/utils/format'
import { fromCents, MAX_CENTS, parseMoneyToCents } from '@/utils/money'

export const PLAN_DESCRIPTION_MAX = 200
export const PLAN_NOTES_MAX = 1000
export const INSTALLMENT_COUNT_MIN = 2
export const INSTALLMENT_COUNT_MAX = 120

export interface InstallmentPlanFormValues {
  description: string
  /** Texto pt-BR. */
  totalAmount: string
  installmentCount: number | null
  firstDate: DateOnlyString | null
  categoryId: string | null
  payWith: 'account' | 'card'
  creditCardId: string | null
  accountId: string | null
  paymentMethod: PaymentMethod | null
  notes: string
}

export const PLAN_FORM_FIELDS = [
  'description',
  'totalAmount',
  'installmentCount',
  'firstDate',
  'categoryId',
  'creditCardId',
  'accountId',
  'paymentMethod',
  'notes',
] as const
export type PlanFormField = (typeof PLAN_FORM_FIELDS)[number]

export function emptyPlanForm(today: DateOnlyString): InstallmentPlanFormValues {
  return {
    description: '',
    totalAmount: '',
    installmentCount: null,
    firstDate: today,
    categoryId: null,
    payWith: 'account',
    creditCardId: null,
    accountId: null,
    paymentMethod: null,
    notes: '',
  }
}

/**
 * Validação de UX espelhando docs/API_CONTRACTS.md §12. Não calcula parcelas, datas nem centavos:
 * isso é do backend (D-026), e o resultado é exibido como devolvido pela API.
 */
export function validatePlanForm(values: InstallmentPlanFormValues): Partial<Record<PlanFormField, string>> {
  const errors: Partial<Record<PlanFormField, string>> = {}

  const description = values.description.trim()
  if (description.length === 0) errors.description = 'Informe a descrição.'
  else if (description.length > PLAN_DESCRIPTION_MAX)
    errors.description = `A descrição deve ter até ${PLAN_DESCRIPTION_MAX} caracteres.`

  if (values.totalAmount.trim().length === 0) {
    errors.totalAmount = 'Informe o valor total.'
  } else {
    const cents = parseMoneyToCents(values.totalAmount)
    if (cents === null) errors.totalAmount = 'Informe um valor válido, com até 2 casas decimais.'
    else if (cents <= 0) errors.totalAmount = 'O valor total deve ser maior que zero.'
    else if (cents > MAX_CENTS) errors.totalAmount = 'Valor acima do limite permitido.'
  }

  const count = values.installmentCount
  if (count === null || count === undefined) errors.installmentCount = 'Informe o número de parcelas.'
  else if (!Number.isInteger(count) || count < INSTALLMENT_COUNT_MIN || count > INSTALLMENT_COUNT_MAX) {
    errors.installmentCount = `O número de parcelas deve ser um inteiro de ${INSTALLMENT_COUNT_MIN} a ${INSTALLMENT_COUNT_MAX}.`
  }

  if (!values.firstDate || !parseDateOnly(values.firstDate)) errors.firstDate = 'Informe a data da primeira parcela.'
  if (!values.categoryId) errors.categoryId = 'Escolha uma categoria de despesa.'
  if (values.payWith === 'card' && !values.creditCardId) errors.creditCardId = 'Escolha o cartão de crédito.'
  if (values.notes.length > PLAN_NOTES_MAX) errors.notes = `As observações devem ter até ${PLAN_NOTES_MAX} caracteres.`

  return errors
}

export function buildPlanRequest(values: InstallmentPlanFormValues): CreateInstallmentPlanRequest {
  const cents = parseMoneyToCents(values.totalAmount)
  const base = {
    description: values.description.trim(),
    totalAmount: fromCents(cents ?? 0),
    installmentCount: values.installmentCount ?? 0,
    firstDate: values.firstDate ?? '',
    categoryId: values.categoryId ?? '',
    notes: values.notes.trim() || null,
  }
  if (values.payWith === 'card') {
    // §12: com cartão, sem `accountId`; método nulo ou `CreditCard`.
    return { ...base, creditCardId: values.creditCardId, paymentMethod: 'CreditCard' }
  }
  return { ...base, accountId: values.accountId, paymentMethod: values.paymentMethod }
}
