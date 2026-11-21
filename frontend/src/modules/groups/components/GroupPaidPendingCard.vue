<script setup lang="ts">
import { computed } from 'vue'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import { donutOption } from '@/modules/reports/charts'
import { formatCurrency } from '@/utils/format'
import { paidPendingSlices } from '../dashboard'
import type { GroupDashboardDto } from '../types'

/**
 * Pago × Pendente: `totals.expensesPaid` (efetivamente pago, com juros) e `totals.expensesPending`
 * (só principal). Os juros/multa pagos aparecem à parte.
 */
const props = defineProps<{ totals: GroupDashboardDto['totals'] }>()

const slices = computed(() => paidPendingSlices(props.totals))
const option = computed(() => donutOption(slices.value.map((s) => ({ name: s.name, value: s.value, color: s.color }))))
const hasData = computed(() => slices.value.some((s) => s.value > 0))
const ariaLabel = computed(
  () =>
    `Pago e pendente. Pago: ${formatCurrency(props.totals.expensesPaid)}, incluindo ${formatCurrency(props.totals.interestPaid)} de juros/multa; pendente: ${formatCurrency(props.totals.expensesPending)}.`,
)
</script>

<template>
  <section class="surface-card" aria-labelledby="paid-pending-title" data-testid="paid-pending-card">
    <h3 id="paid-pending-title" class="report-title paid-pending__title">Pago × Pendente</h3>
    <ChartPanel
      :option="option"
      v-bind="{ ariaLabel }"
      height="240px"
      :empty="!hasData"
      empty-message="Nenhuma despesa no período."
    >
      <template #table>
        <div class="table-scroll">
          <table class="simple-table" data-testid="paid-pending-table">
            <caption class="sr-only">
              Despesas pagas e pendentes no período
            </caption>
            <thead>
              <tr>
                <th scope="col">Situação</th>
                <th scope="col" class="num">Valor</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <th scope="row">Pago (inclui juros/multa)</th>
                <td class="num money">{{ formatCurrency(totals.expensesPaid) }}</td>
              </tr>
              <tr>
                <th scope="row">Juros/multa pagos</th>
                <td class="num money">{{ formatCurrency(totals.interestPaid) }}</td>
              </tr>
              <tr>
                <th scope="row">Pendente</th>
                <td class="num money">{{ formatCurrency(totals.expensesPending) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </ChartPanel>

    <ul class="paid-pending__legend" aria-label="Legenda: pago e pendente">
      <li v-for="slice in slices" :key="slice.key" class="paid-pending__item" :data-testid="`slice-${slice.key}`">
        <span class="paid-pending__dot" :style="{ background: slice.color }" aria-hidden="true" />
        <span class="paid-pending__name">{{ slice.name }}</span>
        <span class="money">{{ formatCurrency(slice.value) }}</span>
      </li>
    </ul>
    <p class="paid-pending__interest" data-testid="interest-line">
      Juros/multa pagos (já incluídos em “Pago”):
      <strong class="money">{{ formatCurrency(totals.interestPaid) }}</strong>
    </p>
  </section>
</template>

<style scoped>
.paid-pending__title {
  margin-bottom: 1rem;
}

.paid-pending__legend {
  list-style: none;
  margin: 1rem 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.paid-pending__item {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.paid-pending__dot {
  width: 0.75rem;
  height: 0.75rem;
  border-radius: 50%;
  flex: none;
}

.paid-pending__name {
  flex: 1;
  font-weight: 500;
}

.paid-pending__interest {
  margin: 0.75rem 0 0;
  font-size: 0.875rem;
  color: var(--gf-text-muted);
}
</style>
