import { describe, expect, it } from 'vitest'
import type { PaymentMethod } from '@/types/api'
import {
  INSTALLMENT_STATUS_PRESENTATION,
  INSTALLMENT_STATUSES,
  PAYMENT_METHODS,
  paymentMethodLabel,
  RECURRENCE_FREQUENCIES,
} from '../labels'

describe('paymentMethodLabel', () => {
  it('traduz todos os métodos do contrato para pt-BR', () => {
    const expected: Record<PaymentMethod, string> = {
      Cash: 'Dinheiro',
      DebitCard: 'Cartão de débito',
      CreditCard: 'Cartão de crédito',
      Pix: 'Pix',
      BankTransfer: 'Transferência',
      BankSlip: 'Boleto',
      Other: 'Outro',
    }
    for (const method of PAYMENT_METHODS) expect(paymentMethodLabel(method)).toBe(expected[method])
  })

  it('null (relatórios) = "Não informado"', () => {
    expect(paymentMethodLabel(null)).toBe('Não informado')
    expect(paymentMethodLabel(undefined)).toBe('Não informado')
  })
})

describe('situação do plano de parcelamento', () => {
  it('cobre os três estados do contrato com rótulo, ícone e tom', () => {
    expect(INSTALLMENT_STATUSES).toEqual(['Open', 'Completed', 'Canceled'])
    expect(INSTALLMENT_STATUS_PRESENTATION.Open.label).toBe('Em aberto')
    expect(INSTALLMENT_STATUS_PRESENTATION.Completed.label).toBe('Concluído')
    expect(INSTALLMENT_STATUS_PRESENTATION.Canceled.label).toBe('Cancelado')
    for (const status of INSTALLMENT_STATUSES) {
      expect(INSTALLMENT_STATUS_PRESENTATION[status].icon).toMatch(/^pi pi-/)
    }
  })
})

describe('frequências', () => {
  it('expõe as 9 frequências do contrato', () => {
    expect(RECURRENCE_FREQUENCIES).toHaveLength(9)
    expect(RECURRENCE_FREQUENCIES).toContain('Custom')
  })
})
