import type { DateOnlyString, IsoDateTimeString } from '@/types/api'

const LOCALE = 'pt-BR'

const currencyFormatters = new Map<string, Intl.NumberFormat>()

function currencyFormatter(currency: string): Intl.NumberFormat {
  let formatter = currencyFormatters.get(currency)
  if (!formatter) {
    formatter = new Intl.NumberFormat(LOCALE, {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })
    currencyFormatters.set(currency, formatter)
  }
  return formatter
}

/** Formata valor monetário (padrão BRL) — ex.: 1234.5 → "R$ 1.234,50". */
export function formatCurrency(value: number | null | undefined, currency = 'BRL'): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—'
  // Evita "-R$ 0,00" para -0 ou valores que arredondam para zero.
  const normalized = Math.abs(value) < 0.005 ? 0 : value
  return currencyFormatter(currency).format(normalized)
}

const DATE_ONLY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/

/**
 * Converte "YYYY-MM-DD" em `Date` local (meia-noite local), sem deslocamento de fuso.
 * `new Date("2026-11-18")` seria interpretado como UTC e poderia exibir o dia anterior.
 */
export function parseDateOnly(value: DateOnlyString): Date | null {
  const match = DATE_ONLY_PATTERN.exec(value)
  if (!match) return null
  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])
  const date = new Date(year, month - 1, day)
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return null
  return date
}

/** Converte `Date` em "YYYY-MM-DD" usando o calendário local (sem `toISOString`, que usa UTC). */
export function toDateOnly(date: Date): DateOnlyString {
  const y = String(date.getFullYear()).padStart(4, '0')
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/** Formata data financeira "YYYY-MM-DD" → "18/11/2026". */
export function formatDate(value: DateOnlyString | null | undefined): string {
  if (!value) return '—'
  const match = DATE_ONLY_PATTERN.exec(value)
  if (!match || !parseDateOnly(value)) return '—'
  return `${match[3]}/${match[2]}/${match[1]}`
}

/** Formata carimbo de tempo ISO UTC no fuso do navegador (D-011) — ex.: "18/11/2026 19:40". */
export function formatDateTime(value: IsoDateTimeString | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat(LOCALE, { dateStyle: 'short', timeStyle: 'short' }).format(date)
}
