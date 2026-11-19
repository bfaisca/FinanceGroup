import { describe, expect, it } from 'vitest'
import { makeDashboard } from '@/test/fixtures'
import { accountsWithoutBalanceMessage, describeDue, isDashboardEmpty } from '../summary'

describe('summary do dashboard', () => {
  it('detecta o estado vazio (sem contas e sem movimentações)', () => {
    const empty = makeDashboard({
      balance: {
        reportedTotal: null,
        projectedTotal: null,
        projectedAsOf: '2026-11-30',
        accountsWithBalance: 0,
        accountsWithoutBalance: 0,
      },
      totals: {
        income: 0,
        expense: 0,
        result: 0,
        receivedIncome: 0,
        pendingIncome: 0,
        paidExpense: 0,
        pendingExpense: 0,
      },
      expensesByCategory: [],
      upcomingDues: [],
      monthlyHistory: [{ month: '2026-11', income: 0, expense: 0, result: 0 }],
    })
    expect(isDashboardEmpty(empty)).toBe(true)
    expect(isDashboardEmpty(makeDashboard())).toBe(false)
  })

  it('com conta mas sem movimentações NÃO é vazio (mostra os cartões)', () => {
    const data = makeDashboard({
      totals: {
        income: 0,
        expense: 0,
        result: 0,
        receivedIncome: 0,
        pendingIncome: 0,
        paidExpense: 0,
        pendingExpense: 0,
      },
      expensesByCategory: [],
      upcomingDues: [],
      monthlyHistory: [],
    })
    expect(isDashboardEmpty(data)).toBe(false)
  })

  it('mensagem de contas sem saldo, no singular e no plural', () => {
    expect(accountsWithoutBalanceMessage(0)).toBeNull()
    expect(accountsWithoutBalanceMessage(1)).toBe('1 conta sem saldo informado fica fora dos totais de saldo.')
    expect(accountsWithoutBalanceMessage(3)).toBe('3 contas sem saldo informado ficam fora dos totais de saldo.')
  })

  it('descreve vencimentos relativos ao "hoje" do servidor', () => {
    expect(describeDue('2026-11-18', '2026-11-18')).toBe('Vence hoje')
    expect(describeDue('2026-11-19', '2026-11-18')).toBe('Vence amanhã')
    expect(describeDue('2026-11-25', '2026-11-18')).toBe('Vence em 7 dias')
    expect(describeDue('2026-11-17', '2026-11-18')).toBe('Venceu ontem')
    expect(describeDue('2026-11-10', '2026-11-18')).toBe('Venceu há 8 dias')
  })
})
