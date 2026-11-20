<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import SelectButton from 'primevue/selectbutton'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import FinancialCard from '@/components/FinancialCard.vue'
import type { ReportGroupBy } from '@/types/api'
import { formatCurrency } from '@/utils/format'
import { daysBetween } from '@/utils/period'
import { formatSeriesKey, formatSeriesKeyLong, incomeExpenseOption } from '../../charts'
import { useReport } from '../../composables'
import { reportService } from '../../services/reportService'
import ReportState from '../ReportState.vue'

const props = defineProps<{ from: string; to: string }>()

// Padrão de apresentação: períodos curtos por dia; longos por mês. O usuário pode alternar.
const defaultGroupBy = computed<ReportGroupBy>(() => (daysBetween(props.from, props.to) <= 31 ? 'day' : 'month'))
const override = ref<ReportGroupBy | null>(null)
const groupBy = computed<ReportGroupBy>(() => override.value ?? defaultGroupBy.value)

watch(
  () => `${props.from}|${props.to}`,
  () => {
    override.value = null
  },
)

const report = useReport(
  () => reportService.summary({ from: props.from, to: props.to, groupBy: groupBy.value }),
  () => `${props.from}|${props.to}|${groupBy.value}`,
)
const data = report.data

const groupOptions = [
  { value: 'day' as ReportGroupBy, label: 'Por dia' },
  { value: 'month' as ReportGroupBy, label: 'Por mês' },
]

const resultTone = computed(() => {
  const result = data.value?.totals.result ?? 0
  return result > 0 ? 'positive' : result < 0 ? 'negative' : 'neutral'
})

const isEmpty = computed(() => data.value !== null && data.value.series.every((s) => s.income === 0 && s.expense === 0))

const option = computed(() =>
  incomeExpenseOption(
    (data.value?.series ?? []).map((s) => ({ label: formatSeriesKey(s.key), income: s.income, expense: s.expense })),
  ),
)
const ariaLabel = computed(
  () =>
    `Gráfico de barras de receitas e despesas ${groupBy.value === 'day' ? 'por dia' : 'por mês'}. ${(
      data.value?.series ?? []
    )
      .map(
        (s) =>
          `${formatSeriesKeyLong(s.key)}: receitas ${formatCurrency(s.income)}, despesas ${formatCurrency(s.expense)}`,
      )
      .join('; ')}.`,
)
</script>

<template>
  <ReportState
    :loading="report.loading.value"
    :error="report.error.value"
    :loaded="data !== null"
    :empty="isEmpty"
    empty-title="Nenhuma movimentação no período"
    @retry="report.reload()"
  >
    <div v-if="data" class="stack" data-testid="summary-report">
      <section class="summary-grid" aria-label="Totais do período">
        <FinancialCard label="Receitas" icon="pi pi-arrow-up-right" tone="positive" :value="data.totals.income" />
        <FinancialCard label="Despesas" icon="pi pi-arrow-down-right" tone="negative" :value="data.totals.expense" />
        <FinancialCard
          label="Resultado"
          icon="pi pi-equals"
          :tone="resultTone"
          :value="data.totals.result"
          caption="Receitas menos despesas"
        />
        <FinancialCard label="Recebido" icon="pi pi-check-circle" :value="data.totals.receivedIncome" />
        <FinancialCard label="A receber" icon="pi pi-clock" :value="data.totals.pendingIncome" />
        <FinancialCard label="Pago" icon="pi pi-check-circle" :value="data.totals.paidExpense" />
        <FinancialCard label="A pagar" icon="pi pi-clock" :value="data.totals.pendingExpense" />
      </section>

      <section class="surface-card" aria-labelledby="summary-series-title">
        <div class="summary-series__header">
          <h2 id="summary-series-title" class="report-title">Receitas e despesas</h2>
          <SelectButton
            :model-value="groupBy"
            :options="groupOptions"
            option-label="label"
            option-value="value"
            :allow-empty="false"
            aria-label="Agrupamento da série"
            @update:model-value="(v: ReportGroupBy | null) => v && (override = v)"
          />
        </div>
        <ChartPanel :option="option" v-bind="{ ariaLabel }" :empty="isEmpty">
          <template #table>
            <table class="simple-table" data-testid="summary-table">
              <caption class="sr-only">
                Receitas, despesas e resultado por
                {{
                  groupBy === 'day' ? 'dia' : 'mês'
                }}
              </caption>
              <thead>
                <tr>
                  <th scope="col">{{ groupBy === 'day' ? 'Dia' : 'Mês' }}</th>
                  <th scope="col" class="num">Receitas</th>
                  <th scope="col" class="num">Despesas</th>
                  <th scope="col" class="num">Resultado</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="s in data.series" :key="s.key">
                  <th scope="row">{{ formatSeriesKeyLong(s.key) }}</th>
                  <td class="num money">{{ formatCurrency(s.income) }}</td>
                  <td class="num money">{{ formatCurrency(s.expense) }}</td>
                  <td class="num money">{{ formatCurrency(s.result) }}</td>
                </tr>
              </tbody>
            </table>
          </template>
        </ChartPanel>
      </section>
    </div>
  </ReportState>
</template>

<style scoped>
.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 13rem), 1fr));
  gap: 1rem;
}

.summary-series__header {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}
</style>
