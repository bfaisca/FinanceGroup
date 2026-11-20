import { describe, expect, it } from 'vitest'
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  formatPercent,
  parseDateOnly,
  ratioPercent,
  toDateOnly,
} from '../format'

// Intl usa espaço não separável entre "R$" e o número.
const normalize = (s: string) => s.replace(/ | /g, ' ')

describe('formatCurrency', () => {
  it('formata BRL com 2 casas e separadores pt-BR', () => {
    expect(normalize(formatCurrency(1234.5))).toBe('R$ 1.234,50')
    expect(normalize(formatCurrency(0.1 + 0.2))).toBe('R$ 0,30')
    expect(normalize(formatCurrency(1_000_000))).toBe('R$ 1.000.000,00')
  })

  it('formata valores negativos', () => {
    expect(normalize(formatCurrency(-10.5))).toBe('-R$ 10,50')
  })

  it('não exibe "-R$ 0,00" para zero negativo', () => {
    expect(normalize(formatCurrency(-0))).toBe('R$ 0,00')
    expect(normalize(formatCurrency(-0.001))).toBe('R$ 0,00')
  })

  it('retorna traço para valores ausentes ou inválidos', () => {
    expect(formatCurrency(null)).toBe('—')
    expect(formatCurrency(undefined)).toBe('—')
    expect(formatCurrency(Number.NaN)).toBe('—')
  })

  it('aceita outra moeda', () => {
    expect(normalize(formatCurrency(10, 'USD'))).toBe('US$ 10,00')
  })
})

describe('datas YYYY-MM-DD', () => {
  it('formata sem deslocamento de fuso', () => {
    expect(formatDate('2026-11-18')).toBe('18/11/2026')
    expect(formatDate('2026-01-01')).toBe('01/01/2026')
    expect(formatDate('2026-12-31')).toBe('31/12/2026')
  })

  it('parseDateOnly cria data local preservando o dia', () => {
    const date = parseDateOnly('2026-01-01')
    expect(date).not.toBeNull()
    expect(date?.getFullYear()).toBe(2026)
    expect(date?.getMonth()).toBe(0)
    expect(date?.getDate()).toBe(1)
  })

  it('rejeita datas inválidas', () => {
    expect(parseDateOnly('2026-02-30')).toBeNull()
    expect(parseDateOnly('18/11/2026')).toBeNull()
    expect(formatDate('2026-13-01')).toBe('—')
    expect(formatDate(null)).toBe('—')
  })

  it('toDateOnly usa o calendário local (ida e volta)', () => {
    expect(toDateOnly(new Date(2026, 10, 18, 23, 59))).toBe('2026-11-18')
    expect(toDateOnly(new Date(2026, 0, 1, 0, 0))).toBe('2026-01-01')
    const parsed = parseDateOnly('2026-03-09')
    expect(parsed && toDateOnly(parsed)).toBe('2026-03-09')
  })

  it('formatDateTime formata carimbo ISO e trata inválidos', () => {
    expect(formatDateTime('2026-11-18T22:40:00Z')).toMatch(/\d{2}\/\d{2}\/\d{4}/)
    expect(formatDateTime('invalid')).toBe('—')
    expect(formatDateTime(undefined)).toBe('—')
  })
})

describe('formatPercent / ratioPercent (Etapa 3)', () => {
  it('formata percentual com 1 casa em pt-BR', () => {
    expect(formatPercent(75)).toBe('75,0%')
    expect(formatPercent(33.333)).toBe('33,3%')
    expect(formatPercent(null)).toBe('—')
  })

  it('calcula proporção em centavos e ignora total não positivo', () => {
    expect(ratioPercent(1250, 5000)).toBe(25)
    expect(ratioPercent(0.1, 0.3)).toBeCloseTo(33.33, 1)
    expect(ratioPercent(10, 0)).toBeNull()
  })
})
