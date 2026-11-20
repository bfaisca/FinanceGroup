<script setup lang="ts">
import { computed } from 'vue'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import type { ChartOption } from '@/components/charts/BaseChart.vue'
import FinancialCard from '@/components/FinancialCard.vue'
import type { ReportInstallmentsDto } from '@/types/api'
import { formatCurrency } from '@/utils/format'
import { formatMonthLong, formatMonthShort } from '@/utils/period'

/** Comprometimento futuro de parcelamentos (`/reports/installments`): total restante + barras por mês. */
const props = defineProps<{ report: ReportInstallmentsDto }>()

const COMMITMENT_COLOR = '#d97706'
const compact = new Intl.NumberFormat('pt-BR', { notation: 'compact', maximumFractionDigits: 1 })

const months = computed(() => props.report.commitmentByMonth)
const hasData = computed(() => months.value.some((m) => m.amount !== 0))

const option = computed<ChartOption>(() => ({
  tooltip: { trigger: 'axis', valueFormatter: (value: unknown) => formatCurrency(Number(value)) },
  grid: { left: 8, right: 8, bottom: 8, top: 24, containLabel: true },
  xAxis: { type: 'category', data: months.value.map((m) => formatMonthShort(m.month)) },
  yAxis: { type: 'value', axisLabel: { formatter: (value: number) => compact.format(value) } },
  series: [
    {
      name: 'Parcelas a pagar',
      type: 'bar',
      itemStyle: { color: COMMITMENT_COLOR },
      data: months.value.map((m) => m.amount),
    },
  ],
}))

const ariaLabel = computed(
  () =>
    `Gráfico de barras do comprometimento futuro com parcelamentos por mês. ${months.value
      .map((m) => `${formatMonthLong(m.month)}: ${formatCurrency(m.amount)} em ${m.installmentsCount} parcelas`)
      .join('; ')}.`,
)
</script>

<template>
  <div class="commitment">
    <div class="commitment__cards">
      <FinancialCard
        label="Total restante"
        icon="pi pi-list-check"
        tone="negative"
        :value="report.totalRemaining"
        caption="Soma do restante das parcelas em aberto"
      />
      <article class="commitment__count">
        <p class="commitment__count-label">Planos em aberto</p>
        <p class="commitment__count-value" data-testid="open-plans">{{ report.openPlans }}</p>
      </article>
    </div>

    <ChartPanel
      :option="option"
      v-bind="{ ariaLabel }"
      :empty="!hasData"
      empty-message="Nenhum compromisso de parcelas no período."
    >
      <template #table>
        <table class="simple-table" data-testid="commitment-table">
          <caption class="sr-only">
            Comprometimento futuro com parcelamentos por mês
          </caption>
          <thead>
            <tr>
              <th scope="col">Mês</th>
              <th scope="col" class="num">Parcelas</th>
              <th scope="col" class="num">Valor</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="m in months" :key="m.month">
              <th scope="row">{{ formatMonthLong(m.month) }}</th>
              <td class="num">{{ m.installmentsCount }}</td>
              <td class="num money">{{ formatCurrency(m.amount) }}</td>
            </tr>
          </tbody>
        </table>
      </template>
    </ChartPanel>
    <p class="commitment__note">
      O mês atual inclui parcelas atrasadas. Valores correspondem ao restante de cada parcela em aberto.
    </p>
  </div>
</template>

<style scoped>
.commitment {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.commitment__cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 14rem), 1fr));
  gap: 1rem;
}

.commitment__count {
  padding: 1.25rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.commitment__count-label {
  margin: 0;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--gf-text-muted);
}

.commitment__count-value {
  margin: 0.5rem 0 0;
  font-size: 1.5rem;
  font-weight: 700;
}

.commitment__note {
  margin: 0;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}
</style>
