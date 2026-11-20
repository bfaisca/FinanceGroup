<script setup lang="ts">
import { computed } from 'vue'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import MonthsSelect from '@/components/MonthsSelect.vue'
import { formatCurrency } from '@/utils/format'
import { formatMonthLong, formatMonthShort } from '@/utils/period'
import { incomeExpenseOption } from '../../charts'
import { useReport } from '../../composables'
import { reportService } from '../../services/reportService'
import ReportState from '../ReportState.vue'

const props = defineProps<{ months: number }>()
const emit = defineEmits<{ 'update:months': [months: number] }>()

const report = useReport(
  () => reportService.monthlyEvolution({ months: props.months }),
  () => String(props.months),
)
const data = report.data

const items = computed(() => data.value?.items ?? [])
const isEmpty = computed(() => items.value.every((i) => i.income === 0 && i.expense === 0))
const option = computed(() =>
  incomeExpenseOption(
    items.value.map((i) => ({ label: formatMonthShort(i.month), income: i.income, expense: i.expense })),
    { name: 'Resultado acumulado', values: items.value.map((i) => i.cumulativeResult) },
  ),
)
const ariaLabel = computed(
  () =>
    `Gráfico de receitas e despesas por mês, com linha do resultado acumulado. ${items.value
      .map(
        (i) =>
          `${formatMonthLong(i.month)}: receitas ${formatCurrency(i.income)}, despesas ${formatCurrency(i.expense)}, resultado acumulado ${formatCurrency(i.cumulativeResult)}`,
      )
      .join('; ')}.`,
)
</script>

<template>
  <section class="surface-card" aria-labelledby="evolution-title">
    <div class="report-header">
      <h2 id="evolution-title" class="report-title">Evolução mensal</h2>
      <MonthsSelect
        :model-value="months"
        label="Período"
        aria-label="Meses da evolução"
        @update:model-value="emit('update:months', $event)"
      />
    </div>
    <p class="report-note">O resultado acumulado é a soma do resultado mensal dentro da janela escolhida.</p>
    <ReportState
      :loading="report.loading.value"
      :error="report.error.value"
      :loaded="data !== null"
      :empty="isEmpty"
      empty-title="Nenhuma movimentação nos meses selecionados"
      @retry="report.reload()"
    >
      <ChartPanel :option="option" v-bind="{ ariaLabel }">
        <template #table>
          <table class="simple-table" data-testid="evolution-table">
            <caption class="sr-only">
              Evolução mensal de receitas, despesas, resultado e resultado acumulado
            </caption>
            <thead>
              <tr>
                <th scope="col">Mês</th>
                <th scope="col" class="num">Receitas</th>
                <th scope="col" class="num">Despesas</th>
                <th scope="col" class="num">Resultado</th>
                <th scope="col" class="num">Acumulado</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in items" :key="i.month">
                <th scope="row">{{ formatMonthLong(i.month) }}</th>
                <td class="num money">{{ formatCurrency(i.income) }}</td>
                <td class="num money">{{ formatCurrency(i.expense) }}</td>
                <td class="num money">{{ formatCurrency(i.result) }}</td>
                <td class="num money">{{ formatCurrency(i.cumulativeResult) }}</td>
              </tr>
            </tbody>
          </table>
        </template>
      </ChartPanel>
    </ReportState>
  </section>
</template>
