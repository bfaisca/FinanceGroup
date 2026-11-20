export type ReportTab = 'summary' | 'categories' | 'accounts' | 'methods' | 'evolution' | 'installments' | 'recurrences'

export const REPORT_TABS: readonly { value: ReportTab; label: string }[] = [
  { value: 'summary', label: 'Resumo' },
  { value: 'categories', label: 'Por categoria' },
  { value: 'accounts', label: 'Por conta' },
  { value: 'methods', label: 'Por método de pagamento' },
  { value: 'evolution', label: 'Evolução mensal' },
  { value: 'installments', label: 'Parcelamentos' },
  { value: 'recurrences', label: 'Recorrências' },
]

/** Abas que dependem do filtro de período (as demais usam janela de meses ou nada). */
export const PERIOD_TABS: readonly ReportTab[] = ['summary', 'categories', 'accounts', 'methods']
/** Abas com janela em meses. */
export const MONTHS_TABS: readonly ReportTab[] = ['evolution', 'installments']

export const MONTHS_OPTIONS = [6, 12, 24, 36] as const
export const DEFAULT_MONTHS = 12

export function isReportTab(value: unknown): value is ReportTab {
  return typeof value === 'string' && REPORT_TABS.some((t) => t.value === value)
}

export function parseMonths(value: unknown): number {
  const n = Number(value)
  return (MONTHS_OPTIONS as readonly number[]).includes(n) ? n : DEFAULT_MONTHS
}
