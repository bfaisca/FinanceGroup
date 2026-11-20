import type { RecurrenceFrequency } from '@/types/api'
import { FREQUENCY_OPTION_LABELS } from './labels'

function plural(n: number, singular: string, pluralForm: string): string {
  return `${n} ${n === 1 ? singular : pluralForm}`
}

/**
 * Rótulo legível da frequência a partir de `frequency` + `interval` (docs D-027).
 * Apenas apresentação: a regra de geração das ocorrências pertence ao backend.
 * Ex.: Monthly/1 → "Mensal"; Weekly/2 → "A cada 2 semanas"; Custom/10 → "A cada 10 dias".
 */
export function frequencyLabel(frequency: RecurrenceFrequency, interval: number): string {
  const n = Number.isInteger(interval) && interval > 0 ? interval : 1
  const every = (count: number, singular: string, pluralForm: string) => `A cada ${plural(count, singular, pluralForm)}`

  switch (frequency) {
    case 'Daily':
      return n === 1 ? 'Diária' : every(n, 'dia', 'dias')
    case 'Weekly':
      return n === 1 ? 'Semanal' : every(n, 'semana', 'semanas')
    case 'Biweekly':
      return n === 1 ? 'Quinzenal' : every(n * 2, 'semana', 'semanas')
    case 'Monthly':
      return n === 1 ? 'Mensal' : every(n, 'mês', 'meses')
    case 'Bimonthly':
      return n === 1 ? 'Bimestral' : every(n * 2, 'mês', 'meses')
    case 'Quarterly':
      return n === 1 ? 'Trimestral' : every(n * 3, 'mês', 'meses')
    case 'Semiannual':
      return n === 1 ? 'Semestral' : every(n * 6, 'mês', 'meses')
    case 'Annual':
      return n === 1 ? 'Anual' : every(n, 'ano', 'anos')
    case 'Custom':
      return every(n, 'dia', 'dias')
    default:
      return FREQUENCY_OPTION_LABELS[frequency as RecurrenceFrequency] ?? String(frequency)
  }
}
