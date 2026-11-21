<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable, { type DataTableRowClickEvent, type DataTableSortEvent } from 'primevue/datatable'
import Message from 'primevue/message'
import Paginator, { type PageState } from 'primevue/paginator'
import CategoryBadge from '@/components/CategoryBadge.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import { BREAKPOINTS, useMediaQuery } from '@/composables/useMediaQuery'
import { PAGE_SIZE_OPTIONS } from '@/modules/transactions/filters'
import TransactionAmount from '@/modules/transactions/components/TransactionAmount.vue'
import TransactionStatusChip from '@/modules/transactions/components/TransactionStatusChip.vue'
import { formatCurrency, formatDate } from '@/utils/format'
import type { GroupTransactionDto } from '../types'
import GroupPaidSummary from './GroupPaidSummary.vue'
import GroupSeriesBadge from './GroupSeriesBadge.vue'

const props = withDefaults(
  defineProps<{
    items: GroupTransactionDto[]
    loading?: boolean
    error?: string | null
    totalCount: number
    page: number
    pageSize: number
    sort: string
    /** Há filtros ativos (muda a mensagem do estado vazio). */
    filtered?: boolean
  }>(),
  { loading: false, error: null, filtered: false },
)

const emit = defineEmits<{
  open: [transaction: GroupTransactionDto]
  'update:page': [page: number]
  'update:pageSize': [pageSize: number]
  'update:sort': [sort: string]
  retry: []
}>()

const isTabletUp = useMediaQuery(BREAKPOINTS.tablet)

const sortField = computed(() => (props.sort.startsWith('-') ? props.sort.slice(1) : props.sort))
const sortOrder = computed(() => (props.sort.startsWith('-') ? -1 : 1))
const first = computed(() => (props.page - 1) * props.pageSize)
const showInitialLoading = computed(() => props.loading && props.items.length === 0)

function onSort(event: DataTableSortEvent): void {
  if (typeof event.sortField !== 'string') return
  emit('update:sort', event.sortOrder === -1 ? `-${event.sortField}` : event.sortField)
}

function onRowClick(event: DataTableRowClickEvent): void {
  emit('open', event.data as GroupTransactionDto)
}

function onPage(event: PageState): void {
  if (event.rows !== props.pageSize) emit('update:pageSize', event.rows)
  else emit('update:page', event.page + 1)
}

function showRemaining(tx: GroupTransactionDto): boolean {
  return tx.status !== 'Canceled' && tx.paidAmount > 0 && tx.remainingAmount > 0
}

function summary(tx: GroupTransactionDto): string {
  return `${tx.description}, ${formatCurrency(tx.amount)}`
}
</script>

