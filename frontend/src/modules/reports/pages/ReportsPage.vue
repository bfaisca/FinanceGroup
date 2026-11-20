<script setup lang="ts">
import { computed, defineAsyncComponent } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Message from 'primevue/message'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import PeriodSelector from '@/modules/dashboard/components/PeriodSelector.vue'
import { useClockStore } from '@/stores/clock'
import { monthPeriod, validatePeriod, type Period } from '@/utils/period'
import {
  DEFAULT_MONTHS,
  isReportTab,
  MONTHS_TABS,
  parseMonths,
  PERIOD_TABS,
  REPORT_TABS,
  type ReportTab,
} from '../tabs'

// Cada aba é carregada sob demanda (código e dados): o bundle inicial da página permanece enxuto.
const asyncTab = (loader: () => Promise<unknown>) =>
  defineAsyncComponent({ loader: loader as never, loadingComponent: LoadingState, delay: 0 })

const SummaryTab = asyncTab(() => import('../components/tabs/SummaryTab.vue'))
const CategoryTab = asyncTab(() => import('../components/tabs/CategoryTab.vue'))
const AccountTab = asyncTab(() => import('../components/tabs/AccountTab.vue'))
const MethodTab = asyncTab(() => import('../components/tabs/MethodTab.vue'))
const EvolutionTab = asyncTab(() => import('../components/tabs/EvolutionTab.vue'))
const InstallmentsTab = asyncTab(() => import('../components/tabs/InstallmentsTab.vue'))
const RecurrencesTab = asyncTab(() => import('../components/tabs/RecurrencesTab.vue'))

const route = useRoute()
const router = useRouter()
const clock = useClockStore()

function queryString(value: unknown): string | undefined {
  const first = Array.isArray(value) ? value[0] : value
  return typeof first === 'string' ? first : undefined
}

const tab = computed<ReportTab>(() => {
  const value = queryString(route.query.tab)
  return isReportTab(value) ? value : 'summary'
})

// Período na URL (mês atual por padrão); inválido ou > 366 dias volta ao padrão.
const period = computed<Period>(() => {
  const from = queryString(route.query.from)
  const to = queryString(route.query.to)
  if (from && to && !validatePeriod({ from, to })) return { from, to }
  return monthPeriod(clock.today)
})
const months = computed(() => parseMonths(queryString(route.query.months)))

const usesPeriod = computed(() => (PERIOD_TABS as readonly string[]).includes(tab.value))
const usesMonths = computed(() => (MONTHS_TABS as readonly string[]).includes(tab.value))

function patchQuery(patch: Record<string, string | undefined>): void {
  const next: Record<string, string> = {}
  for (const [key, value] of Object.entries({ ...route.query, ...patch })) {
    const v = queryString(value)
    if (v !== undefined) next[key] = v
  }
  void router.replace({ query: next })
}

function setTab(value: string | number): void {
  if (isReportTab(value)) patchQuery({ tab: value === 'summary' ? undefined : value })
}

function setPeriod(next: Period): void {
  patchQuery({ from: next.from, to: next.to })
}

function setMonths(value: number): void {
  patchQuery({ months: value === DEFAULT_MONTHS ? undefined : String(value) })
}
</script>

<template>
  <div>
    <PageHeader
      title="Relatórios"
      description="Análises das suas finanças individuais por período, categoria, conta e muito mais."
    />

    <PeriodSelector
      v-if="usesPeriod"
      class="reports-page__filter"
      :model-value="period"
      :today="clock.today"
      @update:model-value="setPeriod"
    />

    <Tabs :value="tab" scrollable @update:value="setTab">
      <TabList>
        <Tab v-for="t in REPORT_TABS" :key="t.value" :value="t.value">{{ t.label }}</Tab>
      </TabList>
      <TabPanels>
        <TabPanel v-for="t in REPORT_TABS" :key="t.value" :value="t.value">
          <template v-if="tab === t.value">
            <SummaryTab v-if="t.value === 'summary'" :from="period.from" :to="period.to" />
            <CategoryTab v-else-if="t.value === 'categories'" :from="period.from" :to="period.to" />
            <AccountTab v-else-if="t.value === 'accounts'" :from="period.from" :to="period.to" />
            <MethodTab v-else-if="t.value === 'methods'" :from="period.from" :to="period.to" />
            <EvolutionTab v-else-if="t.value === 'evolution'" :months="months" @update:months="setMonths" />
            <InstallmentsTab v-else-if="t.value === 'installments'" :months="months" @update:months="setMonths" />
            <RecurrencesTab v-else />
          </template>
        </TabPanel>
      </TabPanels>
    </Tabs>

    <Message v-if="usesMonths" severity="secondary" class="reports-page__note" :closable="false">
      A janela em meses usa o mês atual como referência.
    </Message>

    <p class="reports-page__footnote">
      <i class="pi pi-info-circle" aria-hidden="true" />
      Estes relatórios são individuais. A visão consolidada de cada grupo fica no dashboard do grupo (menu Grupos).
    </p>
  </div>
</template>

<style scoped>
.reports-page__filter {
  margin-bottom: 1rem;
}

.reports-page__note {
  margin-top: 1rem;
}

.reports-page__footnote {
  margin: 1.5rem 0 0;
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}
</style>
