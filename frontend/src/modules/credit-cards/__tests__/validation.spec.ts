import { describe, expect, it } from 'vitest'
import { limitPresentation } from '../limit'
import {
  buildCreditCardRequest,
  emptyCreditCardForm,
  validateCreditCardForm,
  validateDayOfMonth,
  type CreditCardFormValues,
} from '../validation'

function values(partial: Partial<CreditCardFormValues> = {}): CreditCardFormValues {
  return { ...emptyCreditCardForm(), name: 'Roxinho', creditLimit: '5.000,00', closingDay: 5, dueDay: 12, ...partial }
}

describe('validateDayOfMonth', () => {
  it('aceita 1 a 31', () => {
    expect(validateDayOfMonth(1, 'fechamento')).toBeUndefined()
    expect(validateDayOfMonth(31, 'fechamento')).toBeUndefined()
  })

  it.each([0, 32, -1, 5.5])('rejeita %s', (day) => {
    expect(validateDayOfMonth(day, 'vencimento')).toContain('1 a 31')
  })

  it('exige o dia', () => {
    expect(validateDayOfMonth(null, 'fechamento')).toBe('Informe o dia de fechamento.')
  })
})

describe('validateCreditCardForm', () => {
  it('aceita um cartão válido e limite zero', () => {
    expect(validateCreditCardForm(values())).toEqual({})
    expect(validateCreditCardForm(values({ creditLimit: '0,00' }))).toEqual({})
  })

  it('exige nome, limite e os dois dias', () => {
    const errors = validateCreditCardForm(values({ name: ' ', creditLimit: '', closingDay: null, dueDay: null }))
    expect(errors.name).toBeDefined()
    expect(errors.creditLimit).toContain('limite')
    expect(errors.closingDay).toBeDefined()
    expect(errors.dueDay).toBeDefined()
  })

  it('rejeita limite negativo/inválido e textos acima do máximo', () => {
    expect(validateCreditCardForm(values({ creditLimit: '-10' })).creditLimit).toBeDefined()
    expect(validateCreditCardForm(values({ creditLimit: 'abc' })).creditLimit).toContain('válido')
    expect(validateCreditCardForm(values({ name: 'x'.repeat(101) })).name).toContain('100')
    expect(validateCreditCardForm(values({ institution: 'x'.repeat(101) })).institution).toContain('100')
  })
})

describe('buildCreditCardRequest', () => {
  it('converte o limite e normaliza instituição vazia para null', () => {
    expect(buildCreditCardRequest(values({ institution: '  ', accountId: 'a1' }))).toEqual({
      name: 'Roxinho',
      institution: null,
      creditLimit: 5000,
      closingDay: 5,
      dueDay: 12,
      accountId: 'a1',
    })
  })
})

describe('limitPresentation', () => {
  it('usa o percentual utilizado e limita a barra a 100', () => {
    expect(limitPresentation(1250, 5000, 3750)).toEqual({ percent: 25, barValue: 25, state: 'ok' })
    expect(limitPresentation(4500, 5000, 500).state).toBe('high')
  })

  it('limite excedido (disponível negativo) passa de 100% mas a barra fica em 100', () => {
    const result = limitPresentation(6000, 5000, -1000)
    expect(result.percent).toBe(120)
    expect(result.barValue).toBe(100)
    expect(result.state).toBe('exceeded')
  })

  it('limite zero: sem percentual; excedido se há uso', () => {
    expect(limitPresentation(0, 0, 0)).toEqual({ percent: null, barValue: 0, state: 'unknown' })
    expect(limitPresentation(10, 0, -10)).toMatchObject({ percent: null, barValue: 100, state: 'exceeded' })
  })
})
