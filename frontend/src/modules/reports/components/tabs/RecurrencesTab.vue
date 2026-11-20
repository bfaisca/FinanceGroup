<script setup lang="ts">
import { computed } from 'vue'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import TransactionAmount from '@/modules/transactions/components/TransactionAmount.vue'
import { formatCurrency, formatDate } from '@/utils/format'
import { formatMonthLong, formatMonthShort } from '@/utils/period'
import { frequencyLabel } from '@/utils/recurrence'
import { incomeExpenseOption } from '../../charts'
import { useReport } from '../../composables'
import { reportService } from '../../services/reportService'
import ReportState from '../ReportState.vue'

const report = useReport(
  () => reportService.recurrences(),
  () => 'recurrences',
)
const data = report.data

const months = computed(() => data.value?.upcomingByMonth ?? [])
const isEmpty = computed(() => data.value !== null && data.value.activeRules === 0 && data.value.rules.length === 0)
const hasUpcoming = computed(() => months.value.some((m) => m.income !== 0 || m.expense !== 0))
const option = computed(() =>
  incomeExpenseOption(
    months.value.map((m) => ({ label: formatMonthShort(m.month), income: m.income, expense: m.expense })),
  ),
)
const ariaLabel = computed(
  () =>
    `Gráfico de barras do previsto por mês das recorrências. ${months.value
      .map(
        (m) =>
          `${formatMonthLong(m.month)}: receitas ${formatCurrency(m.income)}, despesas ${formatCurrency(m.expense)}`,
      )
      .join('; ')}.`,
)
</script>

<template>
  <section class="surface-card" aria-labelledby="rec-report-title">
    <div class="report-header">
      <h2 id="rec-report-title" class="report-title">Recorrências</h2>
    </div>
    <p class="report-note">
      Considera apenas as ocorrências já geradas, não canceladas, nos próximos 12 meses. Não há estimativas.
    </p>
    <ReportState
      :loading="report.loading.value"
      :error="report.error.value"
      :loaded="data !== null"
      :empty="isEmpty"
      empty-title="Nenhuma recorrência cadastrada"
      empty-description="Cadastre receitas e despesas recorrentes para ver o previsto por mês."
      @retry="report.reload()"
    >
      <div v-if="data" class="stack">
        <div class="rec-report__cards">
          <article class="rec-report__count">
            <p class="rec-report__count-label">Regras ativas</p>
            <p class="rec-report__count-value" data-testid="active-rules">{{ data.activeRules }}</p>
          </article>
        </div>

        <ChartPanel
          :option="option"
          v-bind="{ ariaLabel }"
          :empty="!hasUpcoming"
          empty-message="Nenhuma ocorrência prevista."
        >
          <template #table>
            <table class="simple-table" data-testid="upcoming-table">
              <caption class="sr-only">
                Previsto por mês das recorrências
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
                  <th scope="row">{{ formatMonthLong(m.month) }}</th>
                  <td class="num money">{{ formatCurrency(m.income) }}</td>
                  <td class="num money">{{ formatCurrency(m.expense) }}</td>
                </tr>
              </tbody>
            </table>
          </template>
        </ChartPanel>

        <section aria-labelledby="rec-rules-title">
          <h3 id="rec-rules-title" class="report-title">Regras</h3>
          <div class="table-scroll">
            <table class="simple-table" data-testid="rules-table">
              <caption class="sr-only">
                Regras de recorrência e total previsto em 12 meses
              </caption>
              <thead>
                <tr>
                  <th scope="col">Descrição</th>
                  <th scope="col" class="num">Valor</th>
                  <th scope="col">Frequência</th>
                  <th scope="col">Próxima</th>
                  <th scope="col" class="num">Previsto em 12 meses</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="rule in data.rules" :key="rule.id">
                  <th scope="row">
                    <RouterLink :to="{ name: 'recurrences', query: { open: rule.id } }">{{
                      rule.description
                    }}</RouterLink>
                  </th>
                  <td class="num"><TransactionAmount :amount="rule.amount" :type="rule.type" /></td>
                  <td>{{ frequencyLabel(rule.frequency, rule.interval) }}</td>
                  <td>{{ formatDate(rule.nextOccurrenceDate) }}</td>
                  <td class="num money">{{ formatCurrency(rule.upcomingTotal12m) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </ReportState>
  </section>
</template>

<style scoped>
.rec-report__cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 14rem), 1fr));
  gap: 1rem;
}

.rec-report__count {
  padding: 1.25rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.rec-report__count-label {
  margin: 0;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--gf-text-muted);
}

.rec-report__count-value {
  margin: 0.5rem 0 0;
  font-size: 1.5rem;
  font-weight: 700;
}
</style>
