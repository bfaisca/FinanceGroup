import { describe, expect, it } from 'vitest'
import { formatMoneyInput, fromCents, normalizeMoneyText, parseMoneyToCents, toCents } from '../money'

describe('parseMoneyToCents', () => {
  it.each([
    ['0', 0],
    ['12', 1200],
    ['12,5', 1250],
    ['12,50', 1250],
    ['1.234,56', 123456],
    ['1234,56', 123456],
    ['1.234', 123400],
    ['1.234.567,89', 123456789],
    ['12.5', 1250],
    ['12.50', 1250],
    ['R$ 1.000,00', 100000],
    ['0,01', 1],
  ])('interpreta %s como %i centavos', (text, cents) => {
    expect(parseMoneyToCents(text)).toBe(cents)
  })

  it.each(['', '  ', 'abc', '12,345', '1,2,3', '12,', ',50', '1..2', '12.345,6.7', '-5', '1e3'])(
    'rejeita %j',
    (text) => {
      expect(parseMoneyToCents(text)).toBeNull()
    },
  )

  it('aceita negativos apenas quando permitido', () => {
    expect(parseMoneyToCents('-1.500,25', { allowNegative: true })).toBe(-150025)
    expect(parseMoneyToCents('-1.500,25')).toBeNull()
  })

  it('rejeita valores acima do limite do contrato', () => {
    expect(parseMoneyToCents('9.999.999.999.999,99')).toBe(999_999_999_999_999)
    expect(parseMoneyToCents('10.000.000.000.000,00')).toBeNull()
  })

  it('evita erro de ponto flutuante (0,1 + 0,2)', () => {
    expect(parseMoneyToCents('0,10')! + parseMoneyToCents('0,20')!).toBe(30)
  })
})

describe('conversões', () => {
  it('formatMoneyInput agrupa milhares e usa vírgula', () => {
    expect(formatMoneyInput(123456)).toBe('1.234,56')
    expect(formatMoneyInput(5)).toBe('0,05')
    expect(formatMoneyInput(-150025)).toBe('-1.500,25')
  })

  it('toCents/fromCents são inversas para 2 casas', () => {
    expect(toCents(19.99)).toBe(1999)
    expect(toCents(1.005 * 100) / 100).toBeTypeOf('number')
    expect(fromCents(1999)).toBe(19.99)
    expect(toCents(0.29)).toBe(29)
  })

  it('normalizeMoneyText reformata o válido e preserva o inválido', () => {
    expect(normalizeMoneyText('1234,5')).toBe('1.234,50')
    expect(normalizeMoneyText('abc')).toBe('abc')
  })
})
