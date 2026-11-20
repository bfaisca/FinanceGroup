<script setup lang="ts">
import { computed, defineAsyncComponent } from 'vue'
import type { ChartOption } from '@/components/charts/BaseChart.vue'
import LoadingState from '@/components/LoadingState.vue'
import type { DashboardDto } from '@/types/api'
import { formatCurrency } from '@/utils/format'
import { formatMonthLong, formatMonthShort } from '@/utils/period'

type Month = DashboardDto['monthlyHistory'][number]

// ECharts só é baixado quando um gráfico é realmente exibido (mantém o bundle inicial enxuto).
const BaseChart = defineAsyncComponent({
  loader: () => import('@/components/charts/BaseChart.vue'),
  loadingComponent: LoadingState,
  delay: 0,
})

const props = defineProps<{ months: Month[]; loading?: boolean }>()

const INCOME_COLOR = '#2e9e5b'
const EXPENSE_COLOR = '#d9534f'
const RESULT_COLOR = '#3b82f6'

const compact = new Intl.NumberFormat('pt-BR', { notation: 'compact', maximumFractionDigits: 1 })

const hasData = computed(() => props.months.some((m) => m.income !== 0 || m.expense !== 0))

const option = computed<ChartOption>(() => ({
  tooltip: {
    trigger: 'axis',
    valueFormatter: (value: unknown) => formatCurrency(Number(value)),
  },
  legend: { top: 0 },
  grid: { left: 8, right: 8, bottom: 8, top: 40, containLabel: true },
  xAxis: { type: 'category', data: props.months.map((m) => formatMonthShort(m.month)) },
  yAxis: { type: 'value', axisLabel: { formatter: (value: number) => compact.format(value) } },
  series: [
    { name: 'Receitas', type: 'bar', itemStyle: { color: INCOME_COLOR }, data: props.months.map((m) => m.income) },
    { name: 'Despesas', type: 'bar', itemStyle: { color: EXPENSE_COLOR }, data: props.months.map((m) => m.expense) },
    {
      name: 'Resultado',
      type: 'line',
      symbol: 'circle',
      symbolSize: 8,
      lineStyle: { color: RESULT_COLOR, width: 2 },
      itemStyle: { color: RESULT_COLOR },
      data: props.months.map((m) => m.result),
    },
  ],
}))

const ariaLabel = computed(
  () =>
    `Gráfico de barras de receitas e despesas dos últimos meses, com linha de resultado. ${props.months
      .map(
        (m) =>
          `${formatMonthLong(m.month)}: receitas ${formatCurrency(m.income)}, despesas ${formatCurrency(m.expense)}, resultado ${formatCurrency(m.result)}`,
      )
      .join('; ')}.`,
)
</script>

<template>
  <div>
    <BaseChart
      :option="option"
      v-bind="{ ariaLabel }"
      height="300px"
      :loading="loading"
      :empty="!hasData"
      empty-message="Sem movimentações nos últimos meses."
    />
    <details v-if="hasData" class="monthly-chart__details">
      <summary>Ver dados em tabela</summary>
      <table class="monthly-chart__table">
        <caption class="sr-only">
          Histórico mensal de receitas, despesas e resultado
        </caption>
        <thead>
          <tr>
            <th scope="col">Mês</th>
            <th scope="col" class="num">Receitas</th>
            <th scope="col" class="num">Despesas</th>
            <th scope="col" class="num">Resultado</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="m in months" :key="m.month">
            <th scope="row">{{ formatMonthLong(m.month) }}</th>
            <td class="num money">{{ formatCurrency(m.income) }}</td>
            <td class="num money">{{ formatCurrency(m.expense) }}</td>
            <td class="num money">{{ formatCurrency(m.result) }}</td>
          </tr>
        </tbody>
      </table>
    </details>
  </div>
</template>

<style scoped>
.monthly-chart__details {
  margin-top: 0.75rem;
}

.monthly-chart__details summary {
  cursor: pointer;
  font-weight: 500;
}

.monthly-chart__table {
  width: 100%;
  margin-top: 0.5rem;
  border-collapse: collapse;
  font-size: 0.9375rem;
}

.monthly-chart__table th,
.monthly-chart__table td {
  padding: 0.375rem 0.5rem;
  border-bottom: 1px solid var(--gf-border);
  text-align: left;
  font-weight: 400;
}

.monthly-chart__table thead th {
  font-weight: 600;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.monthly-chart__table .num {
  text-align: right;
}

.monthly-chart__table tbody th {
  text-transform: lowercase;
}

.monthly-chart__table tbody th::first-letter {
  text-transform: uppercase;
}
</style>
