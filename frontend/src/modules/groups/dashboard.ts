import type { GroupParticipationItemDto } from './types'
import { formatPercent } from '@/utils/format'
import type { TransactionType } from '@/types/api'

export type ParticipationKind = 'createdBy' | 'paidBy'

/** Textos fixos do dashboard (D-034 / CLAUDE.md §6): regra de cálculo explícita e aviso de não-dívida. */
export const PARTICIPATION_COPY: Record<ParticipationKind, { title: string; rule: string }> = {
  createdBy: {
    title: 'Participação por quem cadastrou',
    rule: 'Regra de cálculo: soma do valor das movimentações (não canceladas, com data no período) cadastradas por cada participante, dividida pelo total do período do mesmo tipo. Conta o valor total da movimentação, tenha ela sido paga ou não.',
  },
  paidBy: {
    title: 'Participação por quem pagou',
    rule: 'Regra de cálculo: soma dos pagamentos e recebimentos efetivamente registrados por cada participante em movimentações do período, dividida pelo total já pago do mesmo tipo. Movimentações pendentes não entram, pois ainda não há pagamento.',
  },
}

export const NOT_DEBT_NOTICE =
  'Os percentuais são indicadores estatísticos. Eles NÃO representam dívida, crédito ou valor a acertar entre os participantes: o grupo serve para transparência e visão consolidada, não para dividir contas.'

export interface ParticipationRow {
  userId: string
  /** Nome exibido, com a marca de ex-membro quando já não participa. */
  name: string
  former: boolean
  amount: number
  percent: number
}

export function displayName(item: Pick<GroupParticipationItemDto, 'name' | 'status'>): string {
  return item.status === 'Active' ? item.name : `${item.name} (ex-membro)`
}

/** Linhas de um tipo (despesa/receita), sem quem tem valor zero (não aparece no gráfico). */
export function participationRows(items: GroupParticipationItemDto[], type: TransactionType): ParticipationRow[] {
  return items
    .map((item) => ({
      userId: item.userId,
      name: displayName(item),
      former: item.status !== 'Active',
      amount: type === 'Expense' ? item.expenseAmount : item.incomeAmount,
      percent: type === 'Expense' ? item.expensePercent : item.incomePercent,
    }))
    .filter((row) => row.amount > 0)
}

/** "+12,5%" / "−3,0%" / "—" (base zero: sem comparação possível). */
export function formatChange(percent: number | null): string {
  if (percent === null || !Number.isFinite(percent)) return '—'
  if (percent === 0) return formatPercent(0)
  return `${percent > 0 ? '+' : '−'}${formatPercent(Math.abs(percent))}`
}

export function isGroupDashboardEmpty(data: {
  totals: { expenses: number; income: number }
  createdBy: unknown[]
  paidBy: unknown[]
  monthly: { income: number; expenses: number }[]
}): boolean {
  return (
    data.totals.expenses === 0 &&
    data.totals.income === 0 &&
    data.createdBy.length === 0 &&
    data.paidBy.length === 0 &&
    data.monthly.every((m) => m.income === 0 && m.expenses === 0)
  )
}
