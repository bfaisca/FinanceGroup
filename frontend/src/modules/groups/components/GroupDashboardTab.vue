<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import EmptyState from '@/components/EmptyState.vue'
import FinancialCard from '@/components/FinancialCard.vue'
import LoadingState from '@/components/LoadingState.vue'
import MonthsSelect from '@/components/MonthsSelect.vue'
import PeriodSelector from '@/modules/dashboard/components/PeriodSelector.vue'
import { useClockStore } from '@/stores/clock'
import { monthPeriod, validatePeriod, type Period } from '@/utils/period'
import { useResource } from '../composables'
import { isGroupDashboardEmpty } from '../dashboard'
import { useGroupRefreshHandler } from '../refresh'
import { groupDashboardService } from '../services/groupDashboardService'
import type { GroupDetailDto } from '../types'
import GroupCategoryBreakdown from './GroupCategoryBreakdown.vue'
import GroupMonthlyChart from './GroupMonthlyChart.vue'
import GroupPaidByCard from './GroupPaidByCard.vue'
import GroupPaidPendingCard from './GroupPaidPendingCard.vue'
import GroupPeriodComparison from './GroupPeriodComparison.vue'

const props = defineProps<{ group: GroupDetailDto }>()

const route = useRoute()
const router = useRouter()
const clock = useClockStore()

const DEFAULT_MONTHS = 6
const MONTHS_OPTIONS = [3, 6, 12, 24, 36] as const

function queryString(value: unknown): string | undefined {
  const first = Array.isArray(value) ? value[0] : value
  return typeof first === 'string' ? first : undefined
}

// O período fica na URL (mês atual por padrão): recarregar ou compartilhar o link preserva a visão.
const period = computed<Period>(() => {
  const from = queryString(route.query.from)
  const to = queryString(route.query.to)
  if (from && to && !validatePeriod({ from, to })) return { from, to }
  return monthPeriod(clock.today)
})

const months = computed(() => {
  const n = Number(queryString(route.query.months))
  return (MONTHS_OPTIONS as readonly number[]).includes(n) ? n : DEFAULT_MONTHS
})

function patchQuery(patch: Record<string, string | undefined>): void {
  const next: Record<string, string> = {}
  for (const [key, value] of Object.entries({ ...route.query, ...patch })) {
    const v = queryString(value)
    if (v !== undefined) next[key] = v
  }
  void router.replace({ query: next })
}

const dashboard = useResource(
  () =>
    groupDashboardService.get(props.group.id, {
      from: period.value.from,
      to: period.value.to,
      months: months.value === DEFAULT_MONTHS ? undefined : months.value,
    }),
  () => `${props.group.id}|${period.value.from}|${period.value.to}|${months.value}`,
)
const data = dashboard.data

useGroupRefreshHandler(async () => {
  await dashboard.reload()
  return dashboard.error.value === null
})

const empty = computed(() => (data.value ? isGroupDashboardEmpty(data.value) : false))
</script>

<template>
  <div class="gdash">
    <div class="gdash__filters">
      <PeriodSelector
        :model-value="period"
        :today="clock.today"
        @update:model-value="patchQuery({ from: $event.from, to: $event.to })"
      />
      <MonthsSelect
        :model-value="months"
        :options="MONTHS_OPTIONS"
        label="Evolução mensal"
        aria-label="Quantidade de meses da evolução mensal"
        @update:model-value="patchQuery({ months: $event === DEFAULT_MONTHS ? undefined : String($event) })"
      />
    </div>

    <LoadingState v-if="dashboard.loading.value && !data" label="Carregando resumo do grupo…" />

    <Message v-else-if="dashboard.error.value && !data" severity="error" role="alert">
      <div class="gdash__error">
        <span>{{ dashboard.error.value }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="dashboard.reload()" />
      </div>
    </Message>

    <template v-else-if="data">
      <Message v-if="dashboard.error.value" severity="error" role="alert" class="gdash__block">
        <div class="gdash__error">
          <span>Não foi possível atualizar: {{ dashboard.error.value }}</span>
          <Button
            type="button"
            label="Tentar novamente"
            icon="pi pi-refresh"
            size="small"
            @click="dashboard.reload()"
          />
        </div>
      </Message>

      <EmptyState
        v-if="empty"
        icon="pi pi-chart-line"
        title="Sem movimentações neste período"
        description="Escolha outro período ou cadastre movimentações compartilhadas na aba Movimentações."
      />

      <template v-else>
        <section class="gdash__grid gdash__block" aria-label="Resumo do período" :aria-busy="dashboard.loading.value">
          <FinancialCard
            label="Total de despesas"
            icon="pi pi-arrow-down-right"
            tone="negative"
            :value="data.totals.expenses"
            caption="Valor original das contas do período"
          />
          <FinancialCard
            label="Pago"
            icon="pi pi-check-circle"
            :value="data.totals.expensesPaid"
            caption="Efetivamente pago (inclui juros/multa)"
          />
          <FinancialCard
            label="Pendente"
            icon="pi pi-clock"
            :value="data.totals.expensesPending"
            caption="Ainda a pagar (valor das contas)"
          />
          <FinancialCard
            label="Juros/multa"
            icon="pi pi-percentage"
            :value="data.totals.interestPaid"
            caption="Pagos além do valor das contas"
          />
        </section>

        <h2 class="gdash__heading">Pagamentos</h2>
        <div class="gdash__two gdash__block">
          <GroupPaidPendingCard :totals="data.totals" />
          <GroupPaidByCard :items="data.paidBy" />
        </div>

        <div class="gdash__two gdash__block">
          <GroupCategoryBreakdown :items="data.expensesByCategory" />
          <GroupMonthlyChart :months="data.monthly" />
        </div>

        <div class="gdash__block">
          <GroupPeriodComparison :current="data.period" :totals="data.totals" :previous="data.previousPeriod" />
        </div>
      </template>
    </template>
  </div>
</template>

<style scoped>
.gdash__filters {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem 2rem;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.gdash__block {
  margin-bottom: 1.5rem;
}

.gdash__heading {
  font-size: 1.125rem;
  font-weight: 600;
  margin-bottom: 0.75rem;
}

.gdash__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 15rem), 1fr));
  gap: 1rem;
}

.gdash__two {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 1.5rem;
}

@media (min-width: 1280px) {
  .gdash__two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

.gdash__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
