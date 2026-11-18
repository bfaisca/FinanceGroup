<script setup lang="ts">
/**
 * Wrapper base de gráficos (D-002 — ECharts via vue-echarts) com importação modular.
 * Registre aqui novos tipos de gráfico/componentes quando forem necessários (tree-shaking).
 * Não recebe dados por conta própria: o consumidor passa `option` com dados reais da API.
 */
import { computed } from 'vue'
import { use, type ComposeOption } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart, LineChart, PieChart } from 'echarts/charts'
import type { BarSeriesOption, LineSeriesOption, PieSeriesOption } from 'echarts/charts'
import {
  AriaComponent,
  DatasetComponent,
  GridComponent,
  LegendComponent,
  TitleComponent,
  TooltipComponent,
} from 'echarts/components'
import type {
  AriaComponentOption,
  DatasetComponentOption,
  GridComponentOption,
  LegendComponentOption,
  TitleComponentOption,
  TooltipComponentOption,
} from 'echarts/components'
import VChart from 'vue-echarts'
import { useTheme } from '@/composables/useTheme'
import LoadingState from '@/components/LoadingState.vue'
import EmptyState from '@/components/EmptyState.vue'

use([
  CanvasRenderer,
  BarChart,
  LineChart,
  PieChart,
  AriaComponent,
  DatasetComponent,
  GridComponent,
  LegendComponent,
  TitleComponent,
  TooltipComponent,
])

export type ChartOption = ComposeOption<
  | BarSeriesOption
  | LineSeriesOption
  | PieSeriesOption
  | AriaComponentOption
  | DatasetComponentOption
  | GridComponentOption
  | LegendComponentOption
  | TitleComponentOption
  | TooltipComponentOption
>

const props = withDefaults(
  defineProps<{
    option: ChartOption
    /** Descrição acessível do gráfico (obrigatória). */
    ariaLabel: string
    height?: string
    loading?: boolean
    empty?: boolean
    emptyMessage?: string
  }>(),
  { height: '320px', loading: false, empty: false, emptyMessage: 'Sem dados para o período selecionado.' },
)

const { isDark } = useTheme()

const theme = computed(() => (isDark.value ? 'dark' : undefined))
const mergedOption = computed<ChartOption>(() => ({
  backgroundColor: 'transparent',
  aria: { enabled: true },
  textStyle: { fontFamily: 'Inter, system-ui, sans-serif' },
  ...props.option,
}))
</script>

<template>
  <div class="base-chart" :style="{ height }">
    <LoadingState v-if="loading" label="Carregando gráfico…" />
    <EmptyState v-else-if="empty" icon="pi pi-chart-bar" :title="emptyMessage" />
    <VChart v-else :option="mergedOption" :theme="theme" autoresize role="img" :aria-label="ariaLabel" />
  </div>
</template>

<style scoped>
.base-chart {
  width: 100%;
  position: relative;
}
</style>
