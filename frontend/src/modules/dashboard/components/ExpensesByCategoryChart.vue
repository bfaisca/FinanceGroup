<script setup lang="ts">
import { computed, defineAsyncComponent } from 'vue'
import type { ChartOption } from '@/components/charts/BaseChart.vue'
import LoadingState from '@/components/LoadingState.vue'
import type { DashboardDto } from '@/types/api'
import { formatCurrency } from '@/utils/format'

type Item = DashboardDto['expensesByCategory'][number]

// ECharts só é baixado quando um gráfico é realmente exibido (mantém o bundle inicial enxuto).
const BaseChart = defineAsyncComponent({
  loader: () => import('@/components/charts/BaseChart.vue'),
  loadingComponent: LoadingState,
  delay: 0,
})

const props = defineProps<{ items: Item[]; loading?: boolean }>()

/** Cores de reserva (categorias sem cor definida). */
const FALLBACK_COLORS = [
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

const rows = computed(() =>
  props.items.map((item, index) => ({
    ...item,
    displayColor: item.color ?? FALLBACK_COLORS[index % FALLBACK_COLORS.length] ?? '#64748b',
  })),
)

const percentFormat = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })

const option = computed<ChartOption>(() => ({
  tooltip: {
    trigger: 'item',
    formatter: (params: unknown) => {
      const p = params as { name: string; value: number; percent: number }
      return `${p.name}<br/><strong>${formatCurrency(p.value)}</strong> (${percentFormat.format(p.percent)}%)`
    },
  },
  series: [
    {
      type: 'pie',
      radius: ['48%', '74%'],
      avoidLabelOverlap: true,
      label: { show: false },
      itemStyle: { borderWidth: 2, borderColor: 'transparent' },
      data: rows.value.map((r) => ({ name: r.categoryName, value: r.total, itemStyle: { color: r.displayColor } })),
    },
  ],
}))

const ariaLabel = computed(
  () =>
    `Gráfico de rosca das despesas por categoria. ${rows.value
      .map((r) => `${r.categoryName}: ${formatCurrency(r.total)}, ${percentFormat.format(r.percentage)}%`)
      .join('; ')}.`,
)
</script>

<template>
  <div class="expenses-chart">
    <BaseChart
      class="expenses-chart__chart"
      :option="option"
      v-bind="{ ariaLabel }"
      height="260px"
      :loading="loading"
      :empty="rows.length === 0"
      empty-message="Nenhuma despesa no período."
    />
    <table v-if="rows.length > 0" class="expenses-chart__table">
      <caption class="sr-only">
        Despesas por categoria no período
      </caption>
      <thead>
        <tr>
          <th scope="col">Categoria</th>
          <th scope="col" class="num">Total</th>
          <th scope="col" class="num">%</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in rows" :key="row.categoryId">
          <th scope="row">
            <span class="expenses-chart__dot" :style="{ background: row.displayColor }" aria-hidden="true" />
            {{ row.categoryName }}
          </th>
          <td class="num money">{{ formatCurrency(row.total) }}</td>
          <td class="num">{{ percentFormat.format(row.percentage) }}%</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.expenses-chart {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 1rem;
  align-items: center;
}

@media (min-width: 1100px) {
  .expenses-chart {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  }
}

.expenses-chart__table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9375rem;
}

.expenses-chart__table th,
.expenses-chart__table td {
  padding: 0.375rem 0.5rem;
  border-bottom: 1px solid var(--gf-border);
  text-align: left;
  font-weight: 400;
}

.expenses-chart__table thead th {
  font-weight: 600;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.expenses-chart__table .num {
  text-align: right;
}

.expenses-chart__dot {
  display: inline-block;
  width: 0.75rem;
  height: 0.75rem;
  margin-right: 0.5rem;
  border-radius: 50%;
}
</style>
