import type { DateOnlyString } from '@/types/api'
import { parseDateOnly, toDateOnly } from './format'

export interface Period {
  from: DateOnlyString
  to: DateOnlyString
}

/** Intervalo máximo aceito pelo dashboard (contrato §9: ≤ 366 dias). */
export const MAX_PERIOD_DAYS = 366

/** Data de hoje no calendário local do navegador (fallback quando a API ainda não devolveu `today`). */
export function localToday(): DateOnlyString {
  return toDateOnly(new Date())
}

function utcDays(value: DateOnlyString): number {
  const date = parseDateOnly(value)
  if (!date) return Number.NaN
  return Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) / 86_400_000
}

/** Diferença em dias (`to - from`) entre duas datas "YYYY-MM-DD", sem influência de fuso/horário de verão. */
export function daysBetween(from: DateOnlyString, to: DateOnlyString): number {
  return utcDays(to) - utcDays(from)
}

function lastDayOfMonth(year: number, monthIndex: number): number {
  return new Date(year, monthIndex + 1, 0).getDate()
}

/** Primeiro e último dia do mês que contém `date`. */
export function monthPeriod(date: DateOnlyString): Period {
  const parsed = parseDateOnly(date) ?? new Date()
  const y = parsed.getFullYear()
  const m = parsed.getMonth()
  return { from: toDateOnly(new Date(y, m, 1)), to: toDateOnly(new Date(y, m, lastDayOfMonth(y, m))) }
}

/** Desloca um mês cheio `delta` meses (negativo = anterior). */
export function shiftMonth(period: Period, delta: number): Period {
  const parsed = parseDateOnly(period.from) ?? new Date()
  return monthPeriod(toDateOnly(new Date(parsed.getFullYear(), parsed.getMonth() + delta, 1)))
}

export function isFullMonth(period: Period): boolean {
  const month = monthPeriod(period.from)
  return month.from === period.from && month.to === period.to
}

/** Valida um período (UX; o backend decide). Retorna a mensagem de erro ou `undefined`. */
export function validatePeriod(period: Partial<Period>): string | undefined {
  if (!period.from || !period.to) return 'Informe as datas inicial e final.'
  if (!parseDateOnly(period.from) || !parseDateOnly(period.to)) return 'Informe datas válidas.'
  if (period.from > period.to) return 'A data inicial deve ser anterior ou igual à final.'
  if (daysBetween(period.from, period.to) > MAX_PERIOD_DAYS)
    return `O período deve ter no máximo ${MAX_PERIOD_DAYS} dias.`
  return undefined
}

const MONTH_FORMAT = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' })
const MONTH_SHORT_FORMAT = new Intl.DateTimeFormat('pt-BR', { month: 'short', year: '2-digit' })

/** "2026-11" → "novembro de 2026". */
export function formatMonthLong(month: string): string {
  const date = parseDateOnly(`${month}-01`)
  return date ? MONTH_FORMAT.format(date) : month
}

/** "2026-11" → "nov. de 26" (rótulo curto de eixo). */
export function formatMonthShort(month: string): string {
  const date = parseDateOnly(`${month}-01`)
  return date ? MONTH_SHORT_FORMAT.format(date) : month
}
