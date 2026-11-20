import { describe, expect, it } from 'vitest'
import {
  buildCreateRecurrenceRequest,
  buildUpdateRecurrenceRequest,
  emptyRecurrenceForm,
  formValuesFromRecurrence,
  validateRecurrenceForm,
  type RecurrenceFormValues,
} from '../validation'
import { makeRecurrence } from '@/test/fixtures'

function values(partial: Partial<RecurrenceFormValues> = {}): RecurrenceFormValues {
  return { ...emptyRecurrenceForm('2026-11-18'), description: 'Netflix', amount: '55,90', categoryId: 'c1', ...partial }
}

describe('validateRecurrenceForm', () => {
  it('aceita regra válida (padrão: mensal, intervalo 1, início hoje)', () => {
    const form = values()
    expect(form).toMatchObject({ frequency: 'Monthly', interval: 1, startDate: '2026-11-18', type: 'Expense' })
    expect(validateRecurrenceForm(form, { editing: false })).toEqual({})
  })

  it('exige descrição, valor, categoria e início (só na criação)', () => {
    const errors = validateRecurrenceForm(values({ description: '', amount: '', categoryId: null, startDate: null }), {
      editing: false,
    })
    expect(Object.keys(errors).sort()).toEqual(['amount', 'categoryId', 'description', 'startDate'])
    expect(validateRecurrenceForm(values({ startDate: null }), { editing: true }).startDate).toBeUndefined()
  })

  it.each([0, 366, 2.5, -1])('rejeita intervalo %s', (interval) => {
    expect(validateRecurrenceForm(values({ interval }), { editing: false }).interval).toContain('1 a 365')
  })

  it.each([1, 365])('aceita intervalo %s', (interval) => {
    expect(validateRecurrenceForm(values({ interval }), { editing: false }).interval).toBeUndefined()
  })

  it('término não pode ser anterior ao início', () => {
    expect(validateRecurrenceForm(values({ endDate: '2026-11-01' }), { editing: false }).endDate).toContain('posterior')
    expect(validateRecurrenceForm(values({ endDate: '2026-11-18' }), { editing: false }).endDate).toBeUndefined()
  })

  it('cartão só em despesa e exige o cartão', () => {
    expect(validateRecurrenceForm(values({ payWith: 'card' }), { editing: false }).creditCardId).toBeDefined()
    expect(
      validateRecurrenceForm(values({ type: 'Income', payWith: 'card' }), { editing: false }).creditCardId,
    ).toBeUndefined()
  })
})

describe('requests', () => {
  it('criação envia type, startDate e interval', () => {
    expect(buildCreateRecurrenceRequest(values({ frequency: 'Custom', interval: 10 }))).toEqual({
      description: 'Netflix',
      type: 'Expense',
      amount: 55.9,
      categoryId: 'c1',
      notes: null,
      frequency: 'Custom',
      interval: 10,
      endDate: null,
      startDate: '2026-11-18',
      accountId: null,
      paymentMethod: null,
    })
  })

  it('edição não envia type nem startDate', () => {
    const request = buildUpdateRecurrenceRequest(formValuesFromRecurrence(makeRecurrence()))
    expect('type' in request).toBe(false)
    expect('startDate' in request).toBe(false)
    expect(request).toMatchObject({ creditCardId: 'cc1', paymentMethod: 'CreditCard', amount: 55.9, interval: 1 })
    expect('accountId' in request).toBe(false)
  })
})
