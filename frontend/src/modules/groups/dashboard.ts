import type { ChartOption } from '@/components/charts/BaseChart.vue'
import { compactMoney, EXPENSE_COLOR, INCOME_COLOR } from '@/modules/reports/charts'
import { formatCurrency, formatPercent } from '@/utils/format'
import type { GroupDashboardDto, GroupPaidByItemDto } from './types'

/** Textos do quadro "Contas Pagas" (D-034 emendada): somente o título. */
export const PAID_BY_COPY = {
  title: 'Contas Pagas',
}

export const PENDING_COLOR = '#d97706'

export interface PaidByRow {
  userId: string
  /** Nome exibido, com a marca de ex-membro quando já não participa. */
  name: string
  former: boolean
  /** Principal + juros. */
  paidAmount: number
  interestAmount: number
  /** Principal (sem juros). */
  principalAmount: number
  percent: number
}

export function displayName(item: Pick<GroupPaidByItemDto, 'name' | 'status'>): string {
  return item.status === 'Active' ? item.name : `${item.name} (ex-membro)`
}

/** Linhas de "Contas Pagas" (a API já ordena e só traz quem pagou algo). */
export function paidByRows(items: GroupPaidByItemDto[]): PaidByRow[] {
  return items.map((item) => ({
    userId: item.userId,
    name: displayName(item),
    former: item.status !== 'Active',
    paidAmount: item.paidAmount,
    interestAmount: item.interestAmount,
    principalAmount: Math.round((item.paidAmount - item.interestAmount) * 100) / 100,
    percent: item.percent,
  }))
}

/** "+12,5%" / "−3,0%" / "—" (base zero: sem comparação possível). */
export function formatChange(percent: number | null): string {
  if (percent === null || !Number.isFinite(percent)) return '—'
  if (percent === 0) return formatPercent(0)
  return `${percent > 0 ? '+' : '−'}${formatPercent(Math.abs(percent))}`
}

/** Pago (efetivamente, com juros) × pendente (só principal) — `totals.expensesPaid` e `totals.expensesPending`. */
export function paidPendingSlices(totals: GroupDashboardDto['totals']) {
  return [
    { key: 'paid', name: 'Pago', value: totals.expensesPaid, color: INCOME_COLOR },
    { key: 'pending', name: 'Pendente', value: totals.expensesPending, color: PENDING_COLOR },
  ]
}

/** Barras mensais de despesas (valor original) × pago (efetivamente, com juros). */
export function monthlyOption(points: { label: string; expenses: number; paid: number }[]): ChartOption {
  return {
    tooltip: { trigger: 'axis', valueFormatter: (value: unknown) => formatCurrency(Number(value)) },
    legend: { top: 0 },
    grid: { left: 8, right: 8, bottom: 8, top: 40, containLabel: true },
    xAxis: { type: 'category', data: points.map((p) => p.label) },
    yAxis: { type: 'value', axisLabel: { formatter: (value: number) => compactMoney(value) } },
    series: [
      { name: 'Despesas', type: 'bar', itemStyle: { color: EXPENSE_COLOR }, data: points.map((p) => p.expenses) },
      { name: 'Pago', type: 'bar', itemStyle: { color: INCOME_COLOR }, data: points.map((p) => p.paid) },
    ],
  }
}

export function isGroupDashboardEmpty(data: {
  totals: { expenses: number; expensesPaid: number }
  paidBy: unknown[]
  monthly: { expenses: number; paid: number }[]
}): boolean {
  return (
    data.totals.expenses === 0 &&
    data.totals.expensesPaid === 0 &&
    data.paidBy.length === 0 &&
    data.monthly.every((m) => m.expenses === 0 && m.paid === 0)
  )
}
