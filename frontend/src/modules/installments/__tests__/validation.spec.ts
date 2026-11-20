import { describe, expect, it } from 'vitest'
import { buildPlanRequest, emptyPlanForm, validatePlanForm, type InstallmentPlanFormValues } from '../validation'

function values(partial: Partial<InstallmentPlanFormValues> = {}): InstallmentPlanFormValues {
  return {
    ...emptyPlanForm('2026-11-18'),
    description: 'Notebook',
    totalAmount: '3.000,00',
    installmentCount: 12,
    categoryId: 'c1',
    ...partial,
  }
}

describe('validatePlanForm', () => {
  it('aceita um plano válido; a 1ª data vem de hoje', () => {
    expect(validatePlanForm(values())).toEqual({})
    expect(emptyPlanForm('2026-11-18').firstDate).toBe('2026-11-18')
  })

  it.each([2, 120])('aceita %i parcelas (limites do contrato)', (count) => {
    expect(validatePlanForm(values({ installmentCount: count })).installmentCount).toBeUndefined()
  })

  it.each([1, 0, 121, 3.5, -4])('rejeita %s parcelas', (count) => {
    expect(validatePlanForm(values({ installmentCount: count })).installmentCount).toContain('2 a 120')
  })

  it('exige descrição, valor > 0, nº de parcelas, 1ª data e categoria', () => {
    const errors = validatePlanForm(
      values({ description: ' ', totalAmount: '0', installmentCount: null, firstDate: null, categoryId: null }),
    )
    expect(Object.keys(errors).sort()).toEqual(
      ['categoryId', 'description', 'firstDate', 'installmentCount', 'totalAmount'].sort(),
    )
    expect(validatePlanForm(values({ totalAmount: '' })).totalAmount).toContain('Informe')
    expect(validatePlanForm(values({ totalAmount: 'abc' })).totalAmount).toContain('válido')
  })

  it('com cartão exige o cartão', () => {
    expect(validatePlanForm(values({ payWith: 'card' })).creditCardId).toBeDefined()
    expect(validatePlanForm(values({ payWith: 'card', creditCardId: 'cc1' })).creditCardId).toBeUndefined()
  })
})

describe('buildPlanRequest', () => {
  it('não calcula parcelas: envia apenas os dados do contrato (conta/método)', () => {
    expect(buildPlanRequest(values({ accountId: 'a1', paymentMethod: 'Pix' }))).toEqual({
      description: 'Notebook',
      totalAmount: 3000,
      installmentCount: 12,
      firstDate: '2026-11-18',
      categoryId: 'c1',
      notes: null,
      accountId: 'a1',
      paymentMethod: 'Pix',
    })
  })

  it('com cartão: creditCardId, método CreditCard e sem accountId', () => {
    const request = buildPlanRequest(values({ payWith: 'card', creditCardId: 'cc1', accountId: 'a1', notes: ' ok ' }))
    expect(request).toMatchObject({ creditCardId: 'cc1', paymentMethod: 'CreditCard', notes: 'ok' })
    expect('accountId' in request).toBe(false)
  })
})
