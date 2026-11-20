import type { CreditCardDto, CreditCardRequest } from '@/types/api'
import { formatMoneyInput, fromCents, MAX_CENTS, parseMoneyToCents, toCents } from '@/utils/money'

export const CARD_NAME_MAX = 100
export const CARD_INSTITUTION_MAX = 100
export const DAY_MIN = 1
export const DAY_MAX = 31

export interface CreditCardFormValues {
  name: string
  institution: string
  /** Texto pt-BR. */
  creditLimit: string
  closingDay: number | null
  dueDay: number | null
  accountId: string | null
}

export const CREDIT_CARD_FORM_FIELDS = [
  'name',
  'institution',
  'creditLimit',
  'closingDay',
  'dueDay',
  'accountId',
] as const
export type CreditCardFormField = (typeof CREDIT_CARD_FORM_FIELDS)[number]

export function emptyCreditCardForm(): CreditCardFormValues {
  return { name: '', institution: '', creditLimit: '', closingDay: null, dueDay: null, accountId: null }
}

export function formValuesFromCard(card: CreditCardDto): CreditCardFormValues {
  return {
    name: card.name,
    institution: card.institution ?? '',
    creditLimit: formatMoneyInput(toCents(card.creditLimit)),
    closingDay: card.closingDay,
    dueDay: card.dueDay,
    accountId: card.accountId,
  }
}

/** Dia 1–31 inteiro. Retorna a mensagem de erro ou `undefined`. */
export function validateDayOfMonth(value: number | null, label: string): string | undefined {
  if (value === null || value === undefined) return `Informe o dia de ${label}.`
  if (!Number.isInteger(value) || value < DAY_MIN || value > DAY_MAX) {
    return `O dia de ${label} deve ser um número inteiro de ${DAY_MIN} a ${DAY_MAX}.`
  }
  return undefined
}

/** Validação de UX espelhando docs/API_CONTRACTS.md §11; o backend é a fonte definitiva. */
export function validateCreditCardForm(values: CreditCardFormValues): Partial<Record<CreditCardFormField, string>> {
  const errors: Partial<Record<CreditCardFormField, string>> = {}

  const name = values.name.trim()
  if (name.length === 0) errors.name = 'Informe o nome do cartão.'
  else if (name.length > CARD_NAME_MAX) errors.name = `O nome deve ter até ${CARD_NAME_MAX} caracteres.`

  if (values.institution.trim().length > CARD_INSTITUTION_MAX) {
    errors.institution = `A instituição deve ter até ${CARD_INSTITUTION_MAX} caracteres.`
  }

  if (values.creditLimit.trim().length === 0) {
    errors.creditLimit = 'Informe o limite do cartão (pode ser zero).'
  } else {
    const cents = parseMoneyToCents(values.creditLimit)
    if (cents === null) errors.creditLimit = 'Informe um valor válido, com até 2 casas decimais.'
    else if (cents < 0) errors.creditLimit = 'O limite não pode ser negativo.'
    else if (cents > MAX_CENTS) errors.creditLimit = 'Valor acima do limite permitido.'
  }

  const closing = validateDayOfMonth(values.closingDay, 'fechamento')
  if (closing) errors.closingDay = closing
  const due = validateDayOfMonth(values.dueDay, 'vencimento')
  if (due) errors.dueDay = due

  return errors
}

/** POST e PUT têm o mesmo corpo (§11). */
export function buildCreditCardRequest(values: CreditCardFormValues): CreditCardRequest {
  const cents = parseMoneyToCents(values.creditLimit)
  return {
    name: values.name.trim(),
    institution: values.institution.trim() || null,
    creditLimit: fromCents(cents ?? 0),
    closingDay: values.closingDay ?? 0,
    dueDay: values.dueDay ?? 0,
    accountId: values.accountId,
  }
}
