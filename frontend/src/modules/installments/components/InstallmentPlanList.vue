<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable, { type DataTableRowClickEvent } from 'primevue/datatable'
import Message from 'primevue/message'
import Paginator, { type PageState } from 'primevue/paginator'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import { BREAKPOINTS, useMediaQuery } from '@/composables/useMediaQuery'
import type { InstallmentPlanDto } from '@/types/api'
import { formatCurrency, formatDate } from '@/utils/format'
import InstallmentStatusChip from './InstallmentStatusChip.vue'
import PlanProgress from './PlanProgress.vue'

const props = withDefaults(
  defineProps<{
    items: InstallmentPlanDto[]
    loading?: boolean
    error?: string | null
    totalCount: number
    page: number
    pageSize: number
    filtered?: boolean
  }>(),
  { loading: false, error: null, filtered: false },
)

const emit = defineEmits<{
  open: [plan: InstallmentPlanDto]
  create: []
  'update:page': [page: number]
  retry: []
}>()

const isTabletUp = useMediaQuery(BREAKPOINTS.tablet)
const first = computed(() => (props.page - 1) * props.pageSize)
const showInitialLoading = computed(() => props.loading && props.items.length === 0)

function onRowClick(event: DataTableRowClickEvent): void {
  emit('open', event.data as InstallmentPlanDto)
}

function onPage(event: PageState): void {
  emit('update:page', event.page + 1)
}

function source(plan: InstallmentPlanDto): string {
  return plan.creditCardName ?? plan.accountName ?? '—'
}
</script>

<template>
  <div :aria-busy="loading">
    <Message v-if="error" severity="error" role="alert" class="plan-list__error">
      <div class="plan-list__error-content">
        <span>{{ error }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="emit('retry')" />
      </div>
    </Message>

    <LoadingState v-if="showInitialLoading" label="Carregando parcelamentos…" />

    <EmptyState
      v-else-if="items.length === 0 && !error"
      icon="pi pi-list-check"
      :title="filtered ? 'Nenhum parcelamento encontrado' : 'Nenhum parcelamento ainda'"
      :description="
        filtered
          ? 'Nenhum plano com a situação selecionada.'
          : 'Registre uma compra parcelada: o sistema gera as parcelas e você acompanha o que já foi pago e o que ainda falta.'
      "
    >
      <Button v-if="!filtered" type="button" label="Novo parcelamento" icon="pi pi-plus" @click="emit('create')" />
    </EmptyState>

    <template v-else-if="items.length > 0">
      <DataTable
        v-if="isTabletUp"
        :value="items"
        data-key="id"
        :loading="loading"
        row-hover
        class="plan-list__table"
        @row-click="onRowClick"
      >
        <Column header="Descrição">
          <template #body="{ data }: { data: InstallmentPlanDto }">
            <button type="button" class="plan-list__link" @click="emit('open', data)">{{ data.description }}</button>
            <small class="plan-list__muted">{{ data.categoryName }} · {{ source(data) }}</small>
          </template>
        </Column>
        <Column header="Progresso">
          <template #body="{ data }: { data: InstallmentPlanDto }">
            <PlanProgress
              :paid-count="data.paidCount"
              :installment-count="data.installmentCount"
              :description="data.description"
            />
          </template>
        </Column>
        <Column header="Total" header-class="plan-list__right" body-class="plan-list__right">
          <template #body="{ data }: { data: InstallmentPlanDto }">
            <span class="money">{{ formatCurrency(data.totalAmount) }}</span>
          </template>
        </Column>
        <Column header="Restante" header-class="plan-list__right" body-class="plan-list__right">
          <template #body="{ data }: { data: InstallmentPlanDto }">
            <span class="money">{{ formatCurrency(data.remainingAmount) }}</span>
          </template>
        </Column>
        <Column header="Próximo vencimento">
          <template #body="{ data }: { data: InstallmentPlanDto }">{{ formatDate(data.nextDueDate) }}</template>
        </Column>
        <Column header="Situação">
          <template #body="{ data }: { data: InstallmentPlanDto }">
            <InstallmentStatusChip :status="data.status" />
          </template>
        </Column>
        <Column header="Ações" body-class="plan-list__actions">
          <template #body="{ data }: { data: InstallmentPlanDto }">
            <Button
              type="button"
              icon="pi pi-eye"
              label="Detalhes"
              size="small"
              severity="secondary"
              text
              :aria-label="`Ver detalhes do parcelamento ${data.description}`"
              @click="emit('open', data)"
            />
          </template>
        </Column>
      </DataTable>

      <ul v-else class="plan-cards" aria-label="Parcelamentos">
        <li v-for="plan in items" :key="plan.id">
          <button
            type="button"
            class="plan-card"
            :aria-label="`Abrir parcelamento: ${plan.description}, ${formatCurrency(plan.totalAmount)}`"
            @click="emit('open', plan)"
          >
            <span class="plan-card__row">
              <span class="plan-card__title">{{ plan.description }}</span>
              <InstallmentStatusChip :status="plan.status" />
            </span>
            <PlanProgress
              :paid-count="plan.paidCount"
              :installment-count="plan.installmentCount"
              :description="plan.description"
            />
            <span class="plan-card__meta">
              <span>Total: {{ formatCurrency(plan.totalAmount) }}</span>
              <span>Restante: {{ formatCurrency(plan.remainingAmount) }}</span>
              <span v-if="plan.nextDueDate">Próximo vencimento: {{ formatDate(plan.nextDueDate) }}</span>
              <span>{{ source(plan) }}</span>
            </span>
          </button>
        </li>
      </ul>

      <Paginator
        class="plan-list__paginator"
        :first="first"
        :rows="pageSize"
        :total-records="totalCount"
        template="FirstPageLink PrevPageLink CurrentPageReport NextPageLink LastPageLink"
        current-page-report-template="{first}–{last} de {totalRecords}"
        @page="onPage"
      />
    </template>
  </div>
</template>

<style scoped>
.plan-list__error {
  margin-bottom: 1rem;
}

.plan-list__error-content {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.plan-list__link {
  display: block;
  padding: 0;
  border: 0;
  background: none;
  color: inherit;
  font: inherit;
  font-weight: 600;
  text-align: left;
  cursor: pointer;
}

.plan-list__link:hover {
  text-decoration: underline;
}

.plan-list__muted {
  color: var(--gf-text-muted);
}

:deep(.plan-list__right) {
  text-align: right;
}

:deep(.plan-list__right .p-datatable-column-header-content) {
  justify-content: flex-end;
}

:deep(.p-datatable-tbody > tr) {
  cursor: pointer;
}

.plan-cards {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.plan-card {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
  width: 100%;
  padding: 0.875rem 1rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.plan-card:hover {
  background: var(--p-content-hover-background);
}

.plan-card__row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  justify-content: space-between;
}

.plan-card__title {
  font-weight: 600;
  overflow-wrap: anywhere;
}

.plan-card__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem 1rem;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.plan-list__paginator {
  margin-top: 0.75rem;
  flex-wrap: wrap;
}
</style>
