import type { DateOnlyString } from '@/types/api'
import { formatCurrency, formatDate, parseDateOnly, toDateOnly } from '@/utils/format'
import type { GroupRecurrenceFrequency } from './types'

export const RECURRENCE_MIN = 2
export const RECURRENCE_MAX = 60

/** Frequências da recorrência de grupo (D-049): sem diária nem personalizada. */
export const RECURRENCE_FREQUENCY_LABELS: Record<GroupRecurrenceFrequency, string> = {
  Weekly: 'Semanal',
  Biweekly: 'Quinzenal',
  Monthly: 'Mensal',
  Bimonthly: 'Bimestral',
  Quarterly: 'Trimestral',
  Semiannual: 'Semestral',
  Annual: 'Anual',
}

/** Adjetivo no plural para o resumo ("mensais"). */
const FREQUENCY_PLURAL: Record<GroupRecurrenceFrequency, string> = {
  Weekly: 'semanais',
  Biweekly: 'quinzenais',
  Monthly: 'mensais',
  Bimonthly: 'bimestrais',
  Quarterly: 'trimestrais',
  Semiannual: 'semestrais',
  Annual: 'anuais',
}

export const DEFAULT_RECURRENCE_FREQUENCY: GroupRecurrenceFrequency = 'Monthly'

const STEP_DAYS: Partial<Record<GroupRecurrenceFrequency, number>> = { Weekly: 7, Biweekly: 14 }
const STEP_MONTHS: Partial<Record<GroupRecurrenceFrequency, number>> = {
  Monthly: 1,
  Bimonthly: 2,
  Quarterly: 3,
  Semiannual: 6,
  Annual: 12,
}

function lastDayOfMonth(year: number, monthIndex: number): number {
  return new Date(year, monthIndex + 1, 0).getDate()
}

/**
 * Data da i-ésima ocorrência (0 = a primeira), a partir da data da movimentação. Em frequências por mês,
 * o dia é ajustado ao fim do mês sem deriva (31/jan → 28/fev → 31/mar). SOMENTE PRÉVIA visual: o backend
 * é a fonte da verdade das datas (D-049).
 */
export function occurrenceDate(start: DateOnlyString, frequency: GroupRecurrenceFrequency, index: number) {
  const base = parseDateOnly(start)
  if (!base) return null
  const days = STEP_DAYS[frequency]
  if (days !== undefined) {
    return toDateOnly(new Date(base.getFullYear(), base.getMonth(), base.getDate() + days * index))
  }
  const months = (STEP_MONTHS[frequency] ?? 1) * index
  const total = base.getMonth() + months
  const year = base.getFullYear() + Math.floor(total / 12)
  const monthIndex = ((total % 12) + 12) % 12
  const day = Math.min(base.getDate(), lastDayOfMonth(year, monthIndex))
  return toDateOnly(new Date(year, monthIndex, day))
}

export interface RecurrencePreview {
  first: DateOnlyString
  last: DateOnlyString
  count: number
  /** Total em centavos (valor × quantidade). */
  totalCents: number
  summary: string
}

/** Resumo legível da série: "10 contas de R$ 500,00, mensais, de 10/01/2026 a 10/10/2026 — total R$ 5.000,00". */
export function recurrencePreview(
  start: DateOnlyString | null,
  frequency: GroupRecurrenceFrequency,
  count: number,
  amountCents: number | null,
): RecurrencePreview | null {
  if (!start || !Number.isInteger(count) || count < RECURRENCE_MIN || count > RECURRENCE_MAX) return null
  if (amountCents === null || amountCents <= 0) return null
  const first = occurrenceDate(start, frequency, 0)
  const last = occurrenceDate(start, frequency, count - 1)
  if (!first || !last) return null
  const totalCents = amountCents * count
  const summary =
    `${count} contas de ${formatCurrency(amountCents / 100)}, ${FREQUENCY_PLURAL[frequency]}, ` +
    `de ${formatDate(first)} a ${formatDate(last)} — total ${formatCurrency(totalCents / 100)}`
  return { first, last, count, totalCents, summary }
}

/** Selo "3/10" da ocorrência na série. */
export function seriesBadge(series: { number: number; total: number }): string {
  return `${series.number}/${series.total}`
}
