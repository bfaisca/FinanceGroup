<script setup lang="ts">
import { defineAsyncComponent } from 'vue'
import type { ChartOption } from '@/components/charts/BaseChart.vue'
import LoadingState from '@/components/LoadingState.vue'

/**
 * Gráfico + alternativa tabular acessível (`slot table`), exibida em "Ver dados em tabela".
 * O ECharts só é baixado quando um gráfico é realmente exibido (bundle inicial enxuto).
 */
const BaseChart = defineAsyncComponent({
  loader: () => import('@/components/charts/BaseChart.vue'),
  loadingComponent: LoadingState,
  delay: 0,
})

withDefaults(
  defineProps<{
    option: ChartOption
    ariaLabel: string
    height?: string
    empty?: boolean
    emptyMessage?: string
    /** Abre a tabela por padrão. */
    tableOpen?: boolean
    tableLabel?: string
  }>(),
  {
    height: '300px',
    empty: false,
    emptyMessage: 'Sem dados para o período selecionado.',
    tableOpen: false,
    tableLabel: 'Ver dados em tabela',
  },
)
</script>

<template>
  <div class="chart-panel">
    <BaseChart :option="option" v-bind="{ ariaLabel }" :height="height" :empty="empty" :empty-message="emptyMessage" />
    <details v-if="!empty && $slots.table" class="chart-panel__details" :open="tableOpen">
      <summary>{{ tableLabel }}</summary>
      <div class="table-scroll">
        <slot name="table" />
      </div>
    </details>
  </div>
</template>

<style scoped>
.chart-panel__details {
  margin-top: 0.75rem;
}

.chart-panel__details summary {
  cursor: pointer;
  font-weight: 500;
}
</style>
