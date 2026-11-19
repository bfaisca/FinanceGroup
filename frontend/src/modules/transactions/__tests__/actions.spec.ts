import { describe, expect, it } from 'vitest'
import { availableActions, paidPercentage } from '../actions'

describe('availableActions', () => {
  it('pendente sem pagamentos: pode pagar, editar, cancelar e excluir', () => {
    expect(availableActions({ status: 'Pending', paidAmount: 0, remainingAmount: 100 })).toMatchObject({
      canEdit: true,
      canPay: true,
      canCancel: true,
      canReopen: false,
      canDelete: true,
      deleteBlockedByPayments: false,
    })
  })

  it('com pagamentos: não exclui (só cancela)', () => {
    const actions = availableActions({ status: 'Pending', paidAmount: 40, remainingAmount: 60 })
    expect(actions.canDelete).toBe(false)
    expect(actions.deleteBlockedByPayments).toBe(true)
    expect(actions.canCancel).toBe(true)
  })

  it('liquidada: não oferece novo pagamento', () => {
    expect(availableActions({ status: 'Paid', paidAmount: 100, remainingAmount: 0 }).canPay).toBe(false)
  })

  it('cancelada: só reabrir (sem editar, pagar, cancelar ou remover pagamentos)', () => {
    expect(availableActions({ status: 'Canceled', paidAmount: 10, remainingAmount: 90 })).toMatchObject({
      canEdit: false,
      canPay: false,
      canCancel: false,
      canReopen: true,
      canRemovePayments: false,
    })
  })

  it('atrasada ainda aceita pagamento', () => {
    expect(availableActions({ status: 'Overdue', paidAmount: 0, remainingAmount: 10 }).canPay).toBe(true)
  })
})

describe('paidPercentage', () => {
  it('calcula em centavos e limita a 0–100', () => {
    expect(paidPercentage(50, 200)).toBe(25)
    expect(paidPercentage(0.1 + 0.2, 1)).toBe(30)
    expect(paidPercentage(300, 200)).toBe(100)
    expect(paidPercentage(10, 0)).toBe(0)
  })
})
