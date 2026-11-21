<script setup lang="ts">
import { formatCurrency, formatDate } from '@/utils/format'
import { formatChange } from '../dashboard'
import type { GroupDashboardDto } from '../types'

defineProps<{
  current: GroupDashboardDto['period']
  totals: GroupDashboardDto['totals']
  previous: GroupDashboardDto['previousPeriod']
}>()
</script>

<template>
  <section class="surface-card" aria-labelledby="group-compare-title">
    <h3 id="group-compare-title" class="report-title compare__title">Comparação com o período anterior</h3>
    <p class="report-note">
      Período atual: {{ formatDate(current.from) }} a {{ formatDate(current.to) }}. Período anterior (mesma duração):
      {{ formatDate(previous.from) }} a {{ formatDate(previous.to) }}.
    </p>
    <div class="table-scroll">
      <table class="simple-table" data-testid="group-compare-table">
        <caption class="sr-only">
          Comparação de despesas com o período anterior
        </caption>
        <thead>
          <tr>
            <th scope="col">Indicador</th>
            <th scope="col" class="num">Período atual</th>
            <th scope="col" class="num">Período anterior</th>
            <th scope="col" class="num">Variação</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <th scope="row">Despesas</th>
            <td class="num money">{{ formatCurrency(totals.expenses) }}</td>
            <td class="num money">{{ formatCurrency(previous.expenses) }}</td>
            <td class="num" data-testid="expenses-change">{{ formatChange(previous.expensesChangePercent) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <p class="report-note compare__note">
      “—” indica que o período anterior não teve valores, então não há base para calcular a variação.
    </p>
  </section>
</template>

<style scoped>
.compare__title {
  margin-bottom: 0.5rem;
}

.compare__note {
  margin: 0.75rem 0 0;
}
</style>
