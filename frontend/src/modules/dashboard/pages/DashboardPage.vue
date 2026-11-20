<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import EmptyState from '@/components/EmptyState.vue'
import FinancialCard from '@/components/FinancialCard.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useAuthStore } from '@/stores/auth'
import { useClockStore } from '@/stores/clock'
import type { TransactionDto } from '@/types/api'
import { formatDate } from '@/utils/format'
import { monthPeriod, validatePeriod, type Period } from '@/utils/period'
import CreditCardsSummary from '../components/CreditCardsSummary.vue'
import ExpensesByCategoryChart from '../components/ExpensesByCategoryChart.vue'
import MonthlyHistoryChart from '../components/MonthlyHistoryChart.vue'
import PeriodSelector from '../components/PeriodSelector.vue'
import UpcomingDuesList from '../components/UpcomingDuesList.vue'
import { useDashboard } from '../composables'
import { accountsWithoutBalanceMessage, isDashboardEmpty } from '../summary'

const auth = useAuthStore()
const clock = useClockStore()
const route = useRoute()
const router = useRouter()

const firstName = computed(() => auth.user?.name.trim().split(/\s+/)[0] ?? '')

function queryString(value: unknown): string | undefined {
  const first = Array.isArray(value) ? value[0] : value
  return typeof first === 'string' ? first : undefined
}

// O período fica na URL (mês atual por padrão): recarregar a página ou compartilhar o link preserva a visão.
const period = computed<Period>(() => {
  const from = queryString(route.query.from)
  const to = queryString(route.query.to)
  if (from && to && !validatePeriod({ from, to })) return { from, to }
  return monthPeriod(clock.today)
})

function setPeriod(next: Period): void {
  void router.replace({ query: { from: next.from, to: next.to } })
}

const dashboard = useDashboard(() => period.value)
const data = dashboard.data

const warning = computed(() =>
  data.value ? accountsWithoutBalanceMessage(data.value.balance.accountsWithoutBalance) : null,
)
const empty = computed(() => (data.value ? isDashboardEmpty(data.value) : false))
const resultTone = computed(() => {
  const result = data.value?.totals.result ?? 0
  return result > 0 ? 'positive' : result < 0 ? 'negative' : 'neutral'
})

function openTransaction(tx: TransactionDto): void {
  void router.push({ name: 'transactions', query: { open: tx.id } })
}
</script>

