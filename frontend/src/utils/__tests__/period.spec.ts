import { describe, expect, it } from 'vitest'
import { daysBetween, formatMonthLong, isFullMonth, monthPeriod, shiftMonth, validatePeriod } from '../period'

describe('period', () => {
  it('monthPeriod devolve o primeiro e o último dia do mês', () => {
    expect(monthPeriod('2026-11-18')).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    expect(monthPeriod('2028-02-10')).toEqual({ from: '2028-02-01', to: '2028-02-29' })
  })

  it('shiftMonth navega entre meses cruzando o ano', () => {
    const nov = monthPeriod('2026-11-18')
    expect(shiftMonth(nov, 1)).toEqual({ from: '2026-12-01', to: '2026-12-31' })
    expect(shiftMonth(shiftMonth(nov, 1), 1)).toEqual({ from: '2027-01-01', to: '2027-01-31' })
    expect(shiftMonth(monthPeriod('2026-01-05'), -1)).toEqual({ from: '2025-12-01', to: '2025-12-31' })
  })

  it('isFullMonth identifica meses cheios', () => {
    expect(isFullMonth({ from: '2026-11-01', to: '2026-11-30' })).toBe(true)
    expect(isFullMonth({ from: '2026-11-01', to: '2026-11-15' })).toBe(false)
  })

  it('daysBetween não sofre com horário de verão', () => {
    expect(daysBetween('2026-11-01', '2026-11-30')).toBe(29)
    expect(daysBetween('2026-03-01', '2026-04-01')).toBe(31)
    expect(daysBetween('2026-11-10', '2026-11-08')).toBe(-2)
  })

  it('validatePeriod aplica as regras do contrato', () => {
    expect(validatePeriod({ from: '2026-11-01', to: '2026-11-30' })).toBeUndefined()
    expect(validatePeriod({ from: '2026-11-01' })).toMatch(/Informe/)
    expect(validatePeriod({ from: '2026-11-30', to: '2026-11-01' })).toMatch(/anterior/)
    expect(validatePeriod({ from: '2024-01-01', to: '2026-01-01' })).toMatch(/366/)
  })

  it('formatMonthLong usa pt-BR', () => {
    expect(formatMonthLong('2026-11')).toBe('novembro de 2026')
  })
})
