import { describe, expect, it } from 'vitest'
import type { RecurrenceFrequency } from '@/types/api'
import { frequencyLabel } from '../recurrence'

describe('frequencyLabel', () => {
  it.each<[RecurrenceFrequency, number, string]>([
    ['Daily', 1, 'Diária'],
    ['Weekly', 1, 'Semanal'],
    ['Biweekly', 1, 'Quinzenal'],
    ['Monthly', 1, 'Mensal'],
    ['Bimonthly', 1, 'Bimestral'],
    ['Quarterly', 1, 'Trimestral'],
    ['Semiannual', 1, 'Semestral'],
    ['Annual', 1, 'Anual'],
  ])('%s com intervalo %i = "%s"', (frequency, interval, expected) => {
    expect(frequencyLabel(frequency, interval)).toBe(expected)
  })

  it('intervalos maiores viram "A cada N …" na unidade correta', () => {
    expect(frequencyLabel('Weekly', 2)).toBe('A cada 2 semanas')
    expect(frequencyLabel('Daily', 3)).toBe('A cada 3 dias')
    expect(frequencyLabel('Monthly', 2)).toBe('A cada 2 meses')
    expect(frequencyLabel('Annual', 2)).toBe('A cada 2 anos')
    expect(frequencyLabel('Biweekly', 2)).toBe('A cada 4 semanas')
    expect(frequencyLabel('Bimonthly', 2)).toBe('A cada 4 meses')
    expect(frequencyLabel('Quarterly', 2)).toBe('A cada 6 meses')
    expect(frequencyLabel('Semiannual', 2)).toBe('A cada 12 meses')
  })

  it('Custom é sempre "a cada N dias"', () => {
    expect(frequencyLabel('Custom', 10)).toBe('A cada 10 dias')
    expect(frequencyLabel('Custom', 1)).toBe('A cada 1 dia')
  })

  it('intervalo inválido cai em 1', () => {
    expect(frequencyLabel('Monthly', 0)).toBe('Mensal')
    expect(frequencyLabel('Custom', Number.NaN)).toBe('A cada 1 dia')
  })
})
