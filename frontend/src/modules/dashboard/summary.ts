import type { DashboardDto } from '@/types/api'
import { daysBetween } from '@/utils/period'

/** Não há contas nem movimentações: o dashboard deve guiar o usuário a começar. */
export function isDashboardEmpty(data: DashboardDto): boolean {
  const noAccounts = data.balance.accountsWithBalance + data.balance.accountsWithoutBalance === 0
  const noTransactions =
    data.totals.income === 0 &&
    data.totals.expense === 0 &&
    data.expensesByCategory.length === 0 &&
    data.upcomingDues.length === 0 &&
    data.monthlyHistory.every((m) => m.income === 0 && m.expense === 0)
  return noAccounts && noTransactions
}

/** Texto do aviso de contas sem saldo informado (contrato §9: ficam fora dos totais). */
export function accountsWithoutBalanceMessage(count: number): string | null {
  if (count <= 0) return null
  return count === 1
    ? '1 conta sem saldo informado fica fora dos totais de saldo.'
    : `${count} contas sem saldo informado ficam fora dos totais de saldo.`
}

/** Descrição relativa de um vencimento a partir do "hoje" devolvido pela API. */
export function describeDue(dueDate: string, today: string): string {
  const diff = daysBetween(today, dueDate)
  if (Number.isNaN(diff)) return ''
  if (diff === 0) return 'Vence hoje'
  if (diff === 1) return 'Vence amanhã'
  if (diff > 1) return `Vence em ${diff} dias`
  if (diff === -1) return 'Venceu ontem'
  return `Venceu há ${Math.abs(diff)} dias`
}