<template>
  <div class="gtx-list" :aria-busy="loading">
    <Message v-if="error" severity="error" role="alert" class="gtx-list__error">
      <div class="gtx-list__error-content">
        <span>{{ error }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="emit('retry')" />
      </div>
    </Message>

    <LoadingState v-if="showInitialLoading" label="Carregando movimentações do grupo…" />

    <EmptyState
      v-else-if="items.length === 0 && !error"
      icon="pi pi-arrow-right-arrow-left"
      :title="filtered ? 'Nenhuma movimentação encontrada' : 'Nenhuma movimentação no grupo ainda'"
      :description="
        filtered
          ? 'Nenhum resultado para os filtros selecionados. Ajuste ou limpe os filtros.'
          : 'Cadastre as despesas compartilhadas. Todos os participantes do grupo poderão vê-las.'
      "
    >
      <slot name="empty-actions" />
    </EmptyState>

    <template v-else-if="items.length > 0">
      <DataTable
        v-if="isTabletUp"
        :value="items"
        data-key="id"
        lazy
        :sort-field="sortField"
        :sort-order="sortOrder"
        :loading="loading"
        row-hover
        class="gtx-list__table"
        @sort="onSort"
        @row-click="onRowClick"
      >
        <Column field="transactionDate" header="Data" sortable>
          <template #body="{ data }: { data: GroupTransactionDto }">{{ formatDate(data.transactionDate) }}</template>
        </Column>
        <Column field="description" header="Descrição" sortable>
          <template #body="{ data }: { data: GroupTransactionDto }">
            <button
              type="button"
              class="gtx-list__link"
              :aria-label="`Abrir detalhes: ${summary(data)}`"
              @click.stop="emit('open', data)"
            >
              {{ data.description }}
            </button>
            <GroupSeriesBadge v-if="data.series" :series="data.series" class="gtx-list__series" />
            <CategoryBadge
              class="gtx-list__category"
              :name="data.categoryName"
              :parent-name="data.parentCategoryName"
              :color="data.categoryColor"
              :icon="data.categoryIcon"
            />
          </template>
        </Column>
        <Column header="Cadastrada por">
          <template #body="{ data }: { data: GroupTransactionDto }">{{ data.createdBy.name }}</template>
        </Column>
        <Column field="dueDate" header="Vencimento" sortable>
          <template #body="{ data }: { data: GroupTransactionDto }">{{ formatDate(data.dueDate) }}</template>
        </Column>
        <Column header="Situação">
          <template #body="{ data }: { data: GroupTransactionDto }">
            <TransactionStatusChip :status="data.status" />
          </template>
        </Column>
        <Column field="amount" header="Valor" sortable header-class="gtx-list__right" body-class="gtx-list__right">
          <template #body="{ data }: { data: GroupTransactionDto }">
            <TransactionAmount :amount="data.amount" :type="data.type" />
            <small v-if="data.totalPaid > 0" class="gtx-list__remaining">
              <GroupPaidSummary :amount="data.amount" :paid="data.totalPaid" :interest="data.interestPaid" />
            </small>
            <small v-if="showRemaining(data)" class="gtx-list__remaining">
              Restante {{ formatCurrency(data.remainingAmount) }}
            </small>
          </template>
        </Column>
      </DataTable>

      <ul v-else class="gtx-cards" aria-label="Movimentações do grupo">
        <li v-for="tx in items" :key="tx.id">
          <button
            type="button"
            class="gtx-card"
            :aria-label="`Abrir detalhes: ${summary(tx)}`"
            @click="emit('open', tx)"
          >
            <span class="gtx-card__row">
              <span class="gtx-card__title"
                >{{ tx.description }} <GroupSeriesBadge v-if="tx.series" :series="tx.series"
              /></span>
              <TransactionAmount :amount="tx.amount" :type="tx.type" />
            </span>
            <span class="gtx-card__row">
              <CategoryBadge
                :name="tx.categoryName"
                :parent-name="tx.parentCategoryName"
                :color="tx.categoryColor"
                :icon="tx.categoryIcon"
              />
              <TransactionStatusChip :status="tx.status" />
            </span>
            <span class="gtx-card__meta">
              <span>Cadastrada por: {{ tx.createdBy.name }}</span>
              <span>Data: {{ formatDate(tx.transactionDate) }}</span>
              <span v-if="tx.dueDate">Vencimento: {{ formatDate(tx.dueDate) }}</span>
              <GroupPaidSummary
                v-if="tx.totalPaid > 0"
                :amount="tx.amount"
                :paid="tx.totalPaid"
                :interest="tx.interestPaid"
              />
              <span v-if="showRemaining(tx)">Restante: {{ formatCurrency(tx.remainingAmount) }}</span>
            </span>
          </button>
        </li>
      </ul>

      <Paginator
        class="gtx-list__paginator"
        :first="first"
        :rows="pageSize"
        :total-records="totalCount"
        :rows-per-page-options="PAGE_SIZE_OPTIONS"
        template="FirstPageLink PrevPageLink CurrentPageReport NextPageLink LastPageLink RowsPerPageDropdown"
        current-page-report-template="{first}–{last} de {totalRecords}"
        @page="onPage"
      />
    </template>
  </div>
</template>

<style scoped>
.gtx-list__error {
  margin-bottom: 1rem;
}

.gtx-list__error-content {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.gtx-list__link {
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

.gtx-list__link:hover {
  text-decoration: underline;
}

.gtx-list__series {
  display: inline-block;
  margin-top: 0.25rem;
}

.gtx-list__category {
  margin-top: 0.25rem;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.gtx-list__remaining {
  display: block;
  color: var(--gf-text-muted);
}

:deep(.gtx-list__right) {
  text-align: right;
}

:deep(.gtx-list__right .p-datatable-column-header-content) {
  justify-content: flex-end;
}

:deep(.p-datatable-tbody > tr) {
  cursor: pointer;
}

.gtx-cards {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.gtx-card {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
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

.gtx-card:hover {
  background: var(--p-content-hover-background);
}

.gtx-card__row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  justify-content: space-between;
}

.gtx-card__title {
  font-weight: 600;
  overflow-wrap: anywhere;
}

.gtx-card__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem 1rem;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.gtx-list__paginator {
  margin-top: 0.75rem;
  flex-wrap: wrap;
}
</style>
