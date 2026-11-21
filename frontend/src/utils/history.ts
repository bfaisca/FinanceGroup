import type { TransactionHistoryEntryDto, TransactionStatus } from '@/types/api'
import { formatCurrency, formatDate } from './format'
import { HISTORY_ACTION_LABELS, PAYMENT_METHOD_LABELS, STATUS_PRESENTATION, TRANSACTION_TYPE_LABELS } from './labels'

const FIELD_LABELS: Record<string, string> = {
  description: 'Descrição',
  amount: 'Valor',
  type: 'Tipo',
  status: 'Situação',
  categoryId: 'Categoria',
  category: 'Categoria',
  accountId: 'Conta',
  account: 'Conta',
  paymentMethod: 'Método de pagamento',
  transactionDate: 'Data da movimentação',
  dueDate: 'Vencimento',
  paidAt: 'Data de quitação',
  interest: 'Juros/multa',
  notes: 'Observações',
}

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/
const ISO_DATE_TIME = /^\d{4}-\d{2}-\d{2}T/
const NUMERIC = /^-?\d+(?:\.\d+)?$/
// O backend registra pagamentos como "200.00 (2026-11-18)" (valor e data do pagamento); com juros/multa
// (D-048) o valor pode vir como "200.00 + 23.50 (2026-11-18)" — o formato exato não está no contrato,
// então qualquer outro texto é exibido como veio.
const PAYMENT_VALUE = /^(-?\d+(?:\.\d+)?)(?: \+ (\d+(?:\.\d+)?))? \((\d{4}-\d{2}-\d{2})\)$/

function formatPaymentValue(raw: string): string {
  const match = PAYMENT_VALUE.exec(raw)
  if (!match) return raw
  const interest = match[2] ? ` + ${formatCurrency(Number(match[2]))} de juros/multa` : ''
  return `${formatCurrency(Number(match[1]))}${interest} em ${formatDate(match[3])}`
}

/** Resolvedores opcionais de identificadores (ex.: id de categoria → nome). */
export interface HistoryResolvers {
  category?: (id: string) => string | undefined
  account?: (id: string) => string | undefined
}

export function historyFieldLabel(field: string): string {
  return FIELD_LABELS[field] ?? field
}

/** Converte um valor bruto do histórico em texto legível, conforme o campo. */
export function formatHistoryValue(
  field: string | null,
  value: string | null,
  resolvers: HistoryResolvers = {},
): string {
  if (value === null || value === '') return 'vazio'
  if (DATE_ONLY.test(value)) return formatDate(value)
  if (ISO_DATE_TIME.test(value)) return formatDate(value.slice(0, 10))

  switch (field) {
    case 'amount':
      return NUMERIC.test(value) ? formatCurrency(Number(value)) : value
    case 'status':
      return STATUS_PRESENTATION[value as TransactionStatus]?.label ?? value
    case 'type':
      return TRANSACTION_TYPE_LABELS[value as keyof typeof TRANSACTION_TYPE_LABELS] ?? value
    case 'paymentMethod':
      return PAYMENT_METHOD_LABELS[value as keyof typeof PAYMENT_METHOD_LABELS] ?? value
    case 'categoryId':
      return resolvers.category?.(value) ?? value
    case 'accountId':
      return resolvers.account?.(value) ?? value
    default:
      return value
  }
}

/** Frase legível de uma linha do histórico. */
export function describeHistoryEntry(entry: TransactionHistoryEntryDto, resolvers: HistoryResolvers = {}): string {
  const title = HISTORY_ACTION_LABELS[entry.action] ?? entry.action
  if (!entry.field) {
    return entry.newValue ? `${title}: ${formatHistoryValue(null, entry.newValue, resolvers)}` : title
  }

  if (entry.field === 'payment') {
    if (entry.action === 'PaymentUpdated' && entry.oldValue && entry.newValue) {
      return `${title}: de ${formatPaymentValue(entry.oldValue)} para ${formatPaymentValue(entry.newValue)}`
    }
    const raw = entry.newValue ?? entry.oldValue ?? ''
    return PAYMENT_VALUE.test(raw) ? `${title}: ${formatPaymentValue(raw)}` : title
  }

  const label = historyFieldLabel(entry.field)
  if (entry.action === 'Updated') {
    const from = formatHistoryValue(entry.field, entry.oldValue, resolvers)
    const to = formatHistoryValue(entry.field, entry.newValue, resolvers)
    return `${label}: de ${from} para ${to}`
  }

  const value = entry.newValue ?? entry.oldValue
  return `${title} (${label}: ${formatHistoryValue(entry.field, value, resolvers)})`
}