<template>
  <div>
    <PageHeader :title="firstName ? `Olá, ${firstName}` : 'Dashboard'" description="Visão geral das suas finanças.">
      <template #actions>
        <Button
          type="button"
          label="Nova movimentação"
          icon="pi pi-plus"
          @click="router.push({ name: 'transactions', query: { new: '1' } })"
        />
      </template>
    </PageHeader>

    <PeriodSelector
      class="dashboard__period"
      :model-value="period"
      :today="clock.today"
      @update:model-value="setPeriod"
    />

    <LoadingState v-if="dashboard.loading.value && !data" label="Carregando seu resumo…" />

    <Message v-else-if="dashboard.error.value && !data" severity="error" role="alert">
      <div class="dashboard__error">
        <span>{{ dashboard.error.value }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="dashboard.reload()" />
      </div>
    </Message>

    <template v-else-if="data">
      <Message v-if="dashboard.error.value" severity="error" role="alert" class="dashboard__block">
        <div class="dashboard__error">
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
        title="Comece cadastrando suas finanças"
        description="Crie uma conta, informe o saldo e registre suas receitas e despesas para ver o resumo aqui."
      >
        <Button
          type="button"
          label="Criar conta"
          icon="pi pi-building-columns"
          @click="router.push({ name: 'accounts' })"
        />
        <Button
          type="button"
          label="Registrar movimentação"
          icon="pi pi-plus"
          severity="secondary"
          @click="router.push({ name: 'transactions', query: { new: '1' } })"
        />
      </EmptyState>

      <template v-else>
        <Message v-if="warning" severity="warn" class="dashboard__block" data-testid="balance-warning">
          {{ warning }}
          <RouterLink :to="{ name: 'accounts' }">Ver contas</RouterLink>
        </Message>

        <section class="dashboard__grid dashboard__block" aria-label="Saldos" :aria-busy="dashboard.loading.value">
          <FinancialCard
            label="Saldo informado"
            icon="pi pi-wallet"
            :value="data.balance.reportedTotal"
            :caption="
              data.balance.accountsWithBalance > 0
                ? `Soma dos últimos saldos informados (${data.balance.accountsWithBalance} ${data.balance.accountsWithBalance === 1 ? 'conta' : 'contas'})`
                : 'Nenhuma conta com saldo informado'
            "
          />
          <FinancialCard
            label="Saldo projetado"
            icon="pi pi-chart-line"
            :value="data.balance.projectedTotal"
            projected
            :caption="`Projetado em ${formatDate(data.balance.projectedAsOf)}. Estimativa, não é o saldo bancário.`"
          />
        </section>

        <section
          class="dashboard__grid dashboard__block"
          aria-label="Resumo do período"
          :aria-busy="dashboard.loading.value"
        >
          <FinancialCard
            label="Receitas"
            icon="pi pi-arrow-up-right"
            tone="positive"
            :value="data.totals.income"
            caption="Previstas e recebidas no período"
          />
          <FinancialCard
            label="Despesas"
            icon="pi pi-arrow-down-right"
            tone="negative"
            :value="data.totals.expense"
            caption="Previstas e pagas no período"
          />
          <FinancialCard
            label="Resultado"
            icon="pi pi-equals"
            :tone="resultTone"
            :value="data.totals.result"
            caption="Receitas menos despesas"
          />
        </section>

        <section
          class="dashboard__grid dashboard__block"
          aria-label="Pagamentos e recebimentos"
          :aria-busy="dashboard.loading.value"
        >
          <FinancialCard label="Recebido" icon="pi pi-check-circle" :value="data.totals.receivedIncome" />
          <FinancialCard label="A receber" icon="pi pi-clock" :value="data.totals.pendingIncome" />
          <FinancialCard label="Pago" icon="pi pi-check-circle" :value="data.totals.paidExpense" />
          <FinancialCard label="A pagar" icon="pi pi-clock" :value="data.totals.pendingExpense" />
        </section>

        <div class="dashboard__two dashboard__block">
          <section class="surface-card" aria-labelledby="chart-categories">
            <h2 id="chart-categories" class="dashboard__title">Despesas por categoria</h2>
            <ExpensesByCategoryChart :items="data.expensesByCategory" :loading="dashboard.loading.value" />
          </section>
          <section class="surface-card" aria-labelledby="chart-monthly">
            <h2 id="chart-monthly" class="dashboard__title">Últimos 6 meses</h2>
            <MonthlyHistoryChart :months="data.monthlyHistory" :loading="dashboard.loading.value" />
          </section>
        </div>

        <section class="surface-card dashboard__block" aria-labelledby="cards-title">
          <h2 id="cards-title" class="dashboard__title">Resumo dos cartões</h2>
          <CreditCardsSummary :cards="data.creditCards ?? []" />
        </section>

        <section class="surface-card dashboard__block" aria-labelledby="upcoming-title">
          <h2 id="upcoming-title" class="dashboard__title">Próximos vencimentos</h2>
          <UpcomingDuesList :items="data.upcomingDues" :today="data.today" @open="openTransaction" />
        </section>
      </template>
    </template>
  </div>
</template>

<style scoped>
.dashboard__period {
  margin-bottom: 1.5rem;
}

.dashboard__block {
  margin-bottom: 1.5rem;
}

.dashboard__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 15rem), 1fr));
  gap: 1rem;
}

.dashboard__two {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 1.5rem;
}

@media (min-width: 1280px) {
  .dashboard__two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

.dashboard__title {
  font-size: 1.0625rem;
  font-weight: 600;
  margin-bottom: 1rem;
}

.dashboard__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
