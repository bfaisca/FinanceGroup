import type { ChartOption } from '@/components/charts/BaseChart.vue'
import { formatCurrency, formatPercent, parseDateOnly } from '@/utils/format'
import { formatMonthLong, formatMonthShort } from '@/utils/period'

export const INCOME_COLOR = '#2e9e5b'
export const EXPENSE_COLOR = '#d9534f'
export const RESULT_COLOR = '#3b82f6'

/** Cores de reserva (itens sem cor definida). */
export const FALLBACK_COLORS = [
  '#0f766e',
  '#2563eb',
  '#9333ea',
  '#d97706',
  '#db2777',
  '#16a34a',
  '#ea580c',
  '#4f46e5',
  '#64748b',
]

export function colorFor(index: number, color?: string | null): string {
  return color ?? FALLBACK_COLORS[index % FALLBACK_COLORS.length] ?? '#64748b'
}

const compact = new Intl.NumberFormat('pt-BR', { notation: 'compact', maximumFractionDigits: 1 })

export function compactMoney(value: number): string {
  return compact.format(value)
}

/** "2026-11-05" → "05/11"; "2026-11" → "nov. de 26". */
export function formatSeriesKey(key: string): string {
  if (/^\d{4}-\d{2}-\d{2}$/.test(key)) {
    return parseDateOnly(key) ? `${key.slice(8, 10)}/${key.slice(5, 7)}` : key
  }
  return formatMonthShort(key)
}

/** Rótulo longo para leitores de tela/tabelas. */
export function formatSeriesKeyLong(key: string): string {
  if (/^\d{4}-\d{2}-\d{2}$/.test(key)) return `${key.slice(8, 10)}/${key.slice(5, 7)}/${key.slice(0, 4)}`
  return formatMonthLong(key)
}

const moneyTooltip = { trigger: 'axis' as const, valueFormatter: (value: unknown) => formatCurrency(Number(value)) }

interface IncomeExpensePoint {
  label: string
  income: number
  expense: number
}

/** Barras receita × despesa (opcionalmente com uma linha, ex.: resultado acumulado). */
export function incomeExpenseOption(
  points: IncomeExpensePoint[],
  line?: { name: string; values: number[] },
): ChartOption {
  const series: ChartOption['series'] = [
    { name: 'Receitas', type: 'bar', itemStyle: { color: INCOME_COLOR }, data: points.map((p) => p.income) },
    { name: 'Despesas', type: 'bar', itemStyle: { color: EXPENSE_COLOR }, data: points.map((p) => p.expense) },
  ]
  if (line) {
    series.push({
      name: line.name,
      type: 'line',
      symbol: 'circle',
      symbolSize: 7,
      lineStyle: { color: RESULT_COLOR, width: 2 },
      itemStyle: { color: RESULT_COLOR },
      data: line.values,
    })
  }
  return {
    tooltip: moneyTooltip,
    legend: { top: 0 },
    grid: { left: 8, right: 8, bottom: 8, top: 40, containLabel: true },
    xAxis: { type: 'category', data: points.map((p) => p.label) },
    yAxis: { type: 'value', axisLabel: { formatter: (value: number) => compactMoney(value) } },
    series,
  }
}

export interface ShareDatum {
  name: string
  value: number
  color: string
}

/** Rosca de participação (valor + percentual no tooltip). */
export function donutOption(data: ShareDatum[]): ChartOption {
  return {
    tooltip: {
      trigger: 'item',
      formatter: (params: unknown) => {
        const p = params as { name: string; value: number; percent: number }
        return `${p.name}<br/><strong>${formatCurrency(p.value)}</strong> (${formatPercent(p.percent)})`
      },
    },
    series: [
      {
        type: 'pie',
        radius: ['48%', '74%'],
        avoidLabelOverlap: true,
        label: { show: false },
        itemStyle: { borderWidth: 2, borderColor: 'transparent' },
        data: data.map((d) => ({ name: d.name, value: d.value, itemStyle: { color: d.color } })),
      },
    ],
  }
}

/** Descrição textual para `aria-label` de gráficos de participação. */
export function describeShares(title: string, items: { name: string; total: number; percentage: number }[]): string {
  return `${title}. ${items
    .map((i) => `${i.name}: ${formatCurrency(i.total)}, ${formatPercent(i.percentage)}`)
    .join('; ')}.`
}
