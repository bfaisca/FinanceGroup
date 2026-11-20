<script setup lang="ts">
import { computed } from 'vue'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import { incomeExpenseOption } from '@/modules/reports/charts'
import { formatCurrency } from '@/utils/format'
import { formatMonthLong, formatMonthShort } from '@/utils/period'
import type { GroupDashboardDto } from '../types'

const props = defineProps<{ months: GroupDashboardDto['monthly'] }>()

const hasData = computed(() => props.months.some((m) => m.income !== 0 || m.expenses !== 0))
const option = computed(() =>
  incomeExpenseOption(
    props.months.map((m) => ({ label: formatMonthShort(m.month), income: m.income, expense: m.expenses })),
  ),
)
const ariaLabel = computed(
  () =>
    `Gráfico de barras da evolução mensal de receitas e despesas do grupo. ${props.months
      .map(
        (m) =>
          `${formatMonthLong(m.month)}: receitas ${formatCurrency(m.income)}, despesas ${formatCurrency(m.expenses)}`,
      )
      .join('; ')}.`,
)
</script>

<template>
  <section class="surface-card" aria-labelledby="group-monthly-title">
    <h3 id="group-monthly-title" class="report-title group-monthly__title">Evolução mensal</h3>
    <ChartPanel
      :option="option"
      v-bind="{ ariaLabel }"
      height="300px"
      :empty="!hasData"
      empty-message="Sem movimentações nos meses exibidos."
    >
      <template #table>
        <div class="table-scroll">
          <table class="simple-table" data-testid="group-monthly-table">
            <caption class="sr-only">
              Evolução mensal de receitas e despesas do grupo
            </caption>
            <thead>
              <tr>
                <th scope="col">Mês</th>
                <th scope="col" class="num">Receitas</th>
                <th scope="col" class="num">Despesas</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="m in months" :key="m.month">
                <th scope="row" class="group-monthly__month">{{ formatMonthLong(m.month) }}</th>
                <td class="num money">{{ formatCurrency(m.income) }}</td>
                <td class="num money">{{ formatCurrency(m.expenses) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </ChartPanel>
  </section>
</template>

<style scoped>
.group-monthly__title {
  margin-bottom: 1rem;
}

.group-monthly__month {
  text-transform: lowercase;
}

.group-monthly__month::first-letter {
  text-transform: uppercase;
}
</style>
