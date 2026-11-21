<script setup lang="ts">
import { computed } from 'vue'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import { colorFor, describeShares, donutOption } from '@/modules/reports/charts'
import { formatCurrency, formatPercent } from '@/utils/format'
import { PAID_BY_COPY, paidByRows } from '../dashboard'
import type { GroupPaidByItemDto } from '../types'

/** "Contas Pagas": quem pagou (principal + juros/multa) e quanto (percentual sobre o total pago). */
const props = defineProps<{ items: GroupPaidByItemDto[] }>()

const rows = computed(() => paidByRows(props.items).map((row, index) => ({ ...row, color: colorFor(index) })))
const option = computed(() =>
  donutOption(rows.value.map((r) => ({ name: r.name, value: r.paidAmount, color: r.color }))),
)
const ariaLabel = computed(() =>
  describeShares(
    PAID_BY_COPY.title,
    rows.value.map((r) => ({ name: r.name, total: r.paidAmount, percentage: r.percent })),
  ),
)
</script>

<template>
  <section class="surface-card paid-by" aria-labelledby="paid-by-title" data-testid="paid-by-card">
    <h3 id="paid-by-title" class="report-title">{{ PAID_BY_COPY.title }}</h3>

    <ChartPanel
      :option="option"
      v-bind="{ ariaLabel }"
      height="240px"
      :empty="rows.length === 0"
      empty-message="Nenhuma conta paga no período."
    >
      <template #table>
        <div class="table-scroll">
          <table class="simple-table" data-testid="paid-by-table">
            <caption class="sr-only">
              Contas pagas por participante: valor da conta, juros/multa, total pago e percentual
            </caption>
            <thead>
              <tr>
                <th scope="col">Quem pagou</th>
                <th scope="col" class="num">Valor das contas</th>
                <th scope="col" class="num">Juros/multa</th>
                <th scope="col" class="num">Total pago</th>
                <th scope="col" class="num">%</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.userId">
                <th scope="row">{{ row.name }}</th>
                <td class="num money">{{ formatCurrency(row.principalAmount) }}</td>
                <td class="num money">{{ formatCurrency(row.interestAmount) }}</td>
                <td class="num money">{{ formatCurrency(row.paidAmount) }}</td>
                <td class="num">{{ formatPercent(row.percent) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </ChartPanel>

    <ul v-if="rows.length > 0" class="paid-by__legend" aria-label="Legenda: quem pagou">
      <li v-for="row in rows" :key="row.userId" class="paid-by__item" data-testid="paid-by-item">
        <span class="paid-by__dot" :style="{ background: row.color }" aria-hidden="true" />
        <span class="paid-by__name">{{ row.name }}</span>
        <span class="paid-by__value money">{{ formatCurrency(row.paidAmount) }}</span>
        <span class="paid-by__percent">{{ formatPercent(row.percent) }}</span>
        <span v-if="row.interestAmount > 0" class="paid-by__interest">
          inclui {{ formatCurrency(row.interestAmount) }} de juros/multa
        </span>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.paid-by__legend {
  list-style: none;
  margin: 1rem 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.paid-by__item {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem 0.75rem;
  align-items: center;
}

.paid-by__dot {
  width: 0.75rem;
  height: 0.75rem;
  border-radius: 50%;
  flex: none;
}

.paid-by__name {
  flex: 1 1 8rem;
  min-width: 0;
  overflow-wrap: anywhere;
  font-weight: 500;
}

.paid-by__percent {
  min-width: 3.5rem;
  text-align: right;
  color: var(--gf-text-muted);
}

.paid-by__interest {
  flex-basis: 100%;
  padding-left: 1.5rem;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}
</style>
